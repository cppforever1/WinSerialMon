namespace WinSerialMon;

public sealed class SerialMonitorErrorEventArgs : EventArgs
{
    public SerialMonitorErrorEventArgs(Exception exception)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public Exception Exception { get; }
}
