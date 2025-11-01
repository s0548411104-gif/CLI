using System.CommandLine;
using System.IO;
using System.Linq;

var outputOption = new Option<FileInfo>(new[] { "--output", "-o" }, "File path and name") { IsRequired = true };
var languageOption = new Option<string[]>(new[] { "--language", "-l" }, "Programming languages to include (or 'all')") { IsRequired = true };
var noteOption = new Option<bool>(new[] { "--note", "-n" }, "Include source file comments");
var sortOption = new Option<string>(new[] { "--sort", "-s" }, () => "name", "Sort by 'name' or 'type'");
var removeEmptyLinesOption = new Option<bool>(new[] { "--remove-empty-lines", "-r" }, "Remove empty lines from code files");
var authorOption = new Option<string>(new[] { "--author", "-a" }, "Author name to include in bundle");

var bundleCommand = new Command("bundle", "Bundle code files to a single file");
bundleCommand.AddOption(outputOption);
bundleCommand.AddOption(languageOption);
bundleCommand.AddOption(noteOption);
bundleCommand.AddOption(sortOption);
bundleCommand.AddOption(removeEmptyLinesOption);
bundleCommand.AddOption(authorOption);

bundleCommand.SetHandler((output, languages, note, sort, removeEmptyLines, author) =>
{
    try
    {
        var excludedFolders = new[] { "bin", "obj", "debug", ".vs" };
        var allFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories)
            .Where(f => !excludedFolders.Any(ex => f.Contains(Path.DirectorySeparatorChar + ex + Path.DirectorySeparatorChar)))
            .ToArray();

        string[] extensions = languages.Contains("all")
            ? allFiles.Select(f => Path.GetExtension(f)).Distinct().ToArray()
            : languages.Select(l => $".{l.TrimStart('.')}").ToArray();

        var invalidLanguages = languages
            .Where(l => l != "all" && !allFiles.Any(f => Path.GetExtension(f).Equals($".{l}", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (invalidLanguages.Length > 0)
        {
            Console.WriteLine($"Warning: No files found for these languages: {string.Join(", ", invalidLanguages)}");
        }

        var filesToBundle = allFiles
            .Where(f => extensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (filesToBundle.Count == 0)
        {
            Console.WriteLine("No files found to bundle. Operation aborted.");
            return;
        }

        sort = sort.ToLower();
        if (sort == "type")
            filesToBundle = filesToBundle.OrderBy(f => Path.GetExtension(f)).ThenBy(f => Path.GetFileName(f)).ToList();
        else
            filesToBundle = filesToBundle.OrderBy(f => Path.GetFileName(f)).ToList();

        var outputPath = Path.IsPathRooted(output.FullName)
           ? output.FullName
           : Path.Combine(Directory.GetCurrentDirectory(), output.Name);

        using (var writer = new StreamWriter(outputPath))
        {
            writer.WriteLine($"// Bundle created at: {DateTime.Now}");
            if (!string.IsNullOrEmpty(author))
                writer.WriteLine($"// Author: {author}");

            foreach (var file in filesToBundle)
            {
                if (note)
                {
                    string relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), file);
                    writer.WriteLine($"// Source: {relativePath}");
                }

                var lines = File.ReadAllLines(file);
                if (removeEmptyLines)
                    lines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();

                foreach (var line in lines)
                    writer.WriteLine(line);
                writer.WriteLine();
            }
        }
        Console.WriteLine($"Bundle created successfully: {outputPath}");
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine("Error: Access denied. Cannot write to the specified path.");
    }
    catch (DirectoryNotFoundException)
    {
        Console.WriteLine("Error: File path is invalid");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}, outputOption, languageOption, noteOption, sortOption, removeEmptyLinesOption, authorOption);

var createRspCommand = new Command("create-rsp", "Create a response file for the bundle command");
var rspOutputOption = new Option<string>(new[] { "--file", "-f" }, "Response file name to create (e.g., bundle.rsp)") { IsRequired = false };
createRspCommand.AddOption(rspOutputOption);

createRspCommand.SetHandler((rspFileName) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(rspFileName))
        {
            Console.Write("Enter response file name (e.g., bundle.rsp): ");
            rspFileName = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(rspFileName))
            {
                Console.WriteLine("Error: Response file name cannot be empty.");
                return;
            }
        }

        Console.WriteLine("Let's create your bundle response file (.rsp)");
        Console.WriteLine("==============================================");

        Console.Write("Enter output file name (e.g., output.txt): ");
        string output = Console.ReadLine()?.Trim() ?? "";

        Console.Write("Enter languages (comma separated or 'all'): ");
        string languagesInput = Console.ReadLine()?.Trim() ?? "";
        var languages = languagesInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Console.Write("Include source note? (y/n): ");
        bool note = Console.ReadLine()?.Trim().ToLower() == "y";

        Console.Write("Sort by 'name' or 'type': ");
        string sort = Console.ReadLine()?.Trim().ToLower() ?? "name";
        if (sort != "name" && sort != "type")
        {
            Console.WriteLine("Invalid sort option. Defaulting to 'name'.");
            sort = "name";
        }

        Console.Write("Remove empty lines? (y/n): ");
        bool removeEmpty = Console.ReadLine()?.Trim().ToLower() == "y";

        Console.Write("Author name (optional): ");
        string author = Console.ReadLine()?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine("Error: Output file name cannot be empty.");
            return;
        }
        if (languages.Length == 0)
        {
            Console.WriteLine("Error: You must specify at least one language or 'all'.");
            return;
        }

        string command = "bundle";
        command += $" -o \"{output}\"";
        command += $" -l {string.Join(' ', languages)}";
        if (note) command += " -n";
        command += $" -s {sort}";
        if (removeEmpty) command += " -r";
        if (!string.IsNullOrEmpty(author))
            command += $" -a \"{author}\"";

        File.WriteAllText(rspFileName, command);
        Console.WriteLine($"\nResponse file created successfully: {rspFileName}");
        Console.WriteLine($"You can now run it with:");
        Console.WriteLine($"  dotnet run --% @{rspFileName}   (if using dotnet CLI)");
        Console.WriteLine($"  fib.exe --% @{rspFileName}       (if CLI installed in PATH)");

    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }

}, rspOutputOption);

var rootCommand = new RootCommand("Root command for File Bundler CLI");
rootCommand.AddCommand(bundleCommand);
rootCommand.AddCommand(createRspCommand);

await rootCommand.InvokeAsync(args);
