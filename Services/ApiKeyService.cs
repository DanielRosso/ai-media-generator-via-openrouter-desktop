using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace vid_img_frontend_net_core.Services;

/// <summary>
/// Persists the OpenRouter API key in the user's AppData folder.
/// Path: %APPDATA%\MediaGenerator\config.json  (cross-platform equivalent on macOS/Linux)
/// </summary>
public static class ApiKeyService
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MediaGenerator");

    private static readonly string ConfigFile =
        Path.Combine(ConfigDir, "config.json");

    private const string KeyField = "ApiKey";

    /// <summary>Loads the stored API key, or returns null if none is saved yet.</summary>
    public static string? Load()
    {
        if (!File.Exists(ConfigFile))
            return null;

        try
        {
            var json = File.ReadAllText(ConfigFile);
            var node = JsonNode.Parse(json);
            var key = node?[KeyField]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Saves the API key to the user's AppData folder.</summary>
    public static void Save(string apiKey)
    {
        Directory.CreateDirectory(ConfigDir);

        var obj = new JsonObject { [KeyField] = apiKey };
        File.WriteAllText(ConfigFile, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Deletes the stored config (useful for "reset key" scenarios).</summary>
    public static void Delete()
    {
        if (File.Exists(ConfigFile))
            File.Delete(ConfigFile);
    }
}
