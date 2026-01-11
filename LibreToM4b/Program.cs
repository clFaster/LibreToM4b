using System.CommandLine;
using LibreToM4b.Commands;

var rootCommand = new RootCommand("LibreToM4b");
rootCommand.Subcommands.Add(new ConversionCommand());
return await rootCommand.Parse(args).InvokeAsync();
