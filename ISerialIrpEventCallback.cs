namespace WinSerialMon;

public interface ISerialIrpEventCallback
{
    void OnIrpEvent(SerialIrpEvent irpEvent);
    void OnMonitorError(Exception exception);
}
