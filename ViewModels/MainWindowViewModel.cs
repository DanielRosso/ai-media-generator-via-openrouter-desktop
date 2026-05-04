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
using vid_img_frontend_net_core.Models;
using vid_img_frontend_net_core.Services;

namespace vid_img_frontend_net_core.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly HttpClient _httpClient = new();
    private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";

    // ── API Key setup ─────────────────────────────────────────────────────────

    /// <summary>True = main UI visible; False = setup panel visible.</summary>
    [ObservableProperty]
    private bool _isApiKeyConfigured = false;

    /// <summary>Bound to the API key TextBox in the setup panel.</summary>
    [ObservableProperty]
    private string _apiKeyInput = string.Empty;

    /// <summary>Feedback shown in the setup panel.</summary>
    [ObservableProperty]
    private string _setupStatusMessage = string.Empty;

    // Cached key used for API calls (never exposed to the UI after saving)
    private string? _apiKey;

    // ── Media type ────────────────────────────────────────────────────────────

    public ObservableCollection<string> MediaTypes { get; } = ["Bild", "Video"];

    [ObservableProperty]
    private string _selectedMediaType = "Bild";

    partial void OnSelectedMediaTypeChanged(string value) => RefreshModelList();

    // ── Model selection ───────────────────────────────────────────────────────

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

    private string? _currentBase64Image;

    [ObservableProperty]
    private bool _canSaveImage = false;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindowViewModel()
    {
        RefreshModelList();
        CheckApiKey();
    }

    // ── API Key logic ─────────────────────────────────────────────────────────

    private void CheckApiKey()
    {
        _apiKey = ApiKeyService.Load();
        IsApiKeyConfigured = !string.IsNullOrWhiteSpace(_apiKey);
    }

    [RelayCommand]
    private void SaveApiKey()
    {
        var trimmed = ApiKeyInput.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            SetupStatusMessage = "⚠️ Bitte gib einen gültigen API-Key ein.";
            return;
        }

        ApiKeyService.Save(trimmed);
        _apiKey = trimmed;
        ApiKeyInput = string.Empty;
        SetupStatusMessage = string.Empty;
        IsApiKeyConfigured = true;
    }

    /// <summary>Allows the user to reset the stored key from the main UI.</summary>
    [RelayCommand]
    private void ResetApiKey()
    {
        ApiKeyService.Delete();
        _apiKey = null;
        GeneratedImage = null;
        CanSaveImage = false;
        _currentBase64Image = null;
        StatusMessage = string.Empty;
        IsApiKeyConfigured = false;
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

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            SetStatus("⚠️ Kein API-Key konfiguriert.", "#F38BA8");
            return;
        }

        IsLoading = true;
        CanSaveImage = false;
        _currentBase64Image = null;
        GeneratedImage = null;
        SetStatus("🔄 Verbinde mit OpenRouter API...", "#89B4FA");

        try
        {
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
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
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

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            string? base64Image = null;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message))
                {
                    if (message.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                    {
                        var firstImage = images[0];
                        if (firstImage.TryGetProperty("image_url", out var imageUrl) &&
                            imageUrl.TryGetProperty("url", out var urlProp))
                        {
                            base64Image = urlProp.GetString();
                        }
                    }

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

            var base64Data = base64Image.Contains(',')
                ? base64Image[(base64Image.IndexOf(',') + 1)..]
                : base64Image;

            var imageBytes = Convert.FromBase64String(base64Data);
            using var stream = new MemoryStream(imageBytes);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                GeneratedImage = new Bitmap(stream);
            });

            _currentBase64Image = base64Data;
            CanSaveImage = true;

            SetStatus("✅ Bild erfolgreich generiert! Klicke '💾 Bild speichern' zum Speichern.", "#A6E3A1");
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

    // ── Save helpers (called from code-behind) ────────────────────────────────

    public string? GetCurrentBase64Image() => _currentBase64Image;

    public void NotifySaved(string filePath)
        => SetStatus($"💾 Gespeichert: {filePath}", "#A6E3A1");

    public void NotifySaveFailed(string reason)
        => SetStatus($"❌ Speichern fehlgeschlagen: {reason}", "#F38BA8");

    private void SetStatus(string message, string color)
    {
        StatusMessage = message;
        StatusColor = color;
    }
}
