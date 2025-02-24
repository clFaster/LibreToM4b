using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Builders.MetaData;
using FFMpegCore.Enums;
using FFMpegCore.Helpers;
using FluentResults;
using LibreToM4b.BO;

namespace LibreToM4b.Services;

public static class ConversionService
{
    public static async Task<Result> Convert(string input, string? output)
    {
        try
        {
            FFMpegHelper.VerifyFFMpegExists(new FFOptions());
            
            // Output dir
            var outputDir = new DirectoryInfo(output is null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "output") : Path.GetFullPath(output));
            if (!outputDir.Exists)
            {
                outputDir.Create();
            }
            Console.WriteLine("Output folder:");
            Console.WriteLine(outputDir.FullName);
            
            // Input folder
            var inputFile = new DirectoryInfo(input);
            if (!inputFile.Exists)
            {
                return Result.Fail($"Input folder {input} does not exist.");
            }
            Console.WriteLine("Input folder:");
            Console.WriteLine(inputFile.FullName);
            
            // Get List of audio files in the input folder .mp3
            var audioFiles = inputFile.GetFiles("*.mp3");
            if (audioFiles.Length == 0)
            {
                return Result.Fail("No audio files found in the input folder.");
            }
            Console.WriteLine("Found {0} audio files in the input folder.", audioFiles.Length);
            
            // Get bitrate of first file
            var mediaAnalysis = await FFProbe.AnalyseAsync(audioFiles[0].FullName);
            Console.WriteLine("Bitrate detected: {0} kbps", mediaAnalysis.Format.BitRate / 1000);
            
            // Check if Metadata file found
            var metadataFile = inputFile.GetFiles("metadata/metadata.json").FirstOrDefault();
            var json = await metadataFile?.OpenText().ReadToEndAsync();
            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var book = JsonSerializer.Deserialize<Book>(json, serializerOptions);
            if (book is null)
            {
                Console.WriteLine("No metadata file found. Generating metadata from audio files.");
                book = new Book
                {
                    Title = inputFile.Name,
                    Description = new Description
                    {
                        Full = "No description"
                    },
                    Creators = [],
                    Chapters = [
                        new Chapter
                        {
                            Title = "Introduction",
                            Spine = 0,
                            Offset = 0
                        }
                    ]
                };
            }
            
            // TODO Handle Chapter Info
            
            // Concatenate FFMpegCore audio files and convert to m4b
            var concatInput = audioFiles.Select(f => f.FullName);
            
            // Total duration of all audio files
            var totalDuration = 
                audioFiles
                    .Select(f => FFProbe.AnalyseAsync(f.FullName).Result.Format.Duration)
                    .Aggregate(TimeSpan.Zero, (sum, next) => sum + next);

            // Progress bar
            const int barWidth = 30;
            void ProgressHandler(double progress)
            {
                var progressBlocks = (int)(progress / 100 * barWidth);
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"[{new string('#', progressBlocks)}{new string('-', barWidth - progressBlocks)}] {progress:0.0}% ");
            }

            var metaDataBuilder = new MetaDataBuilder();
            metaDataBuilder
                .WithAlbum(book.Title)
                .WithTitle(book.Title)
                .WithEntry("description", book.Description.Full)
                .WithArtists(book.Creators.FirstOrDefault(x => x.Role == "author")?.Name ?? "Unknown Author")
                .WithComposers(book.Creators.FirstOrDefault(x => x.Role == "narrator")?.Name ?? "Unknown Narrator")
                .WithGenres("Audiobook")
                .AddChapters(book.Chapters,
                    chapter =>
                    {
                        if (chapter.Spine < 0 || chapter.Spine >= book.Spine.Count)
                        {
                            return (TimeSpan.Zero, chapter.Title); // Default to zero time if invalid
                        }

                        var preSpineDuration = book.Spine[..chapter.Spine].Sum(s => s.Duration);
                        var time = TimeSpan.FromSeconds(preSpineDuration + chapter.Offset);

                        return (time, chapter.Title);
                    });
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
                    })
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

    private static string GenerateChapterMetadata(Book? book)
    {
        if (book?.Chapters == null || book.Chapters.Count == 0){
            return "00:00:00 Introduction";
        }

        StringBuilder metadata = new();

        foreach (var chapter in book.Chapters)
        {
            if (chapter.Spine < 0 || chapter.Spine >= book.Spine.Count)
            {
                continue;
            }
            
            var preSpineDuration = book.Spine[..chapter.Spine].Sum(s => s.Duration);

            // Convert cumulativeDuration (seconds) to HH:mm:ss format
            var time = TimeSpan.FromSeconds(preSpineDuration + chapter.Offset);
            var timestamp = time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

            // Append to metadata
            metadata.AppendLine($"{timestamp} {chapter.Title}");
        }

        return metadata.ToString().TrimEnd();
    }
}