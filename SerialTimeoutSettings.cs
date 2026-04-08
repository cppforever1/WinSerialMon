namespace WinSerialMon;

/// <summary>
/// Decoded SERIAL_TIMEOUTS structure used by IOCTL_SERIAL_SET_TIMEOUTS and
/// IOCTL_SERIAL_GET_TIMEOUTS.
/// Binary layout (20 bytes, little-endian):
///   ULONG ReadIntervalTimeout        - offset 0
///   ULONG ReadTotalTimeoutMultiplier - offset 4
///   ULONG ReadTotalTimeoutConstant   - offset 8
///   ULONG WriteTotalTimeoutMultiplier- offset 12
///   ULONG WriteTotalTimeoutConstant  - offset 16
/// </summary>
public sealed record SerialTimeoutSettings(
    uint ReadIntervalTimeout,
    uint ReadTotalTimeoutMultiplier,
    uint ReadTotalTimeoutConstant,
    uint WriteTotalTimeoutMultiplier,
    uint WriteTotalTimeoutConstant)
{
    public static SerialTimeoutSettings? TryDecode(byte[]? buffer)
    {
        if (buffer is null || buffer.Length < 20)
        {
            return null;
        }

        return new SerialTimeoutSettings(
            ReadIntervalTimeout: BitConverter.ToUInt32(buffer, 0),
            ReadTotalTimeoutMultiplier: BitConverter.ToUInt32(buffer, 4),
            ReadTotalTimeoutConstant: BitConverter.ToUInt32(buffer, 8),
            WriteTotalTimeoutMultiplier: BitConverter.ToUInt32(buffer, 12),
            WriteTotalTimeoutConstant: BitConverter.ToUInt32(buffer, 16));
    }
}