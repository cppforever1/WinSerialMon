using System.Text.RegularExpressions;

namespace WinSerialMon;

public sealed class SerialPortActionEventArgs : EventArgs
{
    private static readonly Regex ComPortPattern =
        new(@"\b(COM\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SerialPortActionEventArgs(
        SerialPortAction action,
        SerialIrpEvent irpEvent,
        int? requestedLength,
        int? completedLength,
        byte[]? readBuffer,
        byte[]? writeBuffer,
        byte[]? ioctlInputBuffer,
        byte[]? ioctlOutputBuffer)
    {
        Action = action;
        IrpEvent = irpEvent ?? throw new ArgumentNullException(nameof(irpEvent));
        RequestedLength = requestedLength;
        CompletedLength = completedLength;
        ReadBuffer = readBuffer;
        WriteBuffer = writeBuffer;
        IoctlInputBuffer = ioctlInputBuffer;
        IoctlOutputBuffer = ioctlOutputBuffer;
    }

    public SerialPortAction Action { get; }
    public SerialIrpEvent IrpEvent { get; }
    public string? DevicePath => IrpEvent.DevicePath;

    public string? ComPortName
    {
        get
        {
            var path = IrpEvent.DevicePath;
            if (string.IsNullOrWhiteSpace(path)) return null;
            var match = ComPortPattern.Match(path);
            return match.Success ? match.Value.ToUpperInvariant() : null;
        }
    }

    public int ProcessId => IrpEvent.ProcessId;
    public int ThreadId => IrpEvent.ThreadId;
    public ulong? IoControlCode => IrpEvent.IoControlCode;
    public uint? NtStatus => IrpEvent.NtStatus;

    public SerialIoctlCode KnownIoctlCode
    {
        get
        {
            if (IrpEvent.IoControlCode is not { } code) return SerialIoctlCode.Unknown;
            return Enum.IsDefined(typeof(SerialIoctlCode), code)
                ? (SerialIoctlCode)code
                : SerialIoctlCode.Unknown;
        }
    }

    public int? RequestedLength { get; }
    public int? CompletedLength { get; }
    public byte[]? ReadBuffer { get; }
    public byte[]? WriteBuffer { get; }
    public byte[]? IoctlInputBuffer { get; }
    public byte[]? IoctlOutputBuffer { get; }

    // ── Decoded IOCTL parameters ──────────────────────────────────────────────

    /// <summary>New baud rate from SetBaudRate or SetCommConfig IOCTL.</summary>
    public uint? NewBaudRate
    {
        get
        {
            if (KnownIoctlCode == SerialIoctlCode.SetBaudRate
                && IoctlInputBuffer is { Length: >= 4 } buf)
            {
                return BitConverter.ToUInt32(buf, 0);
            }
            return NewCommConfig?.BaudRate;
        }
    }

    /// <summary>Decoded data bits, stop bits, parity from SetLineControl or SetCommConfig IOCTL.</summary>
    public SerialLineControlSettings? NewLineControl
    {
        get
        {
            if (KnownIoctlCode == SerialIoctlCode.SetLineControl)
                return SerialLineControlSettings.TryDecode(IoctlInputBuffer);
            if (NewCommConfig is { } cfg)
                return new SerialLineControlSettings(cfg.DataBits, cfg.StopBits, cfg.Parity);
            return null;
        }
    }

    /// <summary>Decoded handshake/flow-control settings from SetHandFlow IOCTL.</summary>
    public SerialHandFlowSettings? NewHandFlow =>
        KnownIoctlCode == SerialIoctlCode.SetHandFlow
            ? SerialHandFlowSettings.TryDecode(IoctlInputBuffer)
            : null;

    /// <summary>Decoded read/write timeout settings from SetTimeouts or GetTimeouts IOCTL.</summary>
    public SerialTimeoutSettings? NewTimeouts =>
        KnownIoctlCode switch
        {
            SerialIoctlCode.SetTimeouts => SerialTimeoutSettings.TryDecode(IoctlInputBuffer),
            SerialIoctlCode.GetTimeouts => SerialTimeoutSettings.TryDecode(IoctlOutputBuffer),
            _ => null
        };

    /// <summary>Decoded modem status register bits from GetModemStatus IOCTL.</summary>
    public SerialModemStatus? ModemStatus =>
        KnownIoctlCode == SerialIoctlCode.GetModemStatus
            ? SerialModemStatus.TryDecode(IoctlOutputBuffer)
            : null;

    /// <summary>Fully decoded COMMCONFIG/DCB from SetCommConfig IOCTL (baud, data bits, stop bits, parity, flow control).</summary>
    public SerialCommConfigSettings? NewCommConfig =>
        KnownIoctlCode == SerialIoctlCode.SetCommConfig
            ? SerialCommConfigSettings.TryDecode(IoctlInputBuffer)
            : null;
}
