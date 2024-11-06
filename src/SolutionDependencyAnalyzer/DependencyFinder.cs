
using Microsoft.Build.Construction;

using System.Collections.Concurrent;

namespace RoozSoft.SolutionDependencyAnalyzer;

class ProjectItem
{
    public bool traversed = false;
    public Task<ProjectRootElement?> project;
}

public class SolutionDependencyAnalyzer
{
    HashSet<string> allDependencyFolders = new HashSet<string>(50);
    ConcurrentDictionary<string, ProjectItem> projectDependency = new();
    IMonoRepository repo;
    static string fullPathPrefix = OperatingSystem.IsWindows() ? "C:\\" : "/";
    public SolutionDependencyAnalyzer(IMonoRepository _repo)
    {
        repo = _repo;
    }

    public void AddDependencyFolder(string FolderPath)
    {
        FolderPath = NormalizePath(FolderPath);
        var modified = true;
        while (modified)
        {
            string toremove = "";
            modified = false;
            foreach (var item in allDependencyFolders)
            {
                if (FolderPath.StartsWith(item))
                    return;
                if (item.StartsWith(FolderPath))
                {
                    toremove = item;
                    break;
                }
            }
            if (!String.IsNullOrEmpty(toremove))
            {
                allDependencyFolders.Remove(toremove);
            }
        }
        allDependencyFolders.Add(FolderPath);
    }

