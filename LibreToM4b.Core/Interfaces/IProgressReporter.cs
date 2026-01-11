namespace LibreToM4b.Core.Interfaces;

/// <summary>
/// Interface for reporting conversion progress.
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Reports the current progress of the conversion.
    /// </summary>
    /// <param name="progressPercentage">The progress percentage (0-100).</param>
    void ReportProgress(double progressPercentage);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void LogInfo(string message);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    void LogError(string message);
}
