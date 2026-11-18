using System.IO;
using Godot;

namespace MathRacer.Diagnostics;

internal static class DiagnosticArtifacts
{
    internal const string DirectoryVariable = "MATHRACER_TEST_DIRECTORY";

    internal static string PathFor(string relativePath)
    {
        string root = System.Environment.GetEnvironmentVariable(DirectoryVariable)
            ?? ProjectSettings.GlobalizePath("res://artifacts");
        return Path.Combine(root, relativePath);
    }
}
