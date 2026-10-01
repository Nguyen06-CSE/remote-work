namespace RemoteWork.Desktop.Core.Models.Activity;

public readonly record struct MouseClickSample(
    long TimestampMs,
    int X,
    int Y
);