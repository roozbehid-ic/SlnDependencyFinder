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
        if (args.Length < 3)
        {
            Console.WriteLine($"Usage: {Path.GetFileName(Assembly.GetEntryAssembly().Location)} <internal|external> <path-to-repo-> <path-to-sln-file> <optional:id_path_filename>");
            Console.WriteLine("internal uses libgit to discover files.");
            Console.WriteLine("external uses git binary process to discover files.");
            return;
        }

        var id_path_filename = "";
        if (args.Length > 3 && !String.IsNullOrEmpty(args[3]))
        {
            id_path_filename = args[3];
        }
        var solutionFilePath = args[2];

        if (!Directory.Exists(Path.Combine(args[1],".git")))
        {
            Console.WriteLine("Your repository directory should be the root git folder.");
            return;
        }
        IMonoRepository repo;
        if (args[0] == "external")
            repo = new ExternalGitRepository(args[1]);//new MonoRepo(args[0], id_path_filename);
        else if (args[0] == "internal")
            repo = new InternalGitRepository(args[1], id_path_filename);
        else
        {
            Console.WriteLine("valid parameters are either internal or external.");
            return;
        }

        if (!repo.FileExists(solutionFilePath) || !args[2].EndsWith(".sln"))
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

        Console.WriteLine();
        Console.WriteLine($"elapsed time : {stp.ElapsedMilliseconds}ms");
    }



 
}
