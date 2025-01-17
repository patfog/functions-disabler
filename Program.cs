using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace functions_disabler;

class Program
{
  static void Main(string[] args)
  {
    if (args.Length < 1)
    {
      Console.WriteLine("Usage: dotnet run <root-folder>");
      Environment.Exit(1); // Exit with code 1 for incorrect usage
    }

    string rootFolder = args[0];

    // Find all project directories (containing .csproj files)
    var csprojDirs = Directory.GetFiles(rootFolder, "*.csproj", SearchOption.AllDirectories)
                              .Select(Path.GetDirectoryName)
                              .Distinct()
                              .ToList();

    foreach (var projectDir in csprojDirs)
    {
      if (string.IsNullOrEmpty(projectDir)) continue;

      var outputFile = Path.Combine(projectDir, "local.settings.json");

      // Only attempt creation of file if it already exists
      if (!File.Exists(outputFile)) continue;

      // Extract function names
      var functionNames = ExtractFunctionNames(projectDir);

      // Update local.settings.json
      UpdateLocalSettingsJson(outputFile, functionNames);
    }

    Environment.Exit(0); // Exit successfully
  }

  // Extract function names from all .cs files in the project directory
  static HashSet<string> ExtractFunctionNames(string projectDir)
  {
    var functionNames = new HashSet<string>();

    // Find all .cs files in the project directory
    var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);

    foreach (var file in csFiles)
    {
      try
      {
        var code = File.ReadAllText(file);
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        // Set up compilation for semantic analysis
        var compilation = CSharpCompilation.Create("Analysis")
            .AddSyntaxTrees(tree)
            .AddReferences(
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));

        var semanticModel = compilation.GetSemanticModel(tree);

        // Extract all attributes named "Function"
        var attributes = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Name.ToString() == "Function");

        foreach (var attribute in attributes)
        {
          var argument = attribute.ArgumentList?.Arguments.FirstOrDefault();
          if (argument != null)
          {
            // Resolve constant or static field values
            var constantValue = semanticModel.GetConstantValue(argument.Expression);
            if (constantValue.HasValue && constantValue.Value is string stringValue)
            {
              functionNames.Add(stringValue);
            }
            else
            {
              throw new Exception($"Could not resolve function name. Location: {file}:{attribute.GetLocation().GetLineSpan().StartLinePosition}");
            }
          }
        }
      }
      catch
      {
        // Ignore errors and continue processing the next file
      }
    }

    return functionNames;
  }

  // Update the local.settings.json file based on the function names
  static void UpdateLocalSettingsJson(string settingsFilePath, HashSet<string> functionNames)
  {
    var settingsJson = File.ReadAllText(settingsFilePath);
    var settingsNode = JsonNode.Parse(settingsJson);
    if (settingsNode is null) return;

    // Ensure the "Values" section exists
    if (settingsNode["Values"] is not JsonObject values)
    {
      values = [];
      settingsNode["Values"] = values;
    }

    // Get existing function names in the settings
    var existingFunctionNames = values
        .Where(p => p.Key.StartsWith("AzureWebJobs."))
        .Select(p => p.Key.Substring("AzureWebJobs.".Length).Replace(".Disabled", ""))
        .ToHashSet();

    // Add or update function entries
    foreach (var functionName in functionNames)
    {
      var key = $"AzureWebJobs.{functionName}.Disabled";
      if (!values.ContainsKey(key))
      {
        values[key] = true;
      }
    }

    // Remove functions that no longer exist
    foreach (var functionName in existingFunctionNames)
    {
      if (!functionNames.Contains(functionName))
      {
        values.Remove($"AzureWebJobs.{functionName}.Disabled");
      }
      else
      {
        var key = $"AzureWebJobs.{functionName}.Disabled";
        values[key] = true; // Ensure it is disabled
      }
    }

    // Save the updated settings back to the file
    File.WriteAllText(settingsFilePath, settingsNode.ToString());
  }
}
