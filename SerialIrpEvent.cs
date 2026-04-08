namespace WinSerialMon;

public sealed record SerialIrpEvent(
    DateTime TimestampUtc,
    int ProcessId,
    int ThreadId,
    string ProviderName,
    string EventName,
    IrpMajorFunction MajorFunction,
    string? DevicePath,
    string? IrpAddress,
    ulong? IoControlCode,
    uint? NtStatus,
    ulong? Information,
    IReadOnlyDictionary<string, object?> Payload);
