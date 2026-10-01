using RemoteWork.Desktop.Core.Models.Activity;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface IMouseBotDetector
{
    BotDetectionResult Analyze(IReadOnlyList<MouseClickSample> samples);
}