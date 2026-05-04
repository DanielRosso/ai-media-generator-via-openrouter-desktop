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
        "black-forest-labs/flux.2-pro",
        "bytedance-seed/seedream-4.5",
    ];

    /// <summary>
    /// Videogenerierungs-Modelle (OpenRouter-Modell-IDs).
    /// </summary>
    public static readonly string[] VideoModels =
    [
        "bytedance/seedance-2.0",
        "kwaivgi/kling-v3.0-std",
    ];
}
