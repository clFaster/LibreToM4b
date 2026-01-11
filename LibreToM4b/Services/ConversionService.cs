using System.Diagnostics;
using System.Text.Json;
using FFMpegCore;
using FFMpegCore.Builders.MetaData;
using FFMpegCore.Enums;
using FFMpegCore.Helpers;
using FluentResults;
using LibreToM4b.BO;

namespace LibreToM4b.Services;

public static class ConversionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<Result> Convert(string inputDirectoryStr, string? outputDirectoryStr)
    {
        try
        {
            FFMpegHelper.VerifyFFMpegExists(new FFOptions());

            // Output dir
            var outputDir = PrepareOutputDir(outputDirectoryStr);

            // Input folder
            var inputDirectoryResult = CheckInputDir(inputDirectoryStr);
            if (inputDirectoryResult.IsFailed)
            {
                return Result.Fail(inputDirectoryResult.Errors);
                ;
            }
            var inputDirectory = inputDirectoryResult.Value;

            // Get List of audio files in the input folder .mp3
            var audioFiles = inputDirectory.GetFiles("*.mp3");
            if (audioFiles.Length == 0)
            {
                return Result.Fail("No audio files found in the input folder.");
            }
            Console.WriteLine("Found {0} audio files in the input folder.", audioFiles.Length);

            // Get bitrate of first file
            var mediaAnalysis = await FFProbe.AnalyseAsync(audioFiles[0].FullName);
            Console.WriteLine("Bitrate detected: {0} kbps", mediaAnalysis.Format.BitRate / 1000);

            // Check if Metadata file found
            // Do not fail if metadata file is not found
            FileInfo? metadataFile = null;
            try
            {
                metadataFile = inputDirectory.GetFiles("metadata/metadata.json").FirstOrDefault();
            }
            catch (Exception e)
            {
                Console.WriteLine("Metadata file not found.");
            }

            var totalDuration = audioFiles
                .Select(f => FFProbe.AnalyseAsync(f.FullName).Result.Format.Duration)
                .Aggregate(TimeSpan.Zero, (sum, next) => sum + next);

            Book? book = null;
            if (metadataFile is not null)
            {
                var json = await metadataFile.OpenText().ReadToEndAsync();
                book = JsonSerializer.Deserialize<Book>(json, SerializerOptions);
                if (book is not null)
                {
                    CalculateChapterDuration(book, totalDuration);
                }
            }

            if (book is null)
            {
                Console.WriteLine("Generating metadata from audio files.");
                book = GenerateBookFromAudioFiles(
                    audioFiles,
                    mediaAnalysis.Format.Tags,
                    totalDuration
                );
            }

            // Concatenate FFMpegCore audio files and convert to m4b
            var concatInput = audioFiles.Select(f => f.FullName);

            // Total duration of all audio files

            // Progress bar
            const int barWidth = 30;
            void ProgressHandler(double progress)
            {
                var progressBlocks = (int)(progress / 100 * barWidth);
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(
                    $"[{new string('#', progressBlocks)}{new string('-', barWidth - progressBlocks)}] {progress:0.0}% "
                );
            }

            var metaDataBuilder = new MetaDataBuilder();
            metaDataBuilder
                .WithAlbum(book.Title)
                .WithTitle(book.Title)
                .WithEntry("description", book.Description.Full)
                .WithArtists(
                    book.Creators.FirstOrDefault(x => x.Role == "author")?.Name ?? "Unknown Author"
                )
                .WithComposers(
                    book.Creators.FirstOrDefault(x => x.Role == "narrator")?.Name
                        ?? "Unknown Narrator"
                )
                .WithGenres("Audiobook")
                .AddChapters(book.Chapters, chapter => (chapter.Duration, chapter.Title));
            var readOnlyMetaData = metaDataBuilder.Build();

            // FFMpeg conversion
            var outputFileName = Path.Combine(outputDir.FullName, $"{book.Title}.m4b");
            var startingTimestamp = Stopwatch.GetTimestamp();
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
                            .WithAudioBitrate((int)(mediaAnalysis.Format.BitRate / 1000))
                            .WithFastStart();
                    }
                )
                .NotifyOnProgress(ProgressHandler, totalDuration)
                .ProcessAsynchronously();
            var elapsed = Stopwatch.GetElapsedTime(startingTimestamp);
            Console.WriteLine("Conversion took {0} seconds.", elapsed);
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }

        return Result.Ok();
    }

    private static Book GenerateBookFromAudioFiles(
        FileInfo[] audioFiles,
        Dictionary<string, string>? tags,
        TimeSpan totalDuration
    )
    {
        var book = new Book
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
            Spine = audioFiles
                .Select(f => new Spine { Duration = totalDuration.TotalSeconds })
                .ToList(),
            Chapters = audioFiles
                .Select(
                    (f, i) =>
                        new Chapter
                        {
                            Title =
                                tags?.TryGetValue("title", out var title) == true ? title : f.Name,
                            Duration = FFProbe.Analyse(f.FullName).Duration,
                            Spine = 0,
                            Offset = 0,
                        }
                )
                .ToList(),
        };

        return book;
    }

    private static Result<DirectoryInfo> CheckInputDir(string inputDirectoryStr)
    {
        var inputDirectory = new DirectoryInfo(inputDirectoryStr);
        if (!inputDirectory.Exists)
        {
            return Result.Fail($"Input folder {inputDirectoryStr} does not exist.");
        }
        Console.WriteLine("Input folder:");
        Console.WriteLine(inputDirectory.FullName);
        return inputDirectory;
    }

    private static DirectoryInfo PrepareOutputDir(string? outputDirectoryStr)
    {
        var outputDir = new DirectoryInfo(
            outputDirectoryStr is null
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    "LibreToM4b"
                )
                : Path.GetFullPath(outputDirectoryStr)
        );
        if (!outputDir.Exists)
        {
            outputDir.Create();
        }
        Console.WriteLine("Output folder:");
        Console.WriteLine(outputDir.FullName);
        return outputDir;
    }

    private static void CalculateChapterDuration(Book book, TimeSpan totalDuration)
    {
        for (var i = 0; i < book.Chapters.Count; i++)
        {
            var chapter = book.Chapters[i];
            var nextChapter = i + 1 < book.Chapters.Count ? book.Chapters[i + 1] : null;

            var chapterStart = book.Spine[..chapter.Spine].Sum(s => s.Duration) + chapter.Offset;
            var chapterEnd = nextChapter is not null
                ? book.Spine[..nextChapter.Spine].Sum(s => s.Duration) + nextChapter.Offset
                : totalDuration.TotalSeconds;
            chapter.Duration = TimeSpan.FromSeconds(chapterEnd - chapterStart);
        }
    }
}
