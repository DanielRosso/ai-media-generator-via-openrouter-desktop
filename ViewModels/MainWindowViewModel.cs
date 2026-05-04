using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using vid_img_frontend_net_core.Models;

namespace vid_img_frontend_net_core.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly HttpClient _httpClient = new();
    private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";

    // ── Media type ────────────────────────────────────────────────────────────

    /// <summary>Available media types shown in the first ComboBox.</summary>
    public ObservableCollection<string> MediaTypes { get; } = ["Bild", "Video"];

    [ObservableProperty]
    private string _selectedMediaType = "Bild";

    // When the media type changes, refresh the model list.
    partial void OnSelectedMediaTypeChanged(string value)
    {
        RefreshModelList();
    }

    // ── Model selection ───────────────────────────────────────────────────────

    /// <summary>Model IDs for the currently selected media type.</summary>
    public ObservableCollection<string> AvailableModels { get; } = [];

    [ObservableProperty]
    private string? _selectedModel;

    // ── Prompt / result ───────────────────────────────────────────────────────

    [ObservableProperty]
    private string _promptText = string.Empty;

    [ObservableProperty]
    private Bitmap? _generatedImage;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _statusColor = "#A6ADC8";

    /// <summary>Raw Base64 string of the last successfully generated image.</summary>
    private string? _currentBase64Image;

    /// <summary>True once a valid image has been generated – enables the Save button.</summary>
    [ObservableProperty]
    private bool _canSaveImage = false;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindowViewModel()
    {
        RefreshModelList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshModelList()
    {
        var models = SelectedMediaType == "Video"
            ? ModelConfig.VideoModels
            : ModelConfig.ImageModels;

        AvailableModels.Clear();
        foreach (var m in models)
            AvailableModels.Add(m);

        SelectedModel = AvailableModels.Count > 0 ? AvailableModels[0] : null;
    }

    private string LoadApiKey()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "secrets.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secrets.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "secrets.json"),
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;

            var config = new ConfigurationBuilder()
                .AddJsonFile(path, optional: false, reloadOnChange: false)
                .Build();

            var key = config["OpenRouter:ApiKey"];
            if (!string.IsNullOrWhiteSpace(key) && key != "YOUR_OPENROUTER_API_KEY_HERE")
                return key;
        }

        throw new InvalidOperationException(
            "API-Key nicht gefunden. Bitte trage deinen OpenRouter API-Key in die Datei " +
            "'secrets.json' ein (Feld: OpenRouter:ApiKey) und stelle sicher, dass die Datei " +
            "neben der ausführbaren Datei liegt.");
    }

    // ── Generate command ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(PromptText))
        {
            SetStatus("⚠️ Bitte gib einen Prompt ein.", "#F38BA8");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedModel))
        {
            SetStatus("⚠️ Bitte wähle ein Modell aus.", "#F38BA8");
            return;
        }

        IsLoading = true;
        CanSaveImage = false;
        _currentBase64Image = null;
        GeneratedImage = null;
        SetStatus("🔄 Verbinde mit OpenRouter API...", "#89B4FA");

        try
        {
            var apiKey = LoadApiKey();

            var requestBody = new
            {
                model = SelectedModel,
                messages = new[]
                {
                    new { role = "user", content = PromptText }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("HTTP-Referer", "https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop");
            request.Headers.Add("X-Title", "Media Generator");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            SetStatus($"⏳ Sende Anfrage an Modell '{SelectedModel}'...", "#89B4FA");

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"❌ API-Fehler {(int)response.StatusCode}: {response.ReasonPhrase}\n{responseBody}", "#F38BA8");
                return;
            }

            // Parse response – extract Base64 image
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            string? base64Image = null;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message))
                {
                    // flux-schnell / image models: choices[0].message.images[0].image_url.url
                    if (message.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                    {
                        var firstImage = images[0];
                        if (firstImage.TryGetProperty("image_url", out var imageUrl) &&
                            imageUrl.TryGetProperty("url", out var urlProp))
                        {
                            base64Image = urlProp.GetString();
                        }
                    }

                    // Fallback: content field contains a data URI
                    if (base64Image == null && message.TryGetProperty("content", out var content))
                    {
                        var contentStr = content.GetString() ?? string.Empty;
                        if (contentStr.StartsWith("data:image"))
                            base64Image = contentStr;
                    }
                }
            }

            if (string.IsNullOrEmpty(base64Image))
            {
                SetStatus(
                    $"⚠️ Kein Bild in der Antwort gefunden.\n" +
                    $"Antwort: {responseBody[..Math.Min(500, responseBody.Length)]}",
                    "#FAB387");
                return;
            }

            // Strip optional data URI prefix  (e.g. "data:image/png;base64,…")
            var base64Data = base64Image.Contains(',')
                ? base64Image[(base64Image.IndexOf(',') + 1)..]
                : base64Image;

            var imageBytes = Convert.FromBase64String(base64Data);
            using var stream = new MemoryStream(imageBytes);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                GeneratedImage = new Bitmap(stream);
            });

            // Store the clean Base64 data so the Save command can write it to disk
            _currentBase64Image = base64Data;
            CanSaveImage = true;

            SetStatus("✅ Bild erfolgreich generiert! Klicke '💾 Bild speichern' zum Speichern.", "#A6E3A1");
        }
        catch (InvalidOperationException ex)
        {
            SetStatus($"⚙️ Konfigurationsfehler: {ex.Message}", "#F38BA8");
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"🌐 Netzwerkfehler: {ex.Message}", "#F38BA8");
        }
        catch (Exception ex)
        {
            SetStatus($"❌ Unerwarteter Fehler: {ex.Message}", "#F38BA8");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Save command (called from code-behind with StorageProvider) ────────────

    /// <summary>
    /// Exposes the current Base64 image data to the View's code-behind,
    /// which handles the platform StorageProvider dialog.
    /// </summary>
    public string? GetCurrentBase64Image() => _currentBase64Image;

    /// <summary>Called by the View after a successful save to update the status bar.</summary>
    public void NotifySaved(string filePath)
        => SetStatus($"💾 Gespeichert: {filePath}", "#A6E3A1");

    /// <summary>Called by the View if the save dialog was cancelled or failed.</summary>
    public void NotifySaveFailed(string reason)
        => SetStatus($"❌ Speichern fehlgeschlagen: {reason}", "#F38BA8");

    private void SetStatus(string message, string color)
    {
        StatusMessage = message;
        StatusColor = color;
    }
}
