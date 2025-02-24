using System.CommandLine;
using LibreToM4b.Commands;

var rootCommand = new RootCommand("LibreToM4b");
rootCommand.AddCommand(new ConversionCommand());
return rootCommand.Invoke(args);