using RoozSoft.SolutionDependencyAnalyzer;
using System.Diagnostics;
using System.Reflection;

class Program
{
    static async Task Main(string[] args)
    {
        Stopwatch stp = Stopwatch.StartNew();
       

        if (args.Length < 3)
        {
            Console.WriteLine();
            Console.WriteLine($"Usage: {Path.GetFileName(Assembly.GetEntryAssembly().Location)} <internal|external|system> <path-to-repo-> <path-to-sln-file> <optional:id_path_filename>");
            Console.WriteLine("internal uses libgit to discover files. Fastest if you provide an id_path_filename to it.");
            Console.WriteLine("external uses git binary process to discover files.Not so fast for solutions with many projects and dependencies as we use git external process to get content of each file.");
            Console.WriteLine("system uses regular file systems to disocver files. Make sense to use if your repository is fully checked out.");
            Console.WriteLine("You can generate id_path_filename by doing : git ls-tree --format \"%(objectname),%(path)\" -r HEAD");
            Console.WriteLine();
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

        IMonoRepository repo = args[0] switch
        {
            "external" => new ExternalGitRepository(args[1]),
            "internal" => new InternalGitRepository(args[1], id_path_filename),
            "system" => new FileSystemRepository(args[1]),
            _ => throw new ArgumentException("Valid parameters are 'internal', 'external', or 'system'.")
        };


        if (!repo.FileExists(solutionFilePath) || !args[2].EndsWith(".sln"))
        {
            Console.WriteLine("Please provide a valid solution file (.sln).");
            return;
        }

        var depFinder = new SolutionDependencyAnalyzer(repo);
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
