using System.Text.Json;
using Velopack.Locators;

namespace VBAudioMonitor;

/// <summary>
/// Handles atomic loading and persistence of application preferences.
/// </summary>
internal static class PreferencesStorage
{
    internal static readonly string StorageFilePath = ResolveStoragePath(AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        VelopackLocator.IsCurrentSet && VelopackLocator.Current.CurrentlyInstalledVersion != null && !VelopackLocator.Current.IsPortable);

    internal static string ResolveStoragePath(string applicationDirectory, string userDirectory, bool installed) =>
        Path.Combine(installed ? Path.Combine(userDirectory, "VBAudioMonitor") : applicationDirectory, "settings.json");

    /// <summary>
    /// Loads saved user preferences from disk.
    /// </summary>
    public static Preferences? LoadPreferences()
    {
        try
        {
            if (File.Exists(StorageFilePath))
            {
                string jsonPayload = File.ReadAllText(StorageFilePath);
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
    public static bool SavePreferences(Preferences preferencesToSave)
    {
        try
        {
            var serializerOptions = new JsonSerializerOptions { WriteIndented = true };
            string serializedJson = JsonSerializer.Serialize(preferencesToSave, serializerOptions);
            Directory.CreateDirectory(Path.GetDirectoryName(StorageFilePath)!);
            string temporaryPath = StorageFilePath + ".tmp";
            File.WriteAllText(temporaryPath, serializedJson);
            File.Move(temporaryPath, StorageFilePath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
