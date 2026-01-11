using FluentResults;
using LibreToM4b.Core.Models;

namespace LibreToM4b.Core.Interfaces;

/// <summary>
/// Interface for the audiobook conversion service.
/// </summary>
public interface IConversionService
{
    /// <summary>
    /// Converts audio files from the specified input directory to an M4B audiobook.
    /// </summary>
    /// <param name="options">The conversion options.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ConvertAsync(
        ConversionOptions options,
        CancellationToken cancellationToken = default
    );
}
