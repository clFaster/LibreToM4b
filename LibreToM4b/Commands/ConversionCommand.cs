using System.CommandLine;
using LibreToM4b.Services;

namespace LibreToM4b.Commands;

public class ConversionCommand : Command
{
    public ConversionCommand() : base("convert", "Convert LibreOffice files to m4b")
    {
        var outputFolder = new Option<string?>(
            ["--output", "-o"], 
            "The output folder") { IsRequired = false };
        
        var inputFolder = new Argument<string>(
            "input-folder", 
            "The input folder");
        
        AddOption(outputFolder);
        AddArgument(inputFolder);
        
        this.SetHandler(async (output, input) =>
        {
            var r = await ConversionService.Convert(input, output);
            if (r.IsFailed)
            {
                Console.Error.WriteLine(r.Errors.First().Message);
                Environment.Exit(1);
            }
        }, outputFolder, inputFolder);
    }
}