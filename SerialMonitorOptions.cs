namespace WinSerialMon;

public sealed class SerialMonitorOptions
{
    public string SessionName { get; init; } = $"WinSerialMon-{Guid.NewGuid():N}";
    public int BufferSizeMB { get; init; } = 64;
    public int MinimumBuffers { get; init; } = 32;
    public int MaximumBuffers { get; init; } = 128;
    public bool EnableKernelFileIoProvider { get; init; } = true;
    public bool EnableKernelIoTraceProvider { get; init; } = true;
    public bool IncludeEventsWithoutKnownIrpMajor { get; init; } = false;
    public SynchronizationContext? CallbackSynchronizationContext { get; init; }
    public bool PostCallbacksToSynchronizationContext { get; init; } = true;

    public Func<string?, bool> SerialPathFilter { get; init; } = static path =>
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.Contains("serial", StringComparison.OrdinalIgnoreCase)
            || path.Contains("usbser", StringComparison.OrdinalIgnoreCase)
            || path.Contains("\\\\.\\\\COM", StringComparison.OrdinalIgnoreCase)
            || path.Contains("COM", StringComparison.OrdinalIgnoreCase);
    };
}
