using System.Diagnostics;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Helpers;
using FluentResults;

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
            // TODO Handle Chapter Info
            
            // Concatenate FFMpegCore audio files and convert to m4b
            var outputFileName = Path.Combine(outputDir.FullName, "output.m4b");
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
            
            // FFMpeg conversion
            var startingTimestamp = Stopwatch.GetTimestamp();
            await FFMpegArguments
                .FromConcatInput(concatInput)
                .OutputToFile(
                    outputFileName, 
                    true,
                    options =>
                    {
                        options
                            .WithAudioCodec(AudioCodec.Aac)
                            .WithAudioBitrate( (int)(mediaAnalysis.Format.BitRate / 1000) )
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
}