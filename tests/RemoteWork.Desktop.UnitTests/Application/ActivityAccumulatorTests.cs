using RemoteWork.Desktop.Core.Models.Activity;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Application;

public class ActivityAccumulatorTests
{
    [Fact]
    public void Accumulator_Should_Add_And_Reset_Counts_And_Durations()
    {
        var accumulator = new ActivityAccumulator();

        accumulator.AddKeyboard(5);
        accumulator.AddKeyboard(10);
        accumulator.AddMouse(3);
        accumulator.AddActiveDuration(TimeSpan.FromSeconds(20));
        accumulator.AddIdleDuration(TimeSpan.FromSeconds(10));

        Assert.Equal(15, accumulator.KeyboardCount);
        Assert.Equal(3, accumulator.MouseCount);
        Assert.Equal(TimeSpan.FromSeconds(20), accumulator.ActiveDuration);
        Assert.Equal(TimeSpan.FromSeconds(10), accumulator.IdleDuration);

        accumulator.Reset();

        Assert.Equal(0, accumulator.KeyboardCount);
        Assert.Equal(0, accumulator.MouseCount);
        Assert.Equal(TimeSpan.Zero, accumulator.ActiveDuration);
        Assert.Equal(TimeSpan.Zero, accumulator.IdleDuration);
    }
}
