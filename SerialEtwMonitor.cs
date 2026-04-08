using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using NLog;
using NLog.Config;

namespace WinSerialMon;

public sealed class SerialEtwMonitor : IDisposable
{
    private static readonly Logger Logger;

    static SerialEtwMonitor()
    {
        if (LogManager.Configuration is null)
        {
            var configPath = Path.Combine(
                AppContext.BaseDirectory, "NLog.config");

            if (File.Exists(configPath))
            {
                LogManager.Setup().LoadConfigurationFromFile(configPath);
            }
        }

        Logger = LogManager.GetCurrentClassLogger();
    }

    private readonly object _stateGate = new();
    private readonly List<ISerialIrpEventCallback> _callbacks = new();
    private readonly ConcurrentDictionary<string, object?> _scratchPayload = new(StringComparer.OrdinalIgnoreCase);

    private TraceEventSession? _session;
    private ETWTraceEventSource? _source;
    private Task? _processingTask;
    private bool _disposed;

    public SerialEtwMonitor(SerialMonitorOptions? options = null)
    {
        Options = options ?? new SerialMonitorOptions();
    }

    public SerialMonitorOptions Options { get; }

    public bool IsRunning
    {
        get
        {
            lock (_stateGate)
            {
                return _processingTask is { IsCompleted: false };
            }
        }
    }

    public event Action<SerialIrpEvent>? IrpEventReceived;
    public event Action<Exception>? MonitorError;
    public event SerialIrpEventReceivedEventHandler? SerialIrpEventReceived;
    public event SerialMonitorErrorEventHandler? SerialMonitorError;
    public event SerialPortOpenEventHandler? SerialPortOpened;
    public event SerialPortCloseEventHandler? SerialPortClosed;
    public event SerialPortReadEventHandler? SerialPortRead;
    public event SerialPortWriteEventHandler? SerialPortWritten;
    public event SerialPortIoctlEventHandler? SerialPortIoctl;

    public void RegisterCallback(ISerialIrpEventCallback callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (_callbacks)
        {
            _callbacks.Add(callback);
            Logger.Debug("Callback registered: {0}.", callback.GetType().FullName);
        }
    }

    public bool UnregisterCallback(ISerialIrpEventCallback callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (_callbacks)
        {
            var removed = _callbacks.Remove(callback);
            Logger.Debug("Callback unregister attempt: {0} removed={1}.", callback.GetType().FullName, removed);
            return removed;
        }
    }

    public void Start()
    {
        ThrowIfDisposed();

        lock (_stateGate)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Serial ETW monitor is already running.");
            }

            Logger.Info("Starting Serial ETW monitor session '{0}'.", Options.SessionName);

            _session = new TraceEventSession(Options.SessionName);
            _session.StopOnDispose = true;
            _session.BufferSizeMB = Options.BufferSizeMB;

            if (Options.EnableKernelFileIoProvider)
            {
                _session.EnableKernelProvider(KernelTraceEventParser.Keywords.FileIOInit | KernelTraceEventParser.Keywords.FileIO);
                Logger.Debug("Kernel FileIO provider enabled (FileIOInit | FileIO).");
            }

            if (Options.EnableKernelIoTraceProvider)
            {
                _session.EnableProvider("Microsoft-Windows-Kernel-IoTrace", TraceEventLevel.Verbose, ulong.MaxValue);
                Logger.Debug("Microsoft-Windows-Kernel-IoTrace provider enabled at Verbose level.");
            }

            _source = new ETWTraceEventSource(Options.SessionName, TraceEventSourceType.Session);
            _source.Dynamic.All += OnEtwEvent;

            _processingTask = Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        _source.Process();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Unhandled exception while processing ETW stream.");
                        PublishError(ex);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Logger.Info("Serial ETW monitor session '{0}' started.", Options.SessionName);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? processingTask;

        lock (_stateGate)
        {
            if (!IsRunning)
            {
                return;
            }

            Logger.Info("Stopping Serial ETW monitor session '{0}'.", Options.SessionName);

            _source?.StopProcessing();
            _session?.Dispose();
            _source?.Dispose();

            processingTask = _processingTask;
            _processingTask = null;
            _source = null;
            _session = null;
        }

        if (processingTask is null)
        {
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var waitTask = Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token);
        var completedTask = await Task.WhenAny(processingTask, waitTask).ConfigureAwait(false);

        if (completedTask != processingTask)
        {
            throw new OperationCanceledException("Stopping ETW monitor was canceled.", cancellationToken);
        }

        linkedCts.Cancel();
        await processingTask.ConfigureAwait(false);
        Logger.Info("Serial ETW monitor session '{0}' stopped.", Options.SessionName);
    }

    private void OnEtwEvent(TraceEvent traceEvent)
    {
        try
        {
            var providerName = traceEvent.ProviderName ?? string.Empty;
            var eventName = traceEvent.EventName ?? string.Empty;

            Logger.Trace("ETW raw event: provider='{0}' event='{1}' opcode='{2}' pid={3} tid={4}.",
                providerName, eventName, traceEvent.OpcodeName, traceEvent.ProcessID, traceEvent.ThreadID);

            if (!LooksLikeKernelOrIoTraceProvider(providerName))
            {
                Logger.Trace("Skipped: provider '{0}' is not a kernel/IoTrace provider.", providerName);
                return;
            }

            var payload = BuildPayload(traceEvent);

            if (Logger.IsTraceEnabled)
            {
                Logger.Trace("Payload built: {0} field(s): [{1}].", payload.Count, string.Join(", ", payload.Keys));
            }

            var majorFunction = ParseMajorFunction(traceEvent, payload, eventName);
            Logger.Trace("IRP major function resolved: {0} (0x{1:X2}).", majorFunction, (int)majorFunction);

            if (!Options.IncludeEventsWithoutKnownIrpMajor && majorFunction == IrpMajorFunction.Unknown)
            {
                Logger.Trace("Skipped: IRP major function is Unknown and IncludeEventsWithoutKnownIrpMajor is false.");
                return;
            }

            var devicePath = GetAsString(payload,
                "FileName", "DeviceName", "Path", "FilePath", "ObjectName", "TargetName", "FileObjectName");

            Logger.Trace("Device path resolved: '{0}'.", devicePath ?? "<null>");

            if (!Options.SerialPathFilter(devicePath))
            {
                Logger.Trace("Skipped: device path '{0}' did not pass serial path filter.", devicePath ?? "<null>");
                return;
            }

            var serialEvent = new SerialIrpEvent(
                TimestampUtc: traceEvent.TimeStamp.ToUniversalTime(),
                ProcessId: traceEvent.ProcessID,
                ThreadId: traceEvent.ThreadID,
                ProviderName: providerName,
                EventName: eventName,
                MajorFunction: majorFunction,
                DevicePath: devicePath,
                IrpAddress: GetAsString(payload, "Irp", "IrpPtr", "IrpAddress"),
                IoControlCode: GetAsUInt64(payload, "IoControlCode", "ControlCode"),
                NtStatus: GetAsUInt32(payload, "Status", "NtStatus"),
                Information: GetAsUInt64(payload, "Information", "TransferSize", "Length"),
                Payload: payload);

            Logger.Trace("SerialIrpEvent built: device='{0}' irp={1} major={2} ioctl=0x{3:X} status=0x{4:X} info={5}.",
                serialEvent.DevicePath ?? "<null>",
                serialEvent.IrpAddress ?? "<null>",
                serialEvent.MajorFunction,
                serialEvent.IoControlCode ?? 0,
                serialEvent.NtStatus ?? 0,
                serialEvent.Information ?? 0);

            var actionEventArgs = BuildActionEventArgs(serialEvent, payload);

            Logger.Trace("Action event built: action={0} reqLen={1} cmpLen={2} readBuf={3}B writeBuf={4}B ioctlIn={5}B ioctlOut={6}B.",
                actionEventArgs.Action,
                actionEventArgs.RequestedLength?.ToString(CultureInfo.InvariantCulture) ?? "null",
                actionEventArgs.CompletedLength?.ToString(CultureInfo.InvariantCulture) ?? "null",
                actionEventArgs.ReadBuffer?.Length ?? 0,
                actionEventArgs.WriteBuffer?.Length ?? 0,
                actionEventArgs.IoctlInputBuffer?.Length ?? 0,
                actionEventArgs.IoctlOutputBuffer?.Length ?? 0);

            DispatchToCallbackContext(() =>
            {
                IrpEventReceived?.Invoke(serialEvent);
                SerialIrpEventReceived?.Invoke(this, new SerialIrpEventArgs(serialEvent));
                PublishActionEvent(actionEventArgs);
            });

            List<ISerialIrpEventCallback> callbacks;
            lock (_callbacks)
            {
                callbacks = new List<ISerialIrpEventCallback>(_callbacks);
            }

            foreach (var callback in callbacks)
            {
                try
                {
                    Logger.Trace("Invoking OnIrpEvent on callback '{0}'.", callback.GetType().Name);
                    DispatchToCallbackContext(() => callback.OnIrpEvent(serialEvent));
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Callback threw while handling serial IRP event.");
                    DispatchToCallbackContext(() => callback.OnMonitorError(ex));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unhandled exception while handling ETW event callback.");
            PublishError(ex);
        }
    }

    private static bool LooksLikeKernelOrIoTraceProvider(string providerName)
    {
        return providerName.Contains("Kernel", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("IoTrace", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("NT Kernel Logger", StringComparison.OrdinalIgnoreCase);
    }

    private static IrpMajorFunction ParseMajorFunction(TraceEvent traceEvent, IReadOnlyDictionary<string, object?> payload, string eventName)
    {
        var explicitName = GetAsString(payload,
            "MajorFunction", "IrpMajorFunction", "MajorCode", "IrpMj", "Major");

        if (TryParseMajorFromString(explicitName, out var parsedFromName))
        {
            return parsedFromName;
        }

        var explicitNumber = GetAsUInt32(payload,
            "MajorFunction", "IrpMajorFunction", "MajorCode", "IrpMj", "Major");

        if (explicitNumber is not null && Enum.IsDefined(typeof(IrpMajorFunction), (int)explicitNumber.Value))
        {
            return (IrpMajorFunction)explicitNumber.Value;
        }

        if (TryParseMajorFromString(eventName, out var parsedFromEventName))
        {
            return parsedFromEventName;
        }

        if (TryParseMajorFromString(traceEvent.OpcodeName, out var parsedFromOpcodeName))
        {
            return parsedFromOpcodeName;
        }

        return IrpMajorFunction.Unknown;
    }

    private static bool TryParseMajorFromString(string? text, out IrpMajorFunction majorFunction)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            majorFunction = IrpMajorFunction.Unknown;
            return false;
        }

        var normalized = text.Trim().Replace("IRP_MJ_", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (Enum.TryParse<IrpMajorFunction>(normalized, ignoreCase: true, out majorFunction))
        {
            return true;
        }

        majorFunction = normalized.ToUpperInvariant() switch
        {
            "CREATE" => IrpMajorFunction.Create,
            "READ" => IrpMajorFunction.Read,
            "WRITE" => IrpMajorFunction.Write,
            "CLEANUP" => IrpMajorFunction.Cleanup,
            "CLOSE" => IrpMajorFunction.Close,
            "DEVICECONTROL" => IrpMajorFunction.DeviceControl,
            "INTERNALDEVICECONTROL" => IrpMajorFunction.InternalDeviceControl,
            "PNP" => IrpMajorFunction.Pnp,
            "POWER" => IrpMajorFunction.Power,
            _ => IrpMajorFunction.Unknown
        };

        return majorFunction != IrpMajorFunction.Unknown;
    }

    private IReadOnlyDictionary<string, object?> BuildPayload(TraceEvent traceEvent)
    {
        _scratchPayload.Clear();

        foreach (var payloadName in traceEvent.PayloadNames)
        {
            _scratchPayload[payloadName] = traceEvent.PayloadByName(payloadName);
        }

        return new Dictionary<string, object?>(_scratchPayload, StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetAsString(IReadOnlyDictionary<string, object?> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            var text = Convert.ToString(value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static uint? GetAsUInt32(IReadOnlyDictionary<string, object?> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (value is uint u32)
            {
                return u32;
            }

            if (value is int i32 && i32 >= 0)
            {
                return (uint)i32;
            }

            if (value is long i64 && i64 >= 0 && i64 <= uint.MaxValue)
            {
                return (uint)i64;
            }

            if (value is ulong u64 && u64 <= uint.MaxValue)
            {
                return (uint)u64;
            }

            if (uint.TryParse(Convert.ToString(value), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static ulong? GetAsUInt64(IReadOnlyDictionary<string, object?> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (value is ulong u64)
            {
                return u64;
            }

            if (value is long i64 && i64 >= 0)
            {
                return (ulong)i64;
            }

            if (value is uint u32)
            {
                return u32;
            }

            if (value is int i32 && i32 >= 0)
            {
                return (ulong)i32;
            }

            if (ulong.TryParse(Convert.ToString(value), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? GetAsInt32(IReadOnlyDictionary<string, object?> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (value is int i32)
            {
                return i32;
            }

            if (value is uint u32 && u32 <= int.MaxValue)
            {
                return (int)u32;
            }

            if (value is long i64 && i64 >= 0 && i64 <= int.MaxValue)
            {
                return (int)i64;
            }

            if (value is ulong u64 && u64 <= int.MaxValue)
            {
                return (int)u64;
            }

            if (int.TryParse(Convert.ToString(value), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static SerialPortAction ToSerialPortAction(IrpMajorFunction majorFunction)
    {
        return majorFunction switch
        {
            IrpMajorFunction.Create => SerialPortAction.Open,
            IrpMajorFunction.Close => SerialPortAction.Close,
            IrpMajorFunction.Cleanup => SerialPortAction.Close,
            IrpMajorFunction.Read => SerialPortAction.Read,
            IrpMajorFunction.Write => SerialPortAction.Write,
            IrpMajorFunction.DeviceControl => SerialPortAction.Ioctl,
            IrpMajorFunction.InternalDeviceControl => SerialPortAction.Ioctl,
            _ => SerialPortAction.Unknown
        };
    }

    private SerialPortActionEventArgs BuildActionEventArgs(SerialIrpEvent serialEvent, IReadOnlyDictionary<string, object?> payload)
    {
        var action = ToSerialPortAction(serialEvent.MajorFunction);
        var requestedLength = GetAsInt32(payload, "Length", "RequestedLength", "RequestLength", "DataSize", "Size");
        var completedLength = GetAsInt32(payload, "TransferSize", "Information", "BytesTransferred", "ByteCount");

        var readBuffer = action == SerialPortAction.Read
            ? GetAsBytes(payload, "ReadBuffer", "Buffer", "Data", "Payload", "Bytes")
            : null;
        var writeBuffer = action == SerialPortAction.Write
            ? GetAsBytes(payload, "WriteBuffer", "Buffer", "Data", "Payload", "Bytes")
            : null;
        var ioctlInputBuffer = action == SerialPortAction.Ioctl
            ? GetAsBytes(payload, "InputBuffer", "InBuffer", "InputData", "Data", "Payload")
            : null;
        var ioctlOutputBuffer = action == SerialPortAction.Ioctl
            ? GetAsBytes(payload, "OutputBuffer", "OutBuffer", "OutputData", "ResultBuffer")
            : null;

        return new SerialPortActionEventArgs(
            action,
            serialEvent,
            requestedLength,
            completedLength,
            readBuffer,
            writeBuffer,
            ioctlInputBuffer,
            ioctlOutputBuffer);
    }

    private void PublishActionEvent(SerialPortActionEventArgs actionEventArgs)
    {
        Logger.Trace("Publishing action event: {0} on device '{1}'.", actionEventArgs.Action, actionEventArgs.DevicePath ?? "<null>");

        switch (actionEventArgs.Action)
        {
            case SerialPortAction.Open:
                SerialPortOpened?.Invoke(this, actionEventArgs);
                break;
            case SerialPortAction.Close:
                SerialPortClosed?.Invoke(this, actionEventArgs);
                break;
            case SerialPortAction.Read:
                SerialPortRead?.Invoke(this, actionEventArgs);
                break;
            case SerialPortAction.Write:
                SerialPortWritten?.Invoke(this, actionEventArgs);
                break;
            case SerialPortAction.Ioctl:
                SerialPortIoctl?.Invoke(this, actionEventArgs);
                break;
        }
    }

    private static byte[]? GetAsBytes(IReadOnlyDictionary<string, object?> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (TryConvertToBytes(value, out var bytes))
            {
                return bytes;
            }
        }

        return null;
    }

    private static bool TryConvertToBytes(object value, out byte[]? bytes)
    {
        switch (value)
        {
            case byte[] direct:
                bytes = direct;
                return true;
            case ReadOnlyMemory<byte> rom:
                bytes = rom.ToArray();
                return true;
            case Memory<byte> mem:
                bytes = mem.ToArray();
                return true;
            case string text:
                return TryParseHexOrBase64(text, out bytes);
            case IEnumerable<byte> enumerable:
                bytes = enumerable.ToArray();
                return true;
            default:
                bytes = null;
                return false;
        }
    }

    private static bool TryParseHexOrBase64(string text, out byte[]? bytes)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            bytes = null;
            return false;
        }

        if (TryParseHex(trimmed, out bytes))
        {
            return true;
        }

        try
        {
            bytes = Convert.FromBase64String(trimmed);
            return true;
        }
        catch
        {
            bytes = null;
            return false;
        }
    }

    private static bool TryParseHex(string text, out byte[]? bytes)
    {
        var cleaned = text
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);

        if (cleaned.Length == 0 || cleaned.Length % 2 != 0)
        {
            bytes = null;
            return false;
        }

        var result = new byte[cleaned.Length / 2];

        for (var i = 0; i < result.Length; i++)
        {
            var token = cleaned.Substring(i * 2, 2);
            if (!byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
            {
                bytes = null;
                return false;
            }

            result[i] = parsed;
        }

        bytes = result;
        return true;
    }

    private void PublishError(Exception exception)
    {
        Logger.Error(exception, "Serial monitor error published.");

        DispatchToCallbackContext(() =>
        {
            MonitorError?.Invoke(exception);
            SerialMonitorError?.Invoke(this, new SerialMonitorErrorEventArgs(exception));
        });

        List<ISerialIrpEventCallback> callbacks;
        lock (_callbacks)
        {
            callbacks = new List<ISerialIrpEventCallback>(_callbacks);
        }

        foreach (var callback in callbacks)
        {
            try
            {
                DispatchToCallbackContext(() => callback.OnMonitorError(exception));
            }
            catch
            {
                // Intentionally swallowed to avoid recursive callback failures.
            }
        }
    }

    private void DispatchToCallbackContext(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (!Options.PostCallbacksToSynchronizationContext)
        {
            Logger.Trace("Dispatching callback inline (PostCallbacksToSynchronizationContext disabled).");
            callback();
            return;
        }

        var context = Options.CallbackSynchronizationContext;
        if (context is null)
        {
            Logger.Trace("Dispatching callback inline (no SynchronizationContext configured).");
            callback();
            return;
        }

        Logger.Trace("Posting callback to SynchronizationContext '{0}'.", context.GetType().Name);
        context.Post(static state =>
        {
            if (state is Action action)
            {
                action();
            }
        }, callback);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SerialEtwMonitor));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAsync().GetAwaiter().GetResult();
        _disposed = true;
        Logger.Info("SerialEtwMonitor disposed.");
    }
}
