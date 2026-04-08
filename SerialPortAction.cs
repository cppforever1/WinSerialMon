namespace WinSerialMon;

public enum SerialPortAction
{
    Unknown = 0,
    Open,
    Close,
    Read,
    Write,
    Ioctl
}
