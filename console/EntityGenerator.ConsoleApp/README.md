# How To

[Creating and Updating the entity model](https://www.learnentityframeworkcore.com/walkthroughs/existing-database)

### NuGet dependencies:

> Microsoft.EntityFrameworkCore
> Microsoft.EntityFrameworkCore.Design
> Microsoft.EntityFrameworkCore.SqlServer
> Microsoft.EntityFrameworkCore.Tools

### .NET Core CLI:

Tools > Command Line > Developer Command Prompt

// Run once per machine:
// dotnet tool install --global dotnet-ef --version 3.1.5
// ...or this to update it: dotnet tool update --global dotnet-ef

dotnet ef dbcontext scaffold "{{ConnectionString}}" Microsoft.EntityFrameworkCore.SqlServer --project console\Sprocket.ConsoleApp --context-dir Context --output-dir Entities --no-build --force

(if you get an error about updating EF Core tools, run this: dotnet tool update --global dotnet-ef)

### Development Environment Setup

The EF CLI command above requires the database password exist in this project's user secrets. Setup local App secrets:

1. Obtain secrets.json file
2. Place it in the same folder as the startup projects .csproj file
3. Execute the commands below to set application secrets

> Tools > Command Line > Developer Command Prompt
>
> ```
> cd src\Domain
> type secrets.json | dotnet user-secrets set
> ```