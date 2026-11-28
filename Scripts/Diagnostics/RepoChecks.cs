using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Godot;

namespace MathRacer.Diagnostics;

/// <summary>
/// <c>--repo-checks</c>: the rules of <c>CLAUDE.md</c> that no other check would notice being broken, checked on the
/// files of the checkout before any world is generated. The C# code is formatted as CSharpier formats it and no source
/// file is longer than 1000 lines, every line of the Markdown files fits in 120 columns, every plan says how far it got
/// and is listed in <c>docs/PLANS.md</c>, and no committed save fixture or world fingerprint was changed, deleted or
/// renamed, neither in a commit nor in the working tree. Every failure says how to put it right.
/// </summary>
internal static class RepoChecks
{
    /// <summary>The width of a line of Markdown, as of the C# code (<c>.csharpierrc.json</c>).</summary>
    internal const int Columns = 120;

    /// <summary>The most lines a C# source file may have.</summary>
    internal const int MaxLines = 1000;

    /// <summary>The words a plan's status line starts with (<c>docs/PLANS.md</c>).</summary>
    private static readonly string[] Statuses = { "návrh", "rozpracováno", "hotovo", "odloženo" };

    /// <summary>The folders whose committed files are never changed (<c>Tests/README.md</c>).</summary>
    private static readonly string[] Frozen = { "Tests/Saves", "Tests/Worlds" };

