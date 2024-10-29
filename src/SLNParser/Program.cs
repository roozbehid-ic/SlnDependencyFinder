//using Microsoft.Build.Locator;
//using DotNetProjectParser;
using RoozSoft.SlnDependencyFinder;
using System.Diagnostics;
using System.Reflection;

class Program
{

    static async Task Main(string[] args)
    {
        Stopwatch stp = Stopwatch.StartNew();
        if (args.Length < 2)
        {
            Console.WriteLine($"Usage: {Path.GetFileName(Assembly.GetEntryAssembly().Location)} <path-to-repo-> <path-to-sln-file>");
            return;
        }

        var id_path_filename = "";
        if (args.Length > 2 && !String.IsNullOrEmpty(args[2]))
        {
            id_path_filename = args[2];
        }
        var solutionFilePath = args[1];
        var repo = new GitRepository(args[0]);//new MonoRepo(args[0], id_path_filename);

        if (!repo.FileExists(solutionFilePath) || !args[1].EndsWith(".sln"))
        {
            Console.WriteLine("Please provide a valid solution file (.sln).");
            return;
        }

        var depFinder = new SolutionDependencyFinder(repo);
        var result = depFinder.GetDependencyList(solutionFilePath);

        Console.WriteLine("\nDependency Folders:");
        foreach (var folder in result)
        {
            Console.WriteLine(folder);
        }

        Console.WriteLine(stp.ElapsedMilliseconds);
    }



 
}
