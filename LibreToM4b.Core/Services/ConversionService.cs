using System.Diagnostics;
using System.Text.Json;
using FFMpegCore;
using FFMpegCore.Builders.MetaData;
using FFMpegCore.Enums;
using FFMpegCore.Helpers;
using FluentResults;
using LibreToM4b.Core.Interfaces;
using LibreToM4b.Core.Models;

namespace LibreToM4b.Core.Services;

/// <summary>
/// Service for converting audiobook files to M4B format.
/// </summary>
public class ConversionService : IConversionService
{
    private static readonly string[] SupportedAudioExtensions =
    [
        ".mp3",
        ".m4a",
        ".aac",
        ".flac",
        ".wav",
        ".ogg",
        ".opus",
        ".wma",
        ".ts",
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IProgressReporter _progressReporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionService"/> class.
    /// </summary>
    /// <param name="progressReporter">The progress reporter for logging and progress updates.</param>
    public ConversionService(IProgressReporter progressReporter)
    {
        _progressReporter = progressReporter;
    }

    /// <inheritdoc />
    public async Task<Result> ConvertAsync(
        ConversionOptions options,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            FFMpegHelper.VerifyFFMpegExists(new FFOptions());

            var outputDir = PrepareOutputDir(options.OutputDirectory);
            var inputDirectoryResult = ValidateInputDirectory(options.InputDirectory);
            if (inputDirectoryResult.IsFailed)
            {
                return Result.Fail(inputDirectoryResult.Errors);
            }

            var inputDirectory = inputDirectoryResult.Value;
            var audioFiles = GetAudioFiles(inputDirectory);
            if (audioFiles.Length == 0)
            {
                return Result.Fail("No audio files found in the input folder.");
            }

            _progressReporter.LogInfo(
                $"Found {audioFiles.Length} audio files in the input folder."
            );

            var mediaAnalysis = await FFProbe.AnalyseAsync(
                audioFiles[0].FullName,
                cancellationToken: cancellationToken
            );
            var audioBitrateKbps = ResolveAudioBitrateKbps(mediaAnalysis);
            _progressReporter.LogInfo(
                $"Audio bitrate detected: {audioBitrateKbps} kbps"
            );

            var totalDuration = await CalculateTotalDurationAsync(audioFiles, cancellationToken);
            var book = await LoadOrGenerateBookMetadataAsync(
                inputDirectory,
                audioFiles,
                mediaAnalysis.Format.Tags,
                totalDuration,
                cancellationToken
            );

            var outputFileName = Path.Combine(
                outputDir.FullName,
                $"{SanitizeFileName(book.Title)}.m4b"
            );
            var startingTimestamp = Stopwatch.GetTimestamp();

            await ConvertToM4BAsync(
                audioFiles,
                book,
                audioBitrateKbps,
                outputFileName,
                totalDuration,
                cancellationToken
            );

            var elapsed = Stopwatch.GetElapsedTime(startingTimestamp);
            _progressReporter.LogInfo(
                $"Conversion completed in {elapsed.TotalSeconds:F1} seconds."
            );
            _progressReporter.LogInfo($"Output: {outputFileName}");

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("Conversion was cancelled.");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    private Result<DirectoryInfo> ValidateInputDirectory(string inputDirectoryPath)
    {
        var inputDirectory = new DirectoryInfo(inputDirectoryPath);
        if (!inputDirectory.Exists)
        {
            return Result.Fail($"Input folder {inputDirectoryPath} does not exist.");
        }

        _progressReporter.LogInfo($"Input folder: {inputDirectory.FullName}");
        return inputDirectory;
    }

    private DirectoryInfo PrepareOutputDir(string? outputDirectoryPath)
    {
        var outputDir = new DirectoryInfo(
            outputDirectoryPath is null
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    "LibreToM4b"
                )
                : Path.GetFullPath(outputDirectoryPath)
        );

        if (!outputDir.Exists)
        {
            outputDir.Create();
        }

        _progressReporter.LogInfo($"Output folder: {outputDir.FullName}");
        return outputDir;
    }

    private static FileInfo[] GetAudioFiles(DirectoryInfo inputDirectory)
    {
        return inputDirectory
            .GetFiles()
            .Where(file =>
                SupportedAudioExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase)
            )
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<TimeSpan> CalculateTotalDurationAsync(
        FileInfo[] audioFiles,
        CancellationToken cancellationToken
    )
    {
        var totalDuration = TimeSpan.Zero;
        foreach (var file in audioFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var analysis = await FFProbe.AnalyseAsync(
                file.FullName,
                cancellationToken: cancellationToken
            );
            totalDuration += analysis.Format.Duration;
        }
        return totalDuration;
    }

    private async Task<LibreBook> LoadOrGenerateBookMetadataAsync(
        DirectoryInfo inputDirectory,
        FileInfo[] audioFiles,
        Dictionary<string, string>? tags,
        TimeSpan totalDuration,
        CancellationToken cancellationToken
    )
    {
        var metadataFile = TryGetMetadataFile(inputDirectory);

        if (metadataFile is not null)
        {
            var book = await LoadBookFromMetadataAsync(metadataFile, cancellationToken);
            if (book is not null)
            {
                CalculateChapterDurations(book, totalDuration);
                return book;
            }
        }

        _progressReporter.LogInfo("Generating metadata from audio files.");
        return await GenerateBookFromAudioFilesAsync(audioFiles, tags, cancellationToken);
    }

    private static FileInfo? TryGetMetadataFile(DirectoryInfo inputDirectory)
    {
        try
        {
            return inputDirectory.GetFiles("metadata/metadata.json").FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<LibreBook?> LoadBookFromMetadataAsync(
        FileInfo metadataFile,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var reader = metadataFile.OpenText();
            var json = await reader.ReadToEndAsync(cancellationToken);
            return JsonSerializer.Deserialize<LibreBook>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<LibreBook> GenerateBookFromAudioFilesAsync(
        FileInfo[] audioFiles,
        Dictionary<string, string>? tags,
        CancellationToken cancellationToken
    )
    {
        var chapters = new List<LibreChapter>();
        foreach (var file in audioFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var analysis = await FFProbe.AnalyseAsync(
                file.FullName,
                cancellationToken: cancellationToken
            );
            chapters.Add(
                new LibreChapter
                {
                    Title =
                        tags?.TryGetValue("title", out var title) == true
                            ? title
                            : Path.GetFileNameWithoutExtension(file.Name),
                    Duration = analysis.Duration,
                    Spine = 0,
                    Offset = 0,
                }
            );
        }

        return new LibreBook
        {
            Title = tags?.TryGetValue("album", out var album) == true ? album : "Unknown Audiobook",
            Description = new Description
            {
                Full =
                    tags?.TryGetValue("comment", out var comment) == true ? comment : string.Empty,
                Short =
                    tags?.TryGetValue("comment", out var shortComment) == true
                        ? shortComment
                        : string.Empty,
            },
            CoverUrl = string.Empty,
            Creators =
            [
                new Creator
                {
                    Name =
                        tags?.TryGetValue("artist", out var artist) == true
                            ? artist
                            : "Unknown Author",
                    Role = "author",
                },
                new Creator
                {
                    Name =
                        tags?.TryGetValue("composer", out var composer) == true
                            ? composer
                            : (
                                tags?.TryGetValue("album_artist", out var albumArtist) == true
                                    ? albumArtist
                                    : "Unknown Narrator"
                            ),
                    Role = "narrator",
                },
            ],
            Spine = [],
            Chapters = chapters,
        };
    }

    private static void CalculateChapterDurations(LibreBook libreBook, TimeSpan totalDuration)
    {
        for (var i = 0; i < libreBook.Chapters.Count; i++)
        {
            var chapter = libreBook.Chapters[i];
            var nextChapter = i + 1 < libreBook.Chapters.Count ? libreBook.Chapters[i + 1] : null;

            var chapterStart =
                libreBook.Spine.Take(chapter.Spine).Sum(s => s.Duration) + chapter.Offset;
            var chapterEnd = nextChapter is not null
                ? libreBook.Spine.Take(nextChapter.Spine).Sum(s => s.Duration) + nextChapter.Offset
                : totalDuration.TotalSeconds;

            chapter.Duration = TimeSpan.FromSeconds(chapterEnd - chapterStart);
        }
    }

    private async Task ConvertToM4BAsync(
        FileInfo[] audioFiles,
        LibreBook libreBook,
        int audioBitrateKbps,
        string outputFileName,
        TimeSpan totalDuration,
        CancellationToken cancellationToken
    )
    {
        var concatInput = audioFiles.Select(f => f.FullName);

        var metaDataBuilder = new MetaDataBuilder();
        metaDataBuilder
            .WithAlbum(libreBook.Title)
            .WithTitle(libreBook.Title)
            .WithEntry("description", libreBook.Description.Full)
            .WithArtists(
                libreBook.Creators.FirstOrDefault(x => x.Role == "author")?.Name ?? "Unknown Author"
            )
            .WithComposers(
                libreBook.Creators.FirstOrDefault(x => x.Role == "narrator")?.Name
                    ?? "Unknown Narrator"
            )
            .WithGenres("Audiobook")
            .AddChapters(libreBook.Chapters, chapter => (chapter.Duration, chapter.Title));

        var readOnlyMetaData = metaDataBuilder.Build();

        await FFMpegArguments
            .FromConcatInput(concatInput)
            .AddMetaData(readOnlyMetaData)
            .OutputToFile(
                outputFileName,
                true,
                options =>
                {
                    options
                        .WithAudioCodec(AudioCodec.Aac)
                        .WithAudioBitrate(audioBitrateKbps)
                        .WithFastStart();
                }
            )
            .NotifyOnProgress(progress => _progressReporter.ReportProgress(progress), totalDuration)
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously();
    }

    private static int ResolveAudioBitrateKbps(IMediaAnalysis mediaAnalysis)
    {
        var primaryAudioBitrate = mediaAnalysis.PrimaryAudioStream?.BitRate ?? 0;
        if (primaryAudioBitrate > 0)
        {
            return (int)Math.Round(primaryAudioBitrate / 1000d, MidpointRounding.AwayFromZero);
        }

        var firstAudioBitrate = mediaAnalysis.AudioStreams
            .Select(stream => stream.BitRate)
            .FirstOrDefault(bitRate => bitRate > 0);
        if (firstAudioBitrate > 0)
        {
            return (int)Math.Round(firstAudioBitrate / 1000d, MidpointRounding.AwayFromZero);
        }

        if (mediaAnalysis.Format.BitRate > 0)
        {
            return (int)Math.Round(mediaAnalysis.Format.BitRate / 1000d, MidpointRounding.AwayFromZero);
        }

        return 128;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join(
            "_",
            fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)
        );
    }
}
