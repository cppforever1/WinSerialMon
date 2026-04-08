namespace WinSerialMon;

public delegate void SerialIrpEventReceivedEventHandler(object? sender, SerialIrpEventArgs e);
public delegate void SerialMonitorErrorEventHandler(object? sender, SerialMonitorErrorEventArgs e);
public delegate void SerialPortOpenEventHandler(object? sender, SerialPortActionEventArgs e);
public delegate void SerialPortCloseEventHandler(object? sender, SerialPortActionEventArgs e);
public delegate void SerialPortReadEventHandler(object? sender, SerialPortActionEventArgs e);
public delegate void SerialPortWriteEventHandler(object? sender, SerialPortActionEventArgs e);
public delegate void SerialPortIoctlEventHandler(object? sender, SerialPortActionEventArgs e);
