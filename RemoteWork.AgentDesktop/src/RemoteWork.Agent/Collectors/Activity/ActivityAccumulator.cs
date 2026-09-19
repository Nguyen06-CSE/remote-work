namespace RemoteWork.Agent.Core.Models.Activity;

public sealed class ActivityAccumulator
{
    public int KeyboardCount { get; private set; }

    public int MouseCount { get; private set; }

    public TimeSpan ActiveDuration { get; private set; }

    public TimeSpan IdleDuration { get; private set; }

    public void AddKeyboard(int count)
    {
        KeyboardCount += count;
    }

    public void AddMouse(int count)
    {
        MouseCount += count;
    }

    public void AddActiveDuration(TimeSpan duration)
    {
        ActiveDuration += duration;
    }

    public void AddIdleDuration(TimeSpan duration)
    {
        IdleDuration += duration;
    }

    public void Reset()
    {
        KeyboardCount = 0;
        MouseCount = 0;
        ActiveDuration = TimeSpan.Zero;
        IdleDuration = TimeSpan.Zero;
    }
}