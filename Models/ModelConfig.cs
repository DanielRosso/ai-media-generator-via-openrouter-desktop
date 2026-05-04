namespace vid_img_frontend_net_core.Models;

/// <summary>
/// Zentrale Konfiguration aller verfügbaren KI-Modelle.
/// Füge hier einfach neue Modell-IDs hinzu – die UI aktualisiert sich automatisch.
/// </summary>
public static class ModelConfig
{
    /// <summary>
    /// Bildgenerierungs-Modelle (OpenRouter-Modell-IDs).
    /// </summary>
    public static readonly string[] ImageModels =
    [
        "black-forest-labs/flux-schnell",
        "black-forest-labs/flux-1.1-pro-ultra",
    ];

    /// <summary>
    /// Videogenerierungs-Modelle (OpenRouter-Modell-IDs).
    /// </summary>
    public static readonly string[] VideoModels =
    [
        "luma/ray-2-720p",
        "kwaivgi/kling-v3.0-pro",
    ];
}
