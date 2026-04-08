namespace WinSerialMon;

/// <summary>
/// Stop-bit values as defined in the Windows serial driver (ntddser.h / winbase.h).
/// </summary>
public enum SerialStopBits : byte
{
    One = 0,            // ONESTOPBIT
    OnePointFive = 1,   // ONE5STOPBITS
    Two = 2             // TWOSTOPBITS
}
