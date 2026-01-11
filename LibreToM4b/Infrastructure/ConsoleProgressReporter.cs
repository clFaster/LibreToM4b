using LibreToM4b.Core.Interfaces;

namespace LibreToM4b.Infrastructure;

/// <summary>
/// Console-based progress reporter for CLI applications.
/// </summary>
public class ConsoleProgressReporter : IProgressReporter
{
    private const int BarWidth = 30;

    /// <inheritdoc />
    public void ReportProgress(double progressPercentage)
    {
        var progressBlocks = (int)(progressPercentage / 100 * BarWidth);
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(
            $"[{new string('#', progressBlocks)}{new string('-', BarWidth - progressBlocks)}] {progressPercentage:0.0}% "
        );
    }

    /// <inheritdoc />
    public void LogInfo(string message)
    {
        Console.WriteLine(message);
    }

    /// <inheritdoc />
    public void LogError(string message)
    {
        Console.Error.WriteLine(message);
    }
}
