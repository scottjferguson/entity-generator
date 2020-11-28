using Pluralize.NET.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sprocket.EntityFramework
{
    public class EntityFileProcessor
    {
        private readonly Pluralizer _pluralizer;

        public EntityFileProcessor(Pluralizer pluralizer)
        {
            _pluralizer = pluralizer;
        }
        
        public async Task ProcessAsync(string domainDirectoryPath, CancellationToken cancellationToken)
        {
            string entityDirectory = Path.Combine(domainDirectoryPath, "Entities");
            string contextDirectory = Path.Combine(domainDirectoryPath, "Context");
            string[] entityFilePaths = Directory.GetFiles(entityDirectory);

            // Get a list of entity files to process
            List<EntityFile> entityFiles = entityFilePaths
                .Where(fp => !fp.EndsWith("Entity.cs") && !fp.EndsWith("Context.cs"))
                .Select(fp => 
                {
                    FileInfo fileInfo = new FileInfo(fp);
                    string oldEntityName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    string newEntityName = oldEntityName + "Entity";
                    string newFilePath = Path.Combine(fileInfo.DirectoryName!, $"{newEntityName}.cs");

                    return new EntityFile
                    {
                        OldEntityName = oldEntityName,
                        NewEntityName = newEntityName,
                        OldFilePath = fp,
                        NewFilePath = newFilePath
                    };
                }).ToList();

            // Operate on the entity file first
            Parallel.ForEach(entityFiles, async (entityFile, state) =>
            {
                //FileInfo fileInfo = new FileInfo(filePath);

                string newFileContent;
                //string oldEntityName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                //string newEntityName = $"{oldEntityName}Entity";
                string fileContent = await File.ReadAllTextAsync(entityFile.OldFilePath, cancellationToken);
                string searchTerm = $"public partial class {entityFile.OldEntityName}";

                bool isView = entityFile.OldEntityName.StartsWith("Vw");
                
                if (fileContent.Contains("public string CreatedBy { get; set; }") &&
                    fileContent.Contains("public DateTime CreatedDate { get; set; }") &&
                    fileContent.Contains("public string ModifiedBy { get; set; }") &&
                    fileContent.Contains("public DateTime? ModifiedDate { get; set; }") &&
                    !isView)
                {
                    // Rename the class with a suffix of Entity (this class has all auditable fields, make it IAuditable)
                    newFileContent = fileContent.Replace(searchTerm, $"{searchTerm}Entity : Core.Framework.IAuditable");
                }
                else if (fileContent.Contains("public string CreatedBy { get; set; }") &&
                         fileContent.Contains("public DateTime CreatedDate { get; set; }") &&
                         !isView)
                {
                    // Rename the class with a suffix of Entity (this class has only created fields, make it IReadOnly)
                    newFileContent = fileContent.Replace(searchTerm, $"{searchTerm}Entity : Core.Framework.IReadOnly");
                }
                else
                {
                    // Rename the class with a suffix of Entity
                    newFileContent = fileContent.Replace(searchTerm, $"{searchTerm}Entity");
                }

                // Rename the constructor
                newFileContent = newFileContent.Replace($"public {entityFile.OldEntityName}()", $"public {entityFile.NewEntityName}()");

                // Overwrite the context file with the new content
                await File.WriteAllTextAsync(entityFile.OldFilePath, newFileContent, cancellationToken);

                // Track this entity name, we'll need it later
                //if (!_entityTypeNameDictionary.TryAdd(entityFile.OldEntityName, entityFile.NewEntityName))
                //{
                //    Console.WriteLine($"Error: Could not add '{entityFile.OldEntityName}' and '{entityFile.NewEntityName}'");
                //}

                // Read it back as an array, we'll be doing line-by-line replacements from here
                string[] fileLines = await File.ReadAllLinesAsync(entityFile.OldFilePath, cancellationToken);

                // Keep the entire contents of the file in a string for the destination of the search and replaces
                fileContent = await File.ReadAllTextAsync(entityFile.OldFilePath, cancellationToken);
                newFileContent = fileContent;

                foreach (string fileLine in fileLines)
                {
                    // If this line is initializing a collection...
                    if (fileLine.Contains("new HashSet<"))
                    {
                        // Parse the old values from the line
                        string oldTypeName = fileLine.Trim().Split(" ")[0];

                        //if (!_entityTypeNameDictionary.TryGetValue(oldTypeName, out string newTypeName))
                        //{
                        //    Console.WriteLine($"Could not update property type for HastSet. Could not find matching type name reference in _entityTypeNameDictionary for type name {oldTypeName}");
                        //    continue;
                        //}

                        // Get the file line segments as an array
                        string[] fileLineSegments = fileLine.Trim().Replace($"{oldTypeName}>", $"{oldTypeName}Entity>").Split(" ");

                        // Create the new file line
                        string newFileLine = $"{string.Empty.PadLeft(12)}{_pluralizer.Pluralize(fileLineSegments[0])} {string.Join(" ", fileLineSegments.Skip(1))}";

                        // Replace the old type name with the new type name
                        newFileContent = newFileContent.Replace(fileLine, newFileLine);
                    }
                    // If this line is declaring an Entity property...
                    else if (fileLine.Trim().StartsWith("public virtual "))
                    {
                        // Parse the old type name from the line
                        string[] fileLineSegments = fileLine.Trim().Replace("ICollection", "").Replace("<", "").Replace(">", "").Split(" ");
                        string oldTypeName = fileLineSegments[Array.IndexOf(fileLineSegments, "virtual") + 1];

                        string newFileLine = fileLine;

                        if (fileLine.Contains("ICollection"))
                        {
                            // Parse the old property name from the line
                            string oldPropertyName = fileLine.Trim().Replace(" { get; set; }", "").Split(" ").Last();

                            // Pluralize it
                            string newPropertyName = _pluralizer.Pluralize(oldPropertyName);

                            // Replace it in the destination
                            newFileLine = fileLine.Replace(fileLine, fileLine.Replace($"{oldPropertyName} ", $"{newPropertyName} "));
                        }

                        EntityFile entityFileLookedUp = entityFiles.SingleOrDefault(ef => ef.OldEntityName == oldTypeName);

                        if (entityFileLookedUp == null)
                        {
                            throw new Exception($"Could not find an entity file where OldEntityName is '{oldTypeName}'");
                        }

                        // Update the entity type name
                        string newFileContents = ReplaceFirst(newFileLine, oldTypeName, entityFileLookedUp.NewEntityName);

                        // Replace the old type name with the new type name
                        newFileContent = newFileContent.Replace(fileLine, newFileContents);
                    }
                }

                // Overwrite the context file with the new content
                await File.WriteAllTextAsync(entityFile.OldFilePath, newFileContent, cancellationToken);

                fileContent = await File.ReadAllTextAsync(entityFile.OldFilePath, cancellationToken);

                // Write the new content to a new file with a name that matches the new entity type name
                await File.WriteAllTextAsync(entityFile.NewFilePath, fileContent, cancellationToken);

                // Delete the old file
                File.Delete(entityFile.OldFilePath);
            });

            // Operate on the Context class file
            string[] contextFilePaths = Directory.GetFiles(contextDirectory).Where(path => path.EndsWith("Context.cs") && !path.EndsWith("DbContext.cs")).ToArray();

            if (contextFilePaths.Length == 0)
            {
                Console.WriteLine("No context files found. Could not update context class file");
            }
            else if (contextFilePaths.Length > 1)
            {
                Console.WriteLine("Multiple context files found. Could not update context class file");
            }
            else
            {
                FileInfo fileInfo = new FileInfo(contextFilePaths.Single());

                // Get the contents of the context file
                string fileContent = await File.ReadAllTextAsync(fileInfo.FullName, cancellationToken);
                string newFileContent = fileContent;

                // Replace type names
                foreach (EntityFile entityFile in entityFiles)
                {
                    newFileContent = newFileContent.Replace($"<{entityFile.OldEntityName}>", $"<{entityFile.NewEntityName}>");
                }

                // Overwrite the context file with the new content
                await File.WriteAllTextAsync(fileInfo.FullName, newFileContent, cancellationToken);

                // Read it back as an array, we'll be doing line-by-line replacements from here
                string[] fileLines = await File.ReadAllLinesAsync(fileInfo.FullName, cancellationToken);

                // Remove the password left behind be EF's generator
                string lineWithConnectionString = fileLines.SingleOrDefault(fl => fl.Contains("optionsBuilder.UseSqlServer("));
                string lineWithConnectionStringComment = fileLines.SingleOrDefault(fl => fl.Contains("#warning To protect potentially sensitive information in your connection string"));

                if (!string.IsNullOrEmpty(lineWithConnectionString) && !string.IsNullOrEmpty(lineWithConnectionStringComment))
                {
                    newFileContent = newFileContent
                        .Replace(lineWithConnectionString, "")
                        .Replace(lineWithConnectionStringComment, "");
                }

                // Pluralize the collection names on the context
                foreach (string fileLine in fileLines)
                {
                    // If this line is declaring a property...
                    if (fileLine.Trim().StartsWith("public virtual DbSet<"))
                    {
                        // Parse the old property name from the line
                        string oldPropertyName = fileLine.Trim().Replace(" { get; set; }", "").Split(" ").Last();

                        // Pluralize it
                        string newPropertyName = _pluralizer.Pluralize(oldPropertyName);

                        // Replace it in the destination
                        newFileContent = newFileContent.Replace(fileLine, fileLine.Replace($"{oldPropertyName} ", $"{newPropertyName} "));
                    }
                    // If this line is defining a entity relationship for a child collection...
                    else if (fileLine.Trim().StartsWith(".WithMany("))
                    {
                        // Parse the old property name from the line
                        string oldPropertyName = fileLine.Trim().Replace(".WithMany(", "").Replace(")", "");

                        // Pluralize it
                        string newPropertyName = _pluralizer.Pluralize(oldPropertyName);

                        // Replace it in the destination
                        newFileContent = newFileContent.Replace(fileLine, fileLine.Replace($"{oldPropertyName}", $"{newPropertyName}"));
                    }
                }

                // Overwrite the context file with the new content
                await File.WriteAllTextAsync(fileInfo.FullName, newFileContent, cancellationToken);
            }
        }
        
        private string ReplaceFirst(string str, string search, string replace)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            int pos = str.IndexOf(search, StringComparison.Ordinal);

            if (pos < 0)
            {
                return str;
            }

            return str.Substring(0, pos) + replace + str.Substring(pos + search.Length);
        }
    }

    public class EntityFile
    {
        public string OldEntityName { get; set; }
        public string NewEntityName { get; set; }
        public string OldFilePath { get; set; }
        public string NewFilePath { get; set; }
    }
}
