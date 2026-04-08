# WinSerialMon

WinSerialMon is a .NET 10 class library that monitors serial-port I/O in real time using ETW (Event Tracing for Windows). It exposes strongly typed events and delegates for WinForms and any other .NET consumer.

## Features

- Real-time kernel ETW monitoring of serial device I/O.
- Full IRP major function coverage (`IrpMajorFunction` enum).
- Maps IRP major functions to high-level serial port actions:
  - Open, Close, Read, Write, Ioctl
- Per-action strongly typed delegates with rich data parameters:
  - Buffer bytes (read/write/ioctl input/output)
  - Requested and completed lengths
  - IOCTL control code and NT status
- General-purpose raw IRP event delegate (`SerialIrpEventReceived`).
- Interface-based callback registration (`ISerialIrpEventCallback`).
- Optional marshaling to `SynchronizationContext` for UI-safe WinForms callbacks.
- Configurable serial device path filtering.
- Structured NLog logging with two rotating file targets (main + trace).
- `autoReload` NLog config — change log levels without restarting.

## Requirements

- Windows
- .NET 10 SDK
- Administrator privileges required to start kernel ETW sessions

## Dependencies

| Package | Version |
|---|---|
| Microsoft.Diagnostics.Tracing.TraceEvent | 3.1.7 |
| NLog | 5.3.3 |

## Install and Build

    dotnet restore
    dotnet build

## Project Structure

| File | Purpose |
|---|---|
| `SerialEtwMonitor.cs` | Main ETW monitor — start/stop, event routing, dispatch |
| `SerialMonitorOptions.cs` | Session and callback configuration |
| `SerialIrpEvent.cs` | Raw normalized IRP event record |
| `IrpMajorFunction.cs` | All IRP_MJ_* major function codes |
| `SerialPortAction.cs` | High-level action enum (Open/Close/Read/Write/Ioctl) |
| `SerialPortActionEventArgs.cs` | Per-action event arguments with buffer and length fields |
| `SerialIoctlCode.cs` | Well-known serial IOCTL code enum for identifying configuration operations |
| `SerialLineControlSettings.cs` | Decoded line-control model (data bits, stop bits, parity) |
| `SerialHandFlowSettings.cs` | Decoded handshake/flow-control model |
| `SerialTimeoutSettings.cs` | Decoded read/write timeout model |
| `SerialModemStatus.cs` | Decoded modem-status register flags |
| `SerialCommConfigSettings.cs` | Decoded COMMCONFIG/DCB model (all serial settings in one payload) |
| `SerialParity.cs` | Parity enum used by decoded settings |
| `SerialStopBits.cs` | Stop-bit enum used by decoded settings |
| `SerialIrpEventArgs.cs` | General IRP event arguments |
| `SerialMonitorErrorEventArgs.cs` | Error event arguments |
| `SerialMonitorDelegates.cs` | All public delegate type declarations |
| `ISerialIrpEventCallback.cs` | Interface for registering callback objects |
| `NLog.config` | Rotating log configuration (auto-copied to output) |

## SerialMonitorOptions

| Property | Type | Default | Description |
|---|---|---|---|
| `SessionName` | `string` | `WinSerialMon-<guid>` | ETW session name |
| `BufferSizeMB` | `int` | `64` | ETW buffer size |
| `MinimumBuffers` | `int` | `32` | Hint for minimum ETW buffers |
| `MaximumBuffers` | `int` | `128` | Hint for maximum ETW buffers |
| `EnableKernelFileIoProvider` | `bool` | `true` | Enable Kernel FileIO/FileIOInit ETW keywords |
| `EnableKernelIoTraceProvider` | `bool` | `true` | Enable Microsoft-Windows-Kernel-IoTrace |
| `IncludeEventsWithoutKnownIrpMajor` | `bool` | `false` | Pass events where IRP major can't be determined |
| `CallbackSynchronizationContext` | `SynchronizationContext?` | `null` | Target context for UI-thread marshaling |
| `PostCallbacksToSynchronizationContext` | `bool` | `true` | Enable automatic context marshaling |
| `SerialPathFilter` | `Func<string?, bool>` | Matches COM/serial/usbser | Custom device path predicate |

## Events and Delegates

### Action-specific (recommended for WinForms)

| Event | Delegate | Raised when |
|---|---|---|
| `SerialPortOpened` | `SerialPortOpenEventHandler` | IRP_MJ_CREATE on a serial device |
| `SerialPortClosed` | `SerialPortCloseEventHandler` | IRP_MJ_CLOSE or IRP_MJ_CLEANUP |
| `SerialPortRead` | `SerialPortReadEventHandler` | IRP_MJ_READ |
| `SerialPortWritten` | `SerialPortWriteEventHandler` | IRP_MJ_WRITE |
| `SerialPortIoctl` | `SerialPortIoctlEventHandler` | IRP_MJ_DEVICE_CONTROL or IRP_MJ_INTERNAL_DEVICE_CONTROL |

