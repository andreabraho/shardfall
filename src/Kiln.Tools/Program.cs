using Kiln.Tools.Commands;

namespace Kiln.Tools;

/// <summary>
/// The project's command line tool. Run from the repo root:
/// <code>
///   dotnet run --project src/Kiln.Tools -- validate
///   dotnet run --project src/Kiln.Tools -- codegen
///   dotnet run --project src/Kiln.Tools -- codegen --check
/// </code>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0];
        var rest = args[1..];

        try
        {
            return command switch
            {
                "validate" => ValidateCommand.Run(rest),
                "codegen" => CodegenCommand.Run(rest),
                _ => Unknown(command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"sohan: {command} failed — {ex.Message}");
            return 70; // EX_SOFTWARE
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"sohan: unknown command '{command}'.");
        PrintUsage();
        return 64; // EX_USAGE
    }

    private static void PrintUsage() => Console.WriteLine(
        """
        sohan — project tooling

        Commands:
          validate [--data <dir>]              Load and validate all game content.
                                               Exit code 1 if any error is found.

          codegen  [--data <dir>] [--out <f>]  Generate strongly-typed content id constants.
                   [--check]                   --check verifies the committed file is current
                                               instead of writing it (used by CI).

        Options:
          --data <dir>   Path to game/data. Defaults to searching upward from the current
                         directory for a folder containing game/data.
        """);

    /// <summary>
    /// Walks up from the working directory looking for <c>game/data</c>, so the tool works
    /// from the repo root, from the project folder, or from a CI checkout without config.
    /// </summary>
    public static string ResolveDataRoot(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--data") return Path.GetFullPath(args[i + 1]);
        }

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "game", "data");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find game/data above the current directory. Pass --data <dir> explicitly.");
    }

    public static string? OptionValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }

        return null;
    }

    public static bool HasFlag(string[] args, string name) => args.Contains(name);
}
