# SolutionDependencyAnalyzer

SolutionDependencyAnalyzer is a .NET 8.0/.NET 6.0 library designed for monorepo environments or any large repository setup where analyzing solution dependencies efficiently is essential. This tool can parse Visual Studio solution (.sln) files, identify all dependencies across projects within the solution, and output a complete list of required folders. This list can be used to enable selective checkouts, optimize sparse checkouts, or manage build dependencies.

# Features
- Visual Studio Solution Parsing: Automatically processes a .sln file, traversing all associated projects to map out dependencies.
- Folder-Level Dependency Analysis: Returns a list of all folders that a solution requires, streamlining sparse checkouts and focused dependency management.
- Multiple Git Access Options:
   - External Git Executable: Uses your Git setup for dependency analysis.
   - File Access Mode: Directly accesses files for simpler, lightweight dependency management.
   - Internal LibGit: Uses a built-in library for Git interactions, ideal for environments without a Git executable.

# Installation
Install SolutionDependencyAnalyzer from NuGet.

``` bash
dotnet add package RoozSoft.SolutionDependencyAnalyzer
```

# Usage
This library can be used either as part of a larger GitHub Action workflow (see SolutionChangeDetector-Action) or as a standalone library in any .NET project.

Example: Analyzing Solution Dependencies
``` csharp
using RoozSoft.SolutionDependencyAnalyzer;

// Initialize analyzer for your .sln file
var analyzer = new SolutionAnalyzer("path/to/your/solution.sln");
var repo =  new ExternalGitRepository("path\\to\\repository"),
var repo =  new InternalGitRepository("path/to/repository", "path/to/id_path_filename"),
var repo =  new FileSystemRepository("path/to/repository"),

var depFinder = new SolutionDependencyAnalyzer(repo);
var result = depFinder.GetDependencyList("relative/path/to/solution/file");

```

# Repository Modes of operation
The library provides three options for accessing files and dependencies, suited to different environments:

- ExternalGitRepository: This relies on running git as external process and parsing its results. It requires git accessible in your path and ensures maximum compatibility.
- FileAccess: This just uses OS file access, which makes sense if you have everything checked out or you dont want this specific feature of library.
- InternalGitRepository: This uses our bundled library of LibGit to discover files and read contents of files.

# Use Cases
- Sparse Checkouts: Identify only the folders necessary for your solution, making monorepo checkouts faster and more efficient.
- Build Dependency Verification: Verify which files have changed to determine if a rebuild of the solution is required.
- Optimized Solution Builds: Quickly retrieve top-level dependencies, allowing faster dependency analysis and solution builds.

# Future Enhancements


# License
This project is licensed under the MIT License.