    private static int passed;
    private static int failed;

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            passed++;
            return;
        }
        failed++;
        GD.Print("FAIL: " + message);
    }

    /// <summary>Runs every check and returns the exit code.</summary>
    internal static int Run()
    {
        string root = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
        try
        {
            Formatting(root);
            FileLength(root);
            MarkdownWidth(root);
            PlanStatus(root);
            FixturesUnchanged(root);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            return 1;
        }
        GD.Print($"REPO_CHECKS checks={passed + failed} failed={failed}");
        if (failed == 0)
            GD.Print("REPO_CHECKS_PASSED");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// <c>dotnet csharpier check Scripts</c>, with the version pinned in <c>dotnet-tools.json</c>, finds nothing to
    /// change.
    /// </summary>
    private static void Formatting(string root)
    {
        var (exitCode, output) = Execute(root, "dotnet", "csharpier", "check", "Scripts");
        if (exitCode != 0)
            foreach (string line in Lines(output).Take(40))
                GD.Print("  " + line);
        Check(
            exitCode == 0,
            "the C# code is formatted: run `dotnet csharpier format Scripts` (after `dotnet tool restore` on a new "
                + "machine) and commit the result"
        );
    }

    /// <summary>No C# file of <c>Scripts</c> has more than <see cref="MaxLines"/> lines.</summary>
    private static void FileLength(string root)
    {
        int checkedFiles = 0;
        foreach (string file in CheckoutFiles(root, "Scripts/*.cs"))
        {
            checkedFiles++;
            int lines = File.ReadAllLines(Path.Combine(root, file)).Length;
            Check(
                lines <= MaxLines,
                $"{file} has {lines} lines, more than {MaxLines}: move a part with a responsibility of its own into a "
                    + "smaller class (rules, a builder, texts, a scene) and call it, which is better than a new partial file"
            );
        }
        Check(checkedFiles > 0, "git lists the C# files of the checkout");
    }

    /// <summary>
    /// Every line of every Markdown file of the checkout (committed or new, not ignored) has at most 120 characters,
    /// tables and code blocks included; only a line that is one word (a long link) may be longer.
    /// </summary>
    private static void MarkdownWidth(string root)
    {
        int checkedFiles = 0;
        foreach (string file in CheckoutFiles(root, "*.md"))
        {
            checkedFiles++;
            string[] lines = File.ReadAllLines(Path.Combine(root, file), Encoding.UTF8);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].TrimEnd();
                if (line.Length <= Columns || !line.Trim().Contains(' '))
                    continue;
                Check(
                    false,
                    $"{file}:{index + 1} has {line.Length} characters, more than {Columns}: wrap the paragraph at "
                        + $"{Columns} columns; a table row that does not fit becomes a list or a ### section"
                );
            }
        }
        Check(checkedFiles > 0, "git lists the Markdown files of the checkout");
    }

    /// <summary>
    /// Every <c>docs/*_PLAN.md</c> has, in its first lines, a line <c>**Stav:** </c> that starts with one of the
    /// <see cref="Statuses"/>, and <c>docs/PLANS.md</c> links to it.
    /// </summary>
    private static void PlanStatus(string root)
    {
        string index = File.ReadAllText(Path.Combine(root, "docs", "PLANS.md"), Encoding.UTF8);
        int plans = 0;
        foreach (string file in CheckoutFiles(root, "docs/*_PLAN.md"))
        {
            plans++;
            string name = Path.GetFileName(file);
            string? status = File.ReadLines(Path.Combine(root, file), Encoding.UTF8)
                .Take(10)
                .FirstOrDefault(line => line.StartsWith("**Stav:** "));
            Check(
                status != null && Statuses.Any(word => status["**Stav:** ".Length..].StartsWith(word)),
                $"{file} says how far it got: put a line `**Stav:** <{string.Join("|", Statuses)}> …` under its "
                    + "title (docs/PLANS.md says what each word means)"
            );
            Check(
                index.Contains($"({name})"),
                $"docs/PLANS.md lists {name}: add a line `- [`{name}`]({name}) – what it is about: status.`"
            );
        }
        Check(plans > 0, "git lists the plans in docs");
    }

    /// <summary>
    /// The files of <c>Tests/Saves</c> and <c>Tests/Worlds</c> are only ever added: no commit modified, deleted or
    /// renamed one, and the working tree and the index change none.
    /// </summary>
    private static void FixturesUnchanged(string root)
    {
        string history = Git(
            root,
            new[] { "log", "--no-renames", "--diff-filter=MDT", "--format=commit %h %s", "--name-status", "--" }
                .Concat(Frozen)
                .ToArray()
        );
        foreach (string line in Lines(history).Take(40))
            GD.Print("  " + line);
        Check(
            history.Trim().Length == 0,
            "no commit changed, deleted or renamed a committed save fixture or world fingerprint: take the change "
                + "back; a new save format gets a folder of its own and a migration (Tests/README.md)"
        );

        string status = Git(
            root,
            new[] { "status", "--porcelain", "--no-renames", "--untracked-files=no", "--" }.Concat(Frozen).ToArray()
        );
        foreach (string line in Lines(status))
        {
            bool changed = line.Length > 3 && (IsChange(line[0]) || IsChange(line[1]));
            Check(
                !changed,
                $"the committed fixture {line[3..]} is unchanged in the working tree: restore it with "
                    + $"`git restore --staged --worktree -- {line[3..]}`; fixtures are never edited or regenerated"
            );
        }
    }

    /// <summary>The files of the checkout, committed or new and not ignored, that match the git pathspec.</summary>
    private static IEnumerable<string> CheckoutFiles(string root, string pathspec) =>
        Lines(Git(root, "ls-files", "--cached", "--others", "--exclude-standard", "--", pathspec))
            .Distinct()
            .Where(file => File.Exists(Path.Combine(root, file)));

    private static bool IsChange(char code) => code is 'M' or 'D' or 'T' or 'R';

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0);

    private static string Git(string root, params string[] arguments)
    {
        var (exitCode, output) = Execute(
            root,
            "git",
            new[] { "-c", "core.quotePath=false" }.Concat(arguments).ToArray()
        );
        if (exitCode != 0)
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed ({exitCode}); repo-checks need a git checkout: {output}"
            );
        return output;
    }

    private static (int ExitCode, string Output) Execute(string root, string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
        try
        {
            using var process = System.Diagnostics.Process.Start(start)
                ?? throw new InvalidOperationException($"{program} did not start");
            var error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output + error.Result);
        }
        catch (Win32Exception error)
        {
            return (-1, $"{program} is not on the PATH: {error.Message}");
        }
    }
}