### General

| Event | Delegate | Raised when |
|---|---|---|
| `SerialIrpEventReceived` | `SerialIrpEventReceivedEventHandler` | Any matched serial IRP event |
| `SerialMonitorError` | `SerialMonitorErrorEventHandler` | Internal or callback error |

### SerialPortActionEventArgs properties

| Property | Type | Notes |
|---|---|---|
| `Action` | `SerialPortAction` | Open / Close / Read / Write / Ioctl |
| `DevicePath` | `string?` | Device path from ETW payload |
| `ComPortName` | `string?` | COM port name extracted from `DevicePath` (e.g. `"COM1"`, `"COM3"`). `null` when path has no COM token (e.g. `\Device\Serial0` style). |
| `ProcessId` | `int` | Originating process |
| `ThreadId` | `int` | Originating thread |
| `IoControlCode` | `ulong?` | IOCTL code (Ioctl only) |
| `KnownIoctlCode` | `SerialIoctlCode` | Named IOCTL value (e.g. `SetBaudRate`, `SetLineControl`, `SetTimeouts`, `GetModemStatus`, `SetHandFlow`, `SetCommConfig`) |
| `NtStatus` | `uint?` | NT status code |
| `RequestedLength` | `int?` | Bytes requested |
| `CompletedLength` | `int?` | Bytes transferred |
| `ReadBuffer` | `byte[]?` | Data read (when available in payload) |
| `WriteBuffer` | `byte[]?` | Data written (when available in payload) |
| `IoctlInputBuffer` | `byte[]?` | IOCTL input buffer (when available) |
| `IoctlOutputBuffer` | `byte[]?` | IOCTL output buffer (when available) |
| `NewBaudRate` | `uint?` | Decoded baud rate from `SetBaudRate` (and from `SetCommConfig`) |
| `NewLineControl` | `SerialLineControlSettings?` | Decoded data bits, stop bits, parity from `SetLineControl` (and from `SetCommConfig`) |
| `NewTimeouts` | `SerialTimeoutSettings?` | Decoded read/write timeouts from `SetTimeouts` (and `GetTimeouts` output when available) |
| `ModemStatus` | `SerialModemStatus?` | Decoded modem-status register flags from `GetModemStatus` |
| `NewHandFlow` | `SerialHandFlowSettings?` | Decoded handshake / flow-control flags from `SetHandFlow` |
| `NewCommConfig` | `SerialCommConfigSettings?` | Full decoded COMMCONFIG/DCB from `SetCommConfig` |
| `IrpEvent` | `SerialIrpEvent` | Full raw IRP event record |

### Decoded IOCTL parameter example

    _monitor.SerialPortIoctl += (sender, e) =>
    {
        switch (e.KnownIoctlCode)
        {
            case SerialIoctlCode.SetBaudRate:
                // e.NewBaudRate is populated
                Console.WriteLine($"{e.ComPortName} baud -> {e.NewBaudRate}");
                break;

            case SerialIoctlCode.SetLineControl:
                // e.NewLineControl has DataBits / StopBits / Parity
                Console.WriteLine($"{e.ComPortName} line -> {e.NewLineControl?.DataBits},{e.NewLineControl?.StopBits},{e.NewLineControl?.Parity}");
                break;

            case SerialIoctlCode.SetTimeouts:
                // e.NewTimeouts has the SERIAL_TIMEOUTS fields
                Console.WriteLine($"{e.ComPortName} timeouts -> readConst:{e.NewTimeouts?.ReadTotalTimeoutConstant} writeConst:{e.NewTimeouts?.WriteTotalTimeoutConstant}");
                break;

            case SerialIoctlCode.GetModemStatus:
                // e.ModemStatus has decoded CTS/DSR/RI/DCD bits
                Console.WriteLine($"{e.ComPortName} modem -> CTS:{e.ModemStatus?.IsClearToSend} DSR:{e.ModemStatus?.IsDataSetReady} DCD:{e.ModemStatus?.IsDataCarrierDetect}");
                break;

            case SerialIoctlCode.SetHandFlow:
                // e.NewHandFlow has CTS/RTS/XON-XOFF flags
                Console.WriteLine($"{e.ComPortName} handflow -> CTS:{e.NewHandFlow?.IsCtsHandshake} RTS:{e.NewHandFlow?.IsRtsHandshake}");
                break;

            case SerialIoctlCode.SetCommConfig:
                // e.NewCommConfig carries all settings at once
                Console.WriteLine($"{e.ComPortName} dcb -> baud:{e.NewCommConfig?.BaudRate} data:{e.NewCommConfig?.DataBits}");
                break;
        }
    };

