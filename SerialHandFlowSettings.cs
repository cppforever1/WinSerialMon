namespace WinSerialMon;

/// <summary>
/// Decoded SERIAL_HANDFLOW structure sent with IOCTL_SERIAL_SET_HANDFLOW.
/// Binary layout (16 bytes, little-endian):
///   ULONG ControlHandShake  – offset 0
///   ULONG FlowReplace       – offset 4
///   LONG  XonLimit          – offset 8
///   LONG  XoffLimit         – offset 12
/// </summary>
public sealed record SerialHandFlowSettings(
    uint ControlHandShake,
    uint FlowReplace,
    int XonLimit,
    int XoffLimit)
{
    // ── ControlHandShake flag masks (ntddser.h) ───────────────────────────────
    private const uint DtrControl    = 0x00000001;
    private const uint DtrHandshake  = 0x00000002;
    private const uint CtsHandshake  = 0x00000008;
    private const uint DsrHandshake  = 0x00000010;
    private const uint DcdHandshake  = 0x00000020;
    private const uint DsrSensitivity = 0x00000040;
    private const uint ErrorAbort    = 0x80000000;

    // ── FlowReplace flag masks (ntddser.h) ────────────────────────────────────
    private const uint AutoTransmit  = 0x00000001; // XON/XOFF on output
    private const uint AutoReceive   = 0x00000002; // XON/XOFF on input
    private const uint ErrorChar     = 0x00000004;
    private const uint NullStripping = 0x00000008;
    private const uint BreakChar     = 0x00000010;
    private const uint RtsControl    = 0x00000040;
    private const uint RtsHandshake  = 0x00000080;
    private const uint RtsMask       = 0x000000C0;
    private const uint XoffContinue  = 0x80000000;

    // ── ControlHandShake properties ───────────────────────────────────────────

    /// <summary>DTR line asserted (driven high) by the driver.</summary>
    public bool IsDtrControl => (ControlHandShake & DtrControl) != 0;

    /// <summary>DTR used as a handshake line (driver controls it automatically).</summary>
    public bool IsDtrHandshake => (ControlHandShake & DtrHandshake) != 0;

    /// <summary>Output is suspended until CTS is asserted.</summary>
    public bool IsCtsHandshake => (ControlHandShake & CtsHandshake) != 0;

    /// <summary>Output is suspended until DSR is asserted.</summary>
    public bool IsDsrHandshake => (ControlHandShake & DsrHandshake) != 0;

    /// <summary>Output is suspended until DCD is asserted.</summary>
    public bool IsDcdHandshake => (ControlHandShake & DcdHandshake) != 0;

    /// <summary>Received bytes are ignored when DSR is not asserted.</summary>
    public bool IsDsrSensitivity => (ControlHandShake & DsrSensitivity) != 0;

    /// <summary>I/O is aborted on a line error (framing, overrun, parity).</summary>
    public bool IsErrorAbort => (ControlHandShake & ErrorAbort) != 0;

    // ── FlowReplace properties ────────────────────────────────────────────────

    /// <summary>XON/XOFF flow control enabled on transmitted data.</summary>
    public bool IsXonXoffTransmit => (FlowReplace & AutoTransmit) != 0;

    /// <summary>XON/XOFF flow control enabled on received data.</summary>
    public bool IsXonXoffReceive => (FlowReplace & AutoReceive) != 0;

    /// <summary>RTS line is asserted (driven high) by the driver.</summary>
    public bool IsRtsControl => (FlowReplace & RtsMask) == RtsControl;

    /// <summary>RTS is used as a hardware handshake line.</summary>
    public bool IsRtsHandshake => (FlowReplace & RtsMask) == RtsHandshake;

    /// <summary>RTS is toggled on each transmit (half-duplex RS-485 style).</summary>
    public bool IsRtsTransmitToggle => (FlowReplace & RtsMask) == RtsMask;

    /// <summary>Transmission continues after the XOFF threshold is reached.</summary>
    public bool IsXoffContinue => (FlowReplace & XoffContinue) != 0;

    // ── Decode ────────────────────────────────────────────────────────────────

    public static SerialHandFlowSettings? TryDecode(byte[]? buffer)
    {
        if (buffer is null || buffer.Length < 16)
        {
            return null;
        }

        return new SerialHandFlowSettings(
            ControlHandShake: BitConverter.ToUInt32(buffer, 0),
            FlowReplace:      BitConverter.ToUInt32(buffer, 4),
            XonLimit:         BitConverter.ToInt32(buffer, 8),
            XoffLimit:        BitConverter.ToInt32(buffer, 12));
    }
}
