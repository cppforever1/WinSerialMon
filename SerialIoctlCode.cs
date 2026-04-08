namespace WinSerialMon;

/// <summary>
/// Well-known IOCTL codes issued to the Windows serial port driver (serial.sys / usbser.sys).
/// Values are computed from CTL_CODE(FILE_DEVICE_SERIAL_PORT, n, METHOD_BUFFERED, FILE_ANY_ACCESS)
/// which expands to: 0x001B0000 | (n &lt;&lt; 2).
/// </summary>
public enum SerialIoctlCode : ulong
{
    Unknown = 0,

    // ── Baud rate ─────────────────────────────────────────────────────────────
    SetBaudRate = 0x001B0004,   // IOCTL_SERIAL_SET_BAUD_RATE   – input: SERIAL_BAUD_RATE { ULONG BaudRate }
    GetBaudRate = 0x001B0084,   // IOCTL_SERIAL_GET_BAUD_RATE   – output: SERIAL_BAUD_RATE

    // ── Line control: data bits, stop bits, parity ───────────────────────────
    SetLineControl = 0x001B000C, // IOCTL_SERIAL_SET_LINE_CONTROL – input: SERIAL_LINE_CONTROL { StopBits, Parity, WordLength }
    GetLineControl = 0x001B0088, // IOCTL_SERIAL_GET_LINE_CONTROL – output: SERIAL_LINE_CONTROL

    // ── Handshake / flow control ──────────────────────────────────────────────
    SetHandFlow = 0x001B006C,   // IOCTL_SERIAL_SET_HANDFLOW    – input: SERIAL_HANDFLOW { ControlHandShake, FlowReplace, … }
    GetHandFlow = 0x001B0068,   // IOCTL_SERIAL_GET_HANDFLOW    – output: SERIAL_HANDFLOW

    // ── Timeouts ──────────────────────────────────────────────────────────────
    SetTimeouts = 0x001B001C,   // IOCTL_SERIAL_SET_TIMEOUTS    – input: SERIAL_TIMEOUTS
    GetTimeouts = 0x001B0020,   // IOCTL_SERIAL_GET_TIMEOUTS    – output: SERIAL_TIMEOUTS

    // ── Comm config (wraps DCB — baud, parity, stop, byte size, flow) ────────
    SetCommConfig = 0x001B0078, // IOCTL_SERIAL_SET_COMMCONFIG  – input: COMMCONFIG (contains DCB)
    GetCommConfig = 0x001B0074, // IOCTL_SERIAL_GET_COMMCONFIG  – output: COMMCONFIG

    // ── Wait mask (event notification) ───────────────────────────────────────
    SetWaitMask = 0x001B0060,   // IOCTL_SERIAL_SET_WAIT_MASK
    WaitOnMask = 0x001B005C,    // IOCTL_SERIAL_WAIT_ON_MASK

    // ── Signal lines ────────────────────────────────────────────────────────
    SetRts = 0x001B0030,        // IOCTL_SERIAL_SET_RTS
    ClrRts = 0x001B0034,        // IOCTL_SERIAL_CLR_RTS
    SetDtr = 0x001B0038,        // IOCTL_SERIAL_SET_DTR
    ClrDtr = 0x001B003C,        // IOCTL_SERIAL_CLR_DTR
    GetDtrRts = 0x001B0058,     // IOCTL_SERIAL_GET_DTRRTS
    GetModemStatus = 0x001B0070,// IOCTL_SERIAL_GET_MODEMSTATUS

    // ── Break ────────────────────────────────────────────────────────────────
    SetBreakOn = 0x001B0010,    // IOCTL_SERIAL_SET_BREAK_ON
    SetBreakOff = 0x001B0014,   // IOCTL_SERIAL_SET_BREAK_OFF

    // ── Buffer / queue ───────────────────────────────────────────────────────
    SetQueueSize = 0x001B0008,  // IOCTL_SERIAL_SET_QUEUE_SIZE
    Purge = 0x001B0064,         // IOCTL_SERIAL_PURGE

    // ── Status and properties ────────────────────────────────────────────────
    GetCommStatus = 0x001B004C, // IOCTL_SERIAL_GET_COMMSTATUS
    GetProperties = 0x001B0050, // IOCTL_SERIAL_GET_PROPERTIES

    // ── Special / escape characters ──────────────────────────────────────────
    SetChars = 0x001B0044,      // IOCTL_SERIAL_SET_CHARS
    GetChars = 0x001B0048,      // IOCTL_SERIAL_GET_CHARS
    ImmediateChar = 0x001B0018, // IOCTL_SERIAL_IMMEDIATE_CHAR
    XoffCounter = 0x001B0054,   // IOCTL_SERIAL_XOFF_COUNTER

    // ── Device control ───────────────────────────────────────────────────────
    ResetDevice = 0x001B0040,   // IOCTL_SERIAL_RESET_DEVICE
}
