namespace WinSerialMon;

/// <summary>
/// Decoded SERIAL_LINE_CONTROL structure sent with IOCTL_SERIAL_SET_LINE_CONTROL.
/// Binary layout (3 bytes):
///   byte 0 – StopBits   (STOP_BIT_1=0, STOP_BITS_1_5=1, STOP_BITS_2=2)
///   byte 1 – Parity     (NO=0, ODD=1, EVEN=2, MARK=3, SPACE=4)
///   byte 2 – WordLength (data bits: 5, 6, 7, or 8)
/// </summary>
public sealed record SerialLineControlSettings(
    int DataBits,
    SerialStopBits StopBits,
    SerialParity Parity)
{
    public static SerialLineControlSettings? TryDecode(byte[]? buffer)
    {
        if (buffer is null || buffer.Length < 3)
        {
            return null;
        }

        return new SerialLineControlSettings(
            DataBits: buffer[2],
            StopBits: (SerialStopBits)buffer[0],
            Parity: (SerialParity)buffer[1]);
    }
}
