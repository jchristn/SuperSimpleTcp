using Test.Shared;
using Touchstone.Cli;

string? resultsPath = args.Length > 0 ? args[0] : null;
return await ConsoleRunner.RunAsync(SuperSimpleTcpTestSuites.All, resultsPath: resultsPath);
