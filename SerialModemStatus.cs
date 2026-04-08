namespace WinSerialMon;

/// <summary>
/// Decoded modem status register value returned by IOCTL_SERIAL_GET_MODEMSTATUS.
/// Binary layout: ULONG containing the UART modem-status register bits.
/// </summary>
public sealed record SerialModemStatus(uint RawValue)
{
    private const uint DeltaClearToSend = 0x01;
    private const uint DeltaDataSetReady = 0x02;
    private const uint TrailingEdgeRingIndicator = 0x04;
    private const uint DeltaDataCarrierDetect = 0x08;
    private const uint ClearToSend = 0x10;
    private const uint DataSetReady = 0x20;
    private const uint RingIndicator = 0x40;
    private const uint DataCarrierDetect = 0x80;

    public bool IsDeltaClearToSend => (RawValue & DeltaClearToSend) != 0;

    public bool IsDeltaDataSetReady => (RawValue & DeltaDataSetReady) != 0;

    public bool IsTrailingEdgeRingIndicator => (RawValue & TrailingEdgeRingIndicator) != 0;

    public bool IsDeltaDataCarrierDetect => (RawValue & DeltaDataCarrierDetect) != 0;

    public bool IsClearToSend => (RawValue & ClearToSend) != 0;

    public bool IsDataSetReady => (RawValue & DataSetReady) != 0;

    public bool IsRingIndicator => (RawValue & RingIndicator) != 0;

    public bool IsDataCarrierDetect => (RawValue & DataCarrierDetect) != 0;

    public static SerialModemStatus? TryDecode(byte[]? buffer)
    {
        if (buffer is null || buffer.Length < 4)
        {
            return null;
        }

        return new SerialModemStatus(BitConverter.ToUInt32(buffer, 0));
    }
}