## Logging

WinSerialMon uses NLog. `NLog.config` is copied to the output directory automatically.

### Log targets

| Target | File | Roll trigger | Max archives |
|---|---|---|---|
| `rollingFile` | `logs/WinSerialMon.log` | 10 MB or daily | 30 |
| `traceFile` | `logs/WinSerialMon.trace.log` | 50 MB or daily | 10 |
| `console` | stdout | — | — |

### Log level routing

| Level | Destination |
|---|---|
| `Trace` | `traceFile` only — one entry per ETW event, filter step, and dispatch decision |
| `Debug` | `rollingFile` + `console` — provider enables, callback registration |
| `Info` | `rollingFile` + `console` — session start/stop/dispose |
| `Warn` | `rollingFile` + `console` — callback handler exceptions |
| `Error` | `rollingFile` + `console` — unhandled ETW stream exceptions |

The config has `autoReload="true"` — edit `NLog.config` at runtime to change levels without restarting.

To override logging from the host application, call `LogManager.Setup()` before creating a `SerialEtwMonitor` instance.

## WinForms Example

    using System;
    using System.Text;
    using System.Threading;
    using System.Windows.Forms;
    using WinSerialMon;

    public partial class MainForm : Form
    {
        private SerialEtwMonitor? _monitor;

        public MainForm()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            var options = new SerialMonitorOptions
            {
                CallbackSynchronizationContext = SynchronizationContext.Current,
                PostCallbacksToSynchronizationContext = true
            };

            _monitor = new SerialEtwMonitor(options);

            _monitor.SerialPortOpened  += OnSerialOpened;
            _monitor.SerialPortClosed  += OnSerialClosed;
            _monitor.SerialPortRead    += OnSerialRead;
            _monitor.SerialPortWritten += OnSerialWrite;
            _monitor.SerialPortIoctl   += OnSerialIoctl;
            _monitor.SerialMonitorError += OnMonitorError;

            _monitor.Start();
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            if (_monitor is not null)
            {
                await _monitor.StopAsync();
                _monitor.Dispose();
            }

            base.OnFormClosing(e);
        }

        private void OnSerialOpened(object? sender, SerialPortActionEventArgs e)
            => AppendLine($"OPEN  {e.DevicePath}  pid={e.ProcessId}");

        private void OnSerialClosed(object? sender, SerialPortActionEventArgs e)
            => AppendLine($"CLOSE {e.DevicePath}  pid={e.ProcessId}");

        private void OnSerialRead(object? sender, SerialPortActionEventArgs e)
            => AppendLine($"READ  {e.DevicePath}  len={e.CompletedLength}  data={ToHex(e.ReadBuffer)}");

        private void OnSerialWrite(object? sender, SerialPortActionEventArgs e)
            => AppendLine($"WRITE {e.DevicePath}  len={e.CompletedLength}  data={ToHex(e.WriteBuffer)}");

        private void OnSerialIoctl(object? sender, SerialPortActionEventArgs e)
            => AppendLine($"IOCTL {e.DevicePath}  code=0x{e.IoControlCode:X}  in={ToHex(e.IoctlInputBuffer)}  out={ToHex(e.IoctlOutputBuffer)}");

        private void OnMonitorError(object? sender, SerialMonitorErrorEventArgs e)
            => AppendLine($"ERROR {e.Exception.Message}");

        private void AppendLine(string text) => listBoxEvents.Items.Add(text);

        private static string ToHex(byte[]? data)
        {
            if (data is null || data.Length == 0) return "<none>";
            var sb = new StringBuilder(data.Length * 3);
            for (var i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }

## Notes on ETW Payload Data

ETW payload structure varies by provider, Windows build, and event shape.

- Metadata fields (device path, IRP address, status, lengths) are always attempted.
- Buffer fields (`ReadBuffer`, `WriteBuffer`, `IoctlInputBuffer`, `IoctlOutputBuffer`) are parsed from payload when available; they may be `null`.
- Buffer payloads may arrive as raw `byte[]`, `Memory<byte>`, hex strings, or base64 strings — all handled automatically.

## Troubleshooting

- **Access denied when starting monitor** — run the app as Administrator.
- **No serial events observed** — ensure serial traffic is active; verify `SerialPathFilter` matches your device path.
- **UI cross-thread exceptions in WinForms** — set `CallbackSynchronizationContext = SynchronizationContext.Current` before calling `Start()`.
- **Log files not created** — ensure the process has write access to the output directory.
- **Too much trace output** — change `minlevel="Trace"` to `minlevel="Debug"` for the `traceFile` rule in `NLog.config`; the file reloads automatically.