    public string NormalizePath(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return path.Replace("/", "\\");
        }
        else
        {
            return path.Replace("\\", "/");
        }
    }

    void AddNewProject(string projectFilePath)
    {
        projectFilePath = NormalizePath(projectFilePath);
        projectDependency.TryAdd(projectFilePath, new ProjectItem()
        {
            project = Task.Run(() => {
                try
                {
                    var newProjectFileName = Path.GetTempFileName();
                    var projectDirectoryPath = Path.GetFullPath(Path.GetDirectoryName(projectFilePath), fullPathPrefix).Substring(fullPathPrefix.Length);
                    File.WriteAllText(newProjectFileName, repo.GetFileContent(projectFilePath));
                    return ProjectRootElement.Open(newProjectFileName);
                }
                catch
                {
                    return null;
                }
            })
        });
    }

    public List<string> GetDependencyList(string solutionFilePath)
    {
        return GetDependencyListAsync(solutionFilePath).GetAwaiter().GetResult().ToList();
    }

    async Task<HashSet<string>> GetDependencyListAsync(string solutionFilePath)
    {
        var solutionFolder = Path.GetDirectoryName(solutionFilePath);
        if (String.IsNullOrEmpty(solutionFolder))
            solutionFolder = ".";
        ParseSolutionFile(solutionFilePath).ForEach(x => AddNewProject(x));

        AddDependencyFolder(solutionFolder);

        while (projectDependency.Any(x => !x.Value.traversed))
        {
            try
            {
                foreach (var project in projectDependency)
                {

                    if (project.Value.traversed)
                        continue;
                    //Console.WriteLine($"Processing project: {project.Key}");
                    if (await GetProjectDependencyFoldersAsync(project.Key, solutionFolder))
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed on getting dependency {ex}");
            }
        }
        return allDependencyFolders;
    }

    public List<string> ParseSolutionFile(string solutionFilePath)
    {
        var solutionFolder = Path.GetDirectoryName(solutionFilePath);
        var projectPaths = new List<string>();

        foreach (var line in repo.GetFileContent(solutionFilePath).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Project(") && line.Contains(".csproj"))
            {
                var parts = line.Split(',');
                var projectPath = parts[1].Trim().Trim('"');
                var fullPath = NormalizePath(Path.Combine(solutionFolder, projectPath));
                fullPath = Path.GetFullPath(fullPath, fullPathPrefix).Substring(fullPathPrefix.Length);

                if (repo.FileExists(fullPath))
                {
                    projectPaths.Add(fullPath);
                }
                else
                {

                }
            }
        }

        return projectPaths;
    }
    async Task<bool> GetProjectDependencyFoldersAsync(string projectFilePath, string solutionFolder)
    {

        var changed = false;
        projectFilePath = NormalizePath(projectFilePath);
        projectDependency[projectFilePath].traversed = true;
        var projectDirectoryPath = Path.GetDirectoryName(projectFilePath);
        AddDependencyFolder(projectDirectoryPath);

        var project = await projectDependency[projectFilePath].project;
        if (project == null)
            return changed;

        try
        {
            // Get all folders containing .cs, .html, .js files
            foreach (var item in project.Items)
            {
                if (item.ItemType == "Compile" || item.ItemType == "Content" || item.ItemType == "Analyzer" || item.ItemType == "None")
                {
                    var filePath = NormalizePath(Path.Combine(projectDirectoryPath, item.Include));
                    filePath = Path.GetFullPath(filePath, fullPathPrefix).Substring(fullPathPrefix.Length);

                    if (!string.IsNullOrEmpty(filePath) && repo.FileExists(filePath))
                    {
                        var folderPath = Path.GetDirectoryName(filePath);
                        if (folderPath.StartsWith(projectDirectoryPath) || String.IsNullOrEmpty(folderPath))
                            continue;
                        AddDependencyFolder(Path.GetFullPath(folderPath));
                    }
                }
                else if (item.ItemType == "Reference")
                {
                    if (item.FirstChild != null && item.AllChildren.Any(x => x.ElementName == "HintPath"))
                    {
                        var referencePath = NormalizePath(((ProjectMetadataElement)item.AllChildren.FirstOrDefault(x => x.ElementName == "HintPath")).Value);
                        if (!string.IsNullOrEmpty(referencePath) && referencePath.Contains(".dll") && !referencePath.Contains(".nuget\\"))
                        {
                            if (referencePath.Contains("$(SolutionDir)"))
                            {
                                referencePath = referencePath.Replace("$(SolutionDir)", $"{solutionFolder}\\");
                            }
                            if (referencePath.Contains("$(ProjectDir)"))
                            {
                                referencePath = referencePath.Replace("$(ProjectDir)", $"{projectDirectoryPath}\\");
                            }
                            if (referencePath.Contains("C:\\Program Files"))
                            {
                                Console.WriteLine($"WARNING --- Project:{projectFilePath} has reference {referencePath} which is invalid for build pipeline");
                                continue;
                            }

                            var filePath = NormalizePath(Path.Combine(projectDirectoryPath, referencePath));
                            filePath = Path.GetFullPath(filePath, fullPathPrefix).Substring(fullPathPrefix.Length);
                            if (repo.FileExists(filePath))
                            {
                                AddDependencyFolder(Path.GetDirectoryName(filePath));
                            }
                            else
                            {
                                filePath = NormalizePath(Path.Combine(solutionFolder, referencePath));
                                filePath = Path.GetFullPath(filePath, fullPathPrefix).Substring(fullPathPrefix.Length);
                                if (repo.FileExists(filePath))
                                    AddDependencyFolder(Path.GetDirectoryName(filePath));
                            }
                        }
                    }
                }
                else if (item.ItemType == "ProjectReference")
                {
                    var filePath = NormalizePath(Path.Combine(projectDirectoryPath, item.Include));
                    filePath = Path.GetFullPath(filePath, fullPathPrefix).Substring(fullPathPrefix.Length);

                    if (!string.IsNullOrEmpty(filePath) && repo.FileExists(filePath))
                    {
                        if (!projectDependency.ContainsKey(filePath))
                        {
                            Console.WriteLine($"WARNING --- Project:{filePath} is referenced but is not part of your solution!");
                            AddNewProject(filePath);
                            changed = true;
                        }
                    }
                }
            }
        }
        finally
        {
            File.Delete(project.FullPath);
        }
        return changed;
    }
}
