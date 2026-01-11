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
        var outputFolder = new Option<string?>(["--output", "-o"], "The output folder")
        {
            IsRequired = false,
        };

        var inputFolder = new Argument<string>("input-folder", "The input folder");

        AddOption(outputFolder);
        AddArgument(inputFolder);

        this.SetHandler(
            async (output, input) =>
            {
                IProgressReporter progressReporter = new ConsoleProgressReporter();
                IConversionService conversionService = new ConversionService(progressReporter);

                var options = new ConversionOptions
                {
                    InputDirectory = input,
                    OutputDirectory = output,
                };

                var result = await conversionService.ConvertAsync(options);
                if (result.IsFailed)
                {
                    Console.Error.WriteLine(result.Errors.First().Message);
                    Environment.Exit(1);
                }
            },
            outputFolder,
            inputFolder
        );
    }
}
