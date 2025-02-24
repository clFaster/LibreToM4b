// See https://aka.ms/new-console-template for more information

using System.CommandLine;

var rootCommand = new RootCommand("LibreToM4b");
rootCommand.AddCommand(new ConvertCommand());
return rootCommand.Invoke(args);