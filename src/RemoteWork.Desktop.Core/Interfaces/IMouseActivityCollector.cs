using RemoteWork.Desktop.Core.Models.Activity;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface IMouseActivityCollector
{
    int Collect();

    IReadOnlyList<MouseClickSample> DrainSamples();
}