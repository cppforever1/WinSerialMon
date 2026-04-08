namespace WinSerialMon;

/// <summary>
/// Parity values as defined in the Windows serial driver (ntddser.h / winbase.h).
/// </summary>
public enum SerialParity : byte
{
    None = 0,
    Odd = 1,
    Even = 2,
    Mark = 3,
    Space = 4
}
