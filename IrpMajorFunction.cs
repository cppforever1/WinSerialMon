namespace WinSerialMon;

public enum IrpMajorFunction
{
    Unknown = -1,
    Create = 0x00,
    CreateNamedPipe = 0x01,
    Close = 0x02,
    Read = 0x03,
    Write = 0x04,
    QueryInformation = 0x05,
    SetInformation = 0x06,
    QueryEa = 0x07,
    SetEa = 0x08,
    FlushBuffers = 0x09,
    QueryVolumeInformation = 0x0A,
    SetVolumeInformation = 0x0B,
    DirectoryControl = 0x0C,
    FileSystemControl = 0x0D,
    DeviceControl = 0x0E,
    InternalDeviceControl = 0x0F,
    Shutdown = 0x10,
    LockControl = 0x11,
    Cleanup = 0x12,
    CreateMailslot = 0x13,
    QuerySecurity = 0x14,
    SetSecurity = 0x15,
    Power = 0x16,
    SystemControl = 0x17,
    DeviceChange = 0x18,
    QueryQuota = 0x19,
    SetQuota = 0x1A,
    Pnp = 0x1B
}
