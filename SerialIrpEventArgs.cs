namespace WinSerialMon;

public sealed class SerialIrpEventArgs : EventArgs
{
    public SerialIrpEventArgs(SerialIrpEvent irpEvent)
    {
        IrpEvent = irpEvent ?? throw new ArgumentNullException(nameof(irpEvent));
    }

    public SerialIrpEvent IrpEvent { get; }
}
