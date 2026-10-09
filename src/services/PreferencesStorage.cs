using System.Text.Json;
using Velopack.Locators;

namespace VBAudioMonitor;

/// <summary>
/// Handles atomic loading and persistence of application preferences.
/// </summary>
internal static class PreferencesStorage
{
    internal const int MaximumFileBytes = 8 * 1024 * 1024;
    internal static readonly string StorageFilePath = ResolveStoragePath(AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        VelopackLocator.IsCurrentSet && VelopackLocator.Current.CurrentlyInstalledVersion != null && !VelopackLocator.Current.IsPortable);

    internal static string ResolveStoragePath(string applicationDirectory, string userDirectory, bool installed) =>
        Path.Combine(installed ? Path.Combine(userDirectory, "VBAudioMonitor") : applicationDirectory, "settings.json");

    /// <summary>
    /// Loads saved user preferences from disk.
    /// </summary>
    public static Preferences? LoadPreferences() => LoadPreferences(StorageFilePath);

    internal static Preferences? LoadPreferences(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                // Keep the file stable and reject oversized input before allocating its contents.
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length > MaximumFileBytes) return null;
                using var reader = new StreamReader(stream);
                string jsonPayload = reader.ReadToEnd();
                return JsonSerializer.Deserialize<Preferences>(jsonPayload);
            }
        }
        catch
        {
            // Fail silently on corrupted configuration to guarantee application startup.
        }

        return null;
    }

    /// <summary>
    /// Persists user preferences to disk with formatting.
    /// </summary>
    public static bool SavePreferences(Preferences preferencesToSave) => SavePreferences(preferencesToSave, StorageFilePath);

    internal static bool SavePreferences(Preferences preferencesToSave, string filePath)
    {
        string? ownedTemporaryPath = null;
        try
        {
            var serializerOptions = new JsonSerializerOptions { WriteIndented = true };
            byte[] serializedJson = JsonSerializer.SerializeToUtf8Bytes(preferencesToSave, serializerOptions);
            if (serializedJson.Length > MaximumFileBytes) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            string temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            // CreateNew never follows or overwrites a pre-existing temporary file or link.
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownedTemporaryPath = temporaryPath;
                stream.Write(serializedJson);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, filePath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (ownedTemporaryPath != null)
            {
                try { File.Delete(ownedTemporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
