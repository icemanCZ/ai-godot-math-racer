using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MathRacer.App;

namespace MathRacer.Saves;

public static class SaveStore
{
    public static bool Enabled => LaunchOptions.UsesPlayerSave;
    public static string SavePath => ProjectSettings.GlobalizePath("user://progress.json");
    private static readonly Lazy<SaveData?> initial = new(() => Enabled ? ReadInitial(SavePath) : null);
    public static SaveData? Initial => initial.Value;

    /// <summary>Reads the player's save at startup and sets aside whatever the game could overwrite for good.</summary>
    private static SaveData? ReadInitial(string path)
    {
        var data = Read(path);
        Preserve(path);
        return data;
    }

    internal static SaveData? Read(string path)
    {
        foreach (string candidate in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(candidate))
                    continue;
                return Load(candidate);
            }
            catch (Exception error) when (IsLoadFailure(error))
            {
                GD.PushWarning($"Cannot load {candidate}: {error.Message}");
            }
        }
        return null;
    }

    private static bool IsLoadFailure(Exception error) =>
        error is IOException or UnauthorizedAccessException or JsonException or NotSupportedException;

    /// <summary>
    /// Reads, migrates and validates one file; throws when it cannot be used. A file of version 5 or later has the
    /// sections of <see cref="SaveData"/>; an older one is the flat save of versions 1 to 4, which is migrated step by
    /// step and then moved into the sections. What the game of any version left behind is repaired before the save is
    /// validated, and the album and the roadside friends are brought up to date after it, as they always were.
    /// </summary>
    private static SaveData Load(string candidate)
    {
        string json = File.ReadAllText(candidate);
        var data = RawVersion(System.Text.Encoding.UTF8.GetBytes(json)) is >= SaveData.CurrentVersion
            ? JsonSerializer.Deserialize<SaveData>(json) ?? throw new JsonException("Empty save data.")
            : SaveMigrations.ToCurrent(SaveMigrations.ReadLegacy(json));
        if (SaveRules.MissesSection(data))
            throw new JsonException("A section of the save is missing.");
        SaveMigrations.Repair(data);
        SaveRules.Validate(data);
        SaveMigrations.AfterValidation(data);
        return data;
    }

    /// <summary>
    /// The first autosave moves the file into <c>.bak</c> and the next one replaces that, so an older save would be
    /// gone within seconds. Before that can happen, a save of an older version is copied to
    /// <c>progress.vN.bak</c> and a file that cannot be loaded at all to <c>progress.nenacteno-HASH.json</c>.
    /// Both copies are made once and never overwritten.
    /// </summary>
    internal static void Preserve(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;
            byte[] bytes = File.ReadAllBytes(path);
            string directory = Path.GetDirectoryName(path)!;
            string name = Path.GetFileNameWithoutExtension(path);
            try
            {
                Load(path);
                if (RawVersion(bytes) is int version && version < SaveData.CurrentVersion)
                    CopyOnce(path, Path.Combine(directory, $"{name}.v{version}.bak"));
            }
            catch (Exception error) when (IsLoadFailure(error))
            {
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..8];
                CopyOnce(path, Path.Combine(directory, $"{name}.nenacteno-{hash}.json"));
            }
        }
        catch (Exception error) when (IsLoadFailure(error))
        {
            GD.PushWarning($"Cannot keep a copy of {path}: {error.Message}");
        }
    }

    private static int? RawVersion(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.TryGetProperty("Version", out var version) && version.TryGetInt32(out int value)
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void CopyOnce(string source, string target)
    {
        if (!File.Exists(target))
            File.Copy(source, target);
    }

    internal static bool Write(SaveData data, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (
                var stream = new FileStream(path + ".tmp", FileMode.Create, System.IO.FileAccess.Write, FileShare.None)
            )
            {
                JsonSerializer.Serialize(stream, data);
                stream.Flush(true);
            }
            // Another program (a virus scanner, a backup) can hold the old file for a moment: try a few times.
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                        File.Replace(path + ".tmp", path, path + ".bak");
                    else
                        File.Move(path + ".tmp", path);
                    break;
                }
                catch (IOException) when (attempt < 5)
                {
                    System.Threading.Thread.Sleep(60 * attempt);
                }
            }
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Cannot save progress: {error.Message}");
            return false;
        }
    }
}
