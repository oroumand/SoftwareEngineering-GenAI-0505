using System.Text;
using System.Text.RegularExpressions;

namespace WorkshopCli;

public static class Cli
{
    public const string Usage = "command --name CreatePost --output samples/demo [--dry-run]";

    public static int Run(string[] args, string workingDirectory, TextWriter output, TextWriter error)
    {
        try
        {
            if (args is ["--help"])
            {
                output.WriteLine(Usage);
                return 0;
            }

            var options = Parse(args);
            var root = Path.GetFullPath(workingDirectory);
            CheckLinks(root);
            var marker = Path.Combine(root, ".cli-lab");
            CheckLinks(marker);
            if (!File.Exists(marker) || File.ReadAllText(marker).Trim() != "cli-workshop-v1")
                throw new LabError(2, "E_WORKSPACE", "Run from the isolated CLI workshop root.");

            ValidateName(options.Name);
            var target = ValidateOutput(root, options.Output);
            var files = Render(options.Name);
            foreach (var file in files)
            {
                var path = Path.Combine(target, file.Name);
                CheckLinks(path);
                if (Path.Exists(path))
                    throw new LabError(3, "E_EXISTS", "A target file or directory already exists. Nothing was overwritten.");
            }

            if (options.DryRun)
            {
                foreach (var file in files)
                {
                    output.WriteLine($"WOULD_CREATE {Path.GetRelativePath(root, Path.Combine(target, file.Name))}");
                    output.WriteLine(file.Content);
                }
                return 0;
            }

            Directory.CreateDirectory(target);
            // Check again after directory creation; CreateNew protects the final filenames.
            CheckLinks(target);
            var reservations = new List<(FileStream Stream, string Content)>();
            try
            {
                // Reserve both names before writing content. No FileMode.Create / overwrite path.
                foreach (var file in files)
                    reservations.Add((new FileStream(Path.Combine(target, file.Name),
                        FileMode.CreateNew, FileAccess.Write, FileShare.None), file.Content));
                foreach (var reservation in reservations)
                {
                    var bytes = Encoding.UTF8.GetBytes(reservation.Content);
                    reservation.Stream.Write(bytes);
                    reservation.Stream.Flush();
                }
            }
            finally
            {
                foreach (var reservation in reservations)
                    reservation.Stream.Dispose();
            }

            foreach (var file in files)
                output.WriteLine($"CREATED {Path.GetRelativePath(root, Path.Combine(target, file.Name))}");
            return 0;
        }
        catch (LabError ex)
        {
            error.WriteLine($"{ex.Code}: {ex.Message}");
            return ex.ExitCode;
        }
        catch (UnauthorizedAccessException)
        {
            error.WriteLine("E_IO: Access denied. Inspect the sample directory for partial output; stop generation.");
            return 4;
        }
        catch (IOException)
        {
            error.WriteLine("E_IO: Filesystem operation failed. Inspect the sample directory for partial output; stop generation.");
            return 4;
        }
        catch (ArgumentException)
        {
            error.WriteLine("E_PATH: Invalid filesystem path.");
            return 2;
        }
        catch (NotSupportedException)
        {
            error.WriteLine("E_PATH: Unsupported filesystem path.");
            return 2;
        }
    }

    private static Options Parse(string[] args)
    {
        if (args.Length == 0 || args[0] != "command")
            throw new LabError(2, "E_ARGS", Usage);
        string? name = null, path = null;
        var dryRun = false;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--name" when name is null && i + 1 < args.Length:
                    name = args[++i];
                    break;
                case "--output" when path is null && i + 1 < args.Length:
                    path = args[++i];
                    break;
                case "--dry-run" when !dryRun:
                    dryRun = true;
                    break;
                default:
                    throw new LabError(2, "E_ARGS", "Unknown, repeated or incomplete option. " + Usage);
            }
        }
        if (name is null || path is null)
            throw new LabError(2, "E_ARGS", Usage);
        return new Options(name, path, dryRun);
    }

    private static void ValidateName(string name)
    {
        if (!Regex.IsMatch(name, "\\A[A-Z][A-Za-z0-9]{0,63}\\z") || IsDevice(name))
            throw new LabError(2, "E_NAME", "Use 1-64 ASCII letters/digits, starting with an uppercase letter; no device names.");
    }

    private static string ValidateOutput(string root, string relative)
    {
        var segments = relative.Replace('\\', '/').Split('/');
        if (Path.IsPathRooted(relative) || segments.Length < 2 || segments[0] != "samples" ||
            segments.Any(s => !Regex.IsMatch(s, "\\A[A-Za-z][A-Za-z0-9_-]{0,47}\\z") || IsDevice(s)))
            throw new LabError(2, "E_PATH", "Output must be samples/<folder>, with safe relative segments; no dots, devices or absolute paths.");
        var path = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var boundary = Path.Combine(root, "samples") + Path.DirectorySeparatorChar;
        if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
            throw new LabError(2, "E_PATH", "Output escapes the sample boundary.");
        CheckLinks(path);
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            if (File.Exists(current.FullName))
                throw new LabError(2, "E_PATH", "An output directory segment is a file.");
        return path;
    }

    private static bool IsDevice(string value) => Regex.IsMatch(value,
        "\\A(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])\\z", RegexOptions.IgnoreCase);

    private static void CheckLinks(string path)
    {
        // Inspect every existing component, including ancestors and dangling links.
        for (var current = new FileInfo(path); current is not null;
             current = current.Directory is { } parent ? new FileInfo(parent.FullName) : null)
        {
            var attributes = current.Attributes;
            if (attributes == (FileAttributes)(-1)) continue;
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new LabError(2, "E_LINK", "Symbolic links and reparse points are not supported in this lab path.");
        }
    }

    private static (string Name, string Content)[] Render(string name) =>
    [
        ($"{name}Command.cs", $$"""
        namespace Workshop.Sample;

        public sealed record {{name}}Command(string Title);

        """),
        ($"{name}Handler.cs", $$"""
        using System;

        namespace Workshop.Sample;

        public sealed class {{name}}Handler
        {
            public void Handle({{name}}Command command)
            {
                ArgumentNullException.ThrowIfNull(command);
                throw new NotImplementedException("Educational skeleton: implement a reviewed use case before use.");
            }
        }

        """)
    ];

    private sealed record Options(string Name, string Output, bool DryRun);
    private sealed class LabError(int exitCode, string code, string message) : Exception(message)
    {
        public int ExitCode { get; } = exitCode;
        public string Code { get; } = code;
    }
}
