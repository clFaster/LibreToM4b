namespace LibreToM4b.Core.Models;

/// <summary>
/// Options for configuring the audiobook conversion process.
/// </summary>
public class ConversionOptions
{
    /// <summary>
    /// Gets or sets the input directory containing the audio files.
    /// </summary>
    public required string InputDirectory { get; init; }

    /// <summary>
    /// Gets or sets the output directory for the converted M4B file.
    /// If null, a default directory will be used.
    /// </summary>
    public string? OutputDirectory { get; init; }
}
