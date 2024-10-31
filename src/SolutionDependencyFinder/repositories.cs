using LibGit2Sharp;
using System.Diagnostics;
using System.Text;

namespace RoozSoft.SlnDependencyFinder;

public interface IMonoRepository
{
    bool FileExists(string filename);
    string GetFileContent(string filename);
}


public class ExternalGitRepository : IMonoRepository
{
    private readonly string _repositoryPath;
    HashSet<string> _files;
    Task ready;
    public ExternalGitRepository(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new ArgumentException("Invalid repository path", nameof(repositoryPath));
        }

        _repositoryPath = repositoryPath;
        ready = Task.Run(async () =>
        {
            _files =await  GetAllFilesAsync();
        });
    }

    public bool FileExists(string filename)
    {
        if (!ready.IsCompleted)
            ready.GetAwaiter().GetResult();
        filename = filename.Replace("\\", "/");
        return _files.Contains(filename); 
    }

    // Method to get all files in the repository (equivalent to `git ls-tree`)
    public async Task<HashSet<string>> GetAllFilesAsync()
    {
        var files = new HashSet<string>(300_000, StringComparer.OrdinalIgnoreCase);
        
        var result = await RunGitCommandAsyncList(new List<string>() { "ls-tree","-r","HEAD","--name-only" });

        using (var reader = new StringReader(result))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                files.Add(line);
            }
        }

        return files;
    }

    public string GetFileContent(string filename)
    {
        if (!ready.IsCompleted)
            ready.GetAwaiter().GetResult();
        filename = filename.Replace("\\", "/");
        string orgfilename;
        _files.TryGetValue(filename, out orgfilename);
        return GetFileContentAsync(orgfilename).GetAwaiter().GetResult();
    }

    // Method to get the content of a specific file (equivalent to `git show`)
    public async Task<string> GetFileContentAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        var result = await RunGitCommandAsyncList(new List<string>() { "--no-pager","show", $"HEAD:{filePath}" });
        return result;
    }

    SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1);
    // Helper method to execute Git commands
    private async Task<string> RunGitCommandAsyncList(IList<string> arguments)
    {
        //await SemaphoreSlim.WaitAsync();
        try
        {
            var processStartInfo = new ProcessStartInfo("git", arguments)
            {
                RedirectStandardOutput = true,
                //RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetFullPath(_repositoryPath)

            };
            processStartInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationTokenSource cts_thread = new CancellationTokenSource();
            using (var process = new Process { StartInfo = processStartInfo })
            {
                var output = new StringBuilder();
                var error = new StringBuilder();

                process.Start();

                new Thread(() =>
                {
                    while (!cts.IsCancellationRequested) 
                    {
                    if (!process.StandardOutput.EndOfStream)
                        output.AppendLine(process.StandardOutput.ReadLine());
                    //if (!process.StandardError.EndOfStream)
                    //    error.AppendLine(process.StandardError.ReadLine());
                    }
                    cts_thread.Cancel();
                }).Start();



                process.WaitForExit();
                cts.Cancel();
                while (!cts_thread.IsCancellationRequested)
                    await Task.Delay(100);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Git command failed: {error}");
                }

                return output.ToString();
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return "";
        }
        finally
        {
            //SemaphoreSlim?.Release();
        }
    }
}

public class InternalGitRepository : IMonoRepository
{
    Repository repo;
    Dictionary<string, string> allfiles = new(300_000, StringComparer.OrdinalIgnoreCase);
    //FrozenDictionary<string, string> allfiles;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="repoPath"></param>
    /// <param name="_solutionFilePath"></param>
    /// <param name="id_path_filename">can be generated as: git ls-tree --format "%(objectname),%(path)" -r HEAD</param>
    public InternalGitRepository(string repoPath, string id_path_filename)
    {
        if (!String.IsNullOrEmpty(id_path_filename) && File.Exists(id_path_filename))
        {

            Parallel.ForEach(File.ReadLines(id_path_filename), (line) =>
            {

                int i = 40;
                //if (line[i] != ',')
                //    i = line.IndexOf(','); 
                allfiles[line.Substring(i + 1)] = line.Substring(0, i);
                //_allfiles[line.AsMemory().Slice(0,i)] = line.AsMemory().Slice(i+1,line.Length-i-1);
            });

            //allfiles = _allfiles.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        }

        repo = new Repository(repoPath);

        if (String.IsNullOrEmpty(id_path_filename) || !File.Exists(id_path_filename))
        {
            var commit = repo.Head.Tip;
            var tree = commit.Tree;

            //ListFiles(tree, "");
            ParallelListFiles(tree, "");
        }
    }

    void ListFiles(Tree tree, string prefix)
    {
        foreach (var entry in tree)
        {
            if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                // Recursively list files in subdirectories
                var subtree = (Tree)entry.Target;
                ListFiles(subtree, $"{prefix}{entry.Name}/");
            }
            else if (entry.TargetType == TreeEntryTargetType.Blob)
            {
                allfiles.Add($"{prefix}{entry.Name}", entry.Target.Sha);
                // Print file path
                //Console.WriteLine($"{prefix}{entry.Name}");
            }
        }
    }

    void ParallelListFiles(Tree tree, string prefix)
    {
        var myTask = (TreeEntry entry) =>
        {
            if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                var subtree = (Tree)entry.Target;
                // Recursively process subdirectories in parallel
                ParallelListFiles(subtree, $"{prefix}{entry.Name}/");
            }
            else if (entry.TargetType == TreeEntryTargetType.Blob)
            {
                // Add file to the concurrent collection
                //files.Add($"{prefix}{entry.Name}");
                allfiles.Add($"{prefix}{entry.Name}", entry.Target.Sha);
            }
        };

        if (tree.Count > 8)
        {
            Parallel.ForEach(tree, entry =>
            {
                myTask(entry);
            });
        }
        else
        {
            foreach (var entry in tree)
                myTask(entry);
        }
    }

    public bool FileExists(string filename)
    {
        filename = filename.Replace("\\", "/");
        return allfiles.ContainsKey(filename);
    }

    public string GetFileContent(string filename)
    {
        filename = filename.Replace("\\", "/");
        //if (allfiles.ContainsKey(filename))
        {
            return repo.Lookup<LibGit2Sharp.Blob>(allfiles[filename]).GetContentText();
        }
        //return null;
    }



}
