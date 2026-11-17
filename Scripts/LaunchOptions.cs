using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MathRacer;

/// <summary>
/// The user arguments after Godot's <c>--</c> separator, read once at startup.
/// This is the only place that reads the command line.
/// </summary>
internal static class LaunchOptions
{
    private static readonly string[] args = OS.GetCmdlineUserArgs();

    public static IReadOnlyList<string> Args => args;

    public static bool Has(string flag) => args.Contains(flag);

    /// <summary>The value of <c>name=value</c>, or null when the argument is missing.</summary>
    public static string? Value(string name)
    {
        string prefix = name + "=";
        return args.FirstOrDefault(argument => argument.StartsWith(prefix))?[prefix.Length..];
    }

    /// <summary>A world seed requested with <c>--seed=N</c>; leaves room for generator stream offsets.</summary>
    public static int? Seed
    {
        get
        {
            string? value = Value("--seed");
            if (value == null)
                return null;
            if (int.TryParse(value, out int seed) && seed > 0 && seed < int.MaxValue - 4096)
                return seed;
            throw new ArgumentException("Seed must be between 1 and 2147479550.");
        }
    }

    /// <summary>Diagnostics, captures, explicit seeds and <c>--no-save</c> leave the player's save untouched.</summary>
    public static bool UsesPlayerSave { get; } =
        !args.Any(argument =>
            argument.EndsWith("-checks")
            || argument is "--smoke-test" or "--road-audit" or "--capture" or "--no-save"
            || argument.StartsWith("--seed=")
        );
}
