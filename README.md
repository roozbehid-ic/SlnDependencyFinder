# SLNDependencyFinder

This project were born from the need to find all dependency files/directories in a solution. Dependencies are defined as projects and all the files they need to compile/build correctly.

# Use case
Imagine you have a very huge monorepo which consist of many different visual studio solutions. Obviously you can just checkout the whole repository and build those.
But smarter move is just checkout files that are needed to build the solution. 
Also knowing what fiels are needed, you can selectively fire different github actions to build projects. So if files of one solution is changed you wont be building all other solutions that are not changed.

This project gives you ability to find dependencies of each solution and it works based on git objects. Meaning you just need a clone with no files checked out and it uses git to read `.sln` and projects inside it to figure out dependencies.
After this step you can use `git sparse-checkout` feature to just checkout files/directories that are needed.

