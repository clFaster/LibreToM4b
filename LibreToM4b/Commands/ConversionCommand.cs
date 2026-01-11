using System.CommandLine;
using LibreToM4b.Core.Interfaces;
using LibreToM4b.Core.Models;
using LibreToM4b.Core.Services;
using LibreToM4b.Infrastructure;

namespace LibreToM4b.Commands;

public class ConversionCommand : Command
{
    public ConversionCommand()
        : base("convert", "Convert LibreOffice files to m4b")
    {
        var outputFolder = new Option<string?>("--output")
        {
            Description = "Output folder for the converted files",
        };
        outputFolder.Aliases.Add("-o");

        var inputFolder = new Argument<string>("input-folder")
        {
            Description = "Input folder containing LibreOffice files",
        };

        Add(outputFolder);
        Add(inputFolder);

        SetAction(
            async (parseResult, _) =>
            {
                IProgressReporter progressReporter = new ConsoleProgressReporter();
                IConversionService conversionService = new ConversionService(progressReporter);

                var options = new ConversionOptions
                {
                    InputDirectory = parseResult.GetValue(inputFolder)!,
                    OutputDirectory = parseResult.GetValue(outputFolder),
                };

                var result = await conversionService.ConvertAsync(options);
                if (result.IsFailed)
                {
                    await Console.Error.WriteLineAsync(result.Errors[0].Message);
                    return 1;
                }
                return 0;
            }
        );
    }
}
