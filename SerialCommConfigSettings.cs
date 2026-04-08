namespace WinSerialMon;

/// <summary>
/// Decoded COMMCONFIG / DCB structure sent with IOCTL_SERIAL_SET_COMMCONFIG.
/// A single COMMCONFIG applies baud rate, data bits, stop bits, parity, and
/// flow control all at once.
///
/// COMMCONFIG binary layout (offsets from start of buffer):
///   offset  0 – dwSize      (DWORD)
///   offset  4 – wVersion    (WORD)
///   offset  6 – wReserved   (WORD)
///   offset  8 – DCB start   (28 bytes)
///     DCB offset  0 – DCBlength  (DWORD)
///     DCB offset  4 – BaudRate   (DWORD)
///     DCB offset  8 – fBitFields (DWORD)  – packed flags
///     DCB offset 12 – wReserved  (WORD)
///     DCB offset 14 – XonLim     (WORD)
///     DCB offset 16 – XoffLim    (WORD)
///     DCB offset 18 – ByteSize   (BYTE)  – data bits
///     DCB offset 19 – Parity     (BYTE)
///     DCB offset 20 – StopBits   (BYTE)
/// </summary>
public sealed record SerialCommConfigSettings(
    uint BaudRate,
    int DataBits,
    SerialStopBits StopBits,
    SerialParity Parity,
    bool IsCtsFlowControl,
    bool IsDsrFlowControl,
    bool IsDtrControl,
    bool IsDtrHandshake,
    bool IsRtsControl,
    bool IsRtsHandshake,
    bool IsRtsTransmitToggle,
    bool IsXonXoffTransmit,
    bool IsXonXoffReceive,
    bool IsAbortOnError)
{
    // DCB fBitFields bit definitions (winbase.h)
    private const uint FOutxCtsFlow  = 1u << 2;
    private const uint FOutxDsrFlow  = 1u << 3;
    private const uint FDtrMask      = 3u << 4;
    private const uint FDtrControl   = 1u << 4;
    private const uint FDtrHandshake = 2u << 4;
    private const uint FOutX         = 1u << 8;
    private const uint FInX          = 1u << 9;
    private const uint FRtsMask      = 3u << 12;
    private const uint FRtsControl   = 1u << 12;
    private const uint FRtsHandshake = 2u << 12;
    private const uint FRtsToggle    = 3u << 12;
    private const uint FAbortOnError = 1u << 14;

    private const int CommConfigDcbOffset = 8;

    public static SerialCommConfigSettings? TryDecode(byte[]? buffer)
    {
        // Need at least COMMCONFIG header (8) + DCB through StopBits (21 bytes) = 29
        if (buffer is null || buffer.Length < CommConfigDcbOffset + 21)
        {
            return null;
        }

        var baud     = BitConverter.ToUInt32(buffer, CommConfigDcbOffset + 4);
        var flags    = BitConverter.ToUInt32(buffer, CommConfigDcbOffset + 8);
        var dataBytes = buffer[CommConfigDcbOffset + 18];
        var parity   = buffer[CommConfigDcbOffset + 19];
        var stopBits = buffer[CommConfigDcbOffset + 20];

        return new SerialCommConfigSettings(
            BaudRate:             baud,
            DataBits:             dataBytes,
            StopBits:             (SerialStopBits)stopBits,
            Parity:               (SerialParity)parity,
            IsCtsFlowControl:     (flags & FOutxCtsFlow)  != 0,
            IsDsrFlowControl:     (flags & FOutxDsrFlow)  != 0,
            IsDtrControl:         (flags & FDtrMask)      == FDtrControl,
            IsDtrHandshake:       (flags & FDtrMask)      == FDtrHandshake,
            IsRtsControl:         (flags & FRtsMask)      == FRtsControl,
            IsRtsHandshake:       (flags & FRtsMask)      == FRtsHandshake,
            IsRtsTransmitToggle:  (flags & FRtsMask)      == FRtsToggle,
            IsXonXoffTransmit:    (flags & FOutX)         != 0,
            IsXonXoffReceive:     (flags & FInX)          != 0,
            IsAbortOnError:       (flags & FAbortOnError) != 0);
    }
}
