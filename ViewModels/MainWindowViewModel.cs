using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    // 10-minute timeout – video generation can take several minutes
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";

    // Matches http(s) URLs that look like video links (mp4, webm, or known CDN patterns)
    private static readonly Regex VideoUrlRegex = new(
        @"https?://[^\s\)\]""'<>]+(?:\.mp4|\.webm|\.mov|/video/[^\s\)\]""'<>]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Fallback: any bare https URL in the content
    private static readonly Regex AnyUrlRegex = new(
        @"https?://[^\s\)\]""'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── API Key setup ─────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isApiKeyConfigured = false;

    [ObservableProperty]
    private string _apiKeyInput = string.Empty;

    [ObservableProperty]
    private string _setupStatusMessage = string.Empty;

    private string? _apiKey;

    // ── Media type ────────────────────────────────────────────────────────────

    public ObservableCollection<string> MediaTypes { get; } = ["Bild", "Video"];

    [ObservableProperty]
    private string _selectedMediaType = "Bild";

    partial void OnSelectedMediaTypeChanged(string value)
    {
        RefreshModelList();
        // Clear previous result when switching type
        GeneratedImage = null;
        GeneratedVideoUrl = null;
        CanSaveImage = false;
        _currentBase64Image = null;
        StatusMessage = string.Empty;
    }

    // ── Model selection ───────────────────────────────────────────────────────

    public ObservableCollection<string> AvailableModels { get; } = [];

    [ObservableProperty]
    private string? _selectedModel;

    // ── Prompt / result ───────────────────────────────────────────────────────

    [ObservableProperty]
    private string _promptText = string.Empty;

    /// <summary>Set for image results; null for video results.</summary>
    [ObservableProperty]
    private Bitmap? _generatedImage;

    /// <summary>Set for video results; null for image results.</summary>
    [ObservableProperty]
    private string? _generatedVideoUrl;

    /// <summary>True when a video URL is available – shows the "open video" button.</summary>
    public bool HasVideoResult => !string.IsNullOrEmpty(GeneratedVideoUrl);

    partial void OnGeneratedVideoUrlChanged(string? value)
        => OnPropertyChanged(nameof(HasVideoResult));

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

    [RelayCommand]
    private void ResetApiKey()
    {
        ApiKeyService.Delete();
        _apiKey = null;
        GeneratedImage = null;
        GeneratedVideoUrl = null;
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

    private bool IsVideoMode => SelectedMediaType == "Video";

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
        GeneratedVideoUrl = null;

        var modeLabel = IsVideoMode ? "Video" : "Bild";
        SetStatus($"🔄 Verbinde mit OpenRouter API ({modeLabel}-Modus)...", "#89B4FA");

        try
        {
            // Same endpoint and payload for both image and video models
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

            if (IsVideoMode)
                SetStatus($"⏳ Video wird generiert mit '{SelectedModel}' – das kann mehrere Minuten dauern...", "#89B4FA");
            else
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

            // Extract the message content
            string? contentStr = null;
            string? base64Image = null;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message))
                {
                    // Image path: choices[0].message.images[0].image_url.url
                    if (!IsVideoMode &&
                        message.TryGetProperty("images", out var images) &&
                        images.GetArrayLength() > 0)
                    {
                        var firstImage = images[0];
                        if (firstImage.TryGetProperty("image_url", out var imageUrl) &&
                            imageUrl.TryGetProperty("url", out var urlProp))
                        {
                            base64Image = urlProp.GetString();
                        }
                    }

                    // Content field (used by video models and as image fallback)
                    if (message.TryGetProperty("content", out var contentProp))
                        contentStr = contentProp.GetString() ?? string.Empty;

                    // Image fallback: data URI in content
                    if (base64Image == null && !IsVideoMode &&
                        contentStr?.StartsWith("data:image") == true)
                    {
                        base64Image = contentStr;
                    }
                }
            }

            // ── VIDEO path ────────────────────────────────────────────────────
            if (IsVideoMode)
            {
                var videoUrl = ExtractVideoUrl(contentStr ?? string.Empty, responseBody);

                if (string.IsNullOrEmpty(videoUrl))
                {
                    SetStatus(
                        $"⚠️ Keine Video-URL in der Antwort gefunden.\n" +
                        $"Antwort: {responseBody[..Math.Min(600, responseBody.Length)]}",
                        "#FAB387");
                    return;
                }

                GeneratedVideoUrl = videoUrl;
                SetStatus($"✅ Video fertig! Klicke '▶ Video öffnen' um es im Browser anzusehen.", "#A6E3A1");
                return;
            }

            // ── IMAGE path ────────────────────────────────────────────────────
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
        catch (TaskCanceledException)
        {
            SetStatus("⏱️ Timeout: Die Anfrage hat zu lange gedauert. Bitte versuche es erneut.", "#F38BA8");
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

    // ── Video URL extraction ──────────────────────────────────────────────────

    private static string? ExtractVideoUrl(string content, string fullResponseBody)
    {
        // 1. Try to find a video-specific URL in the content field
        var match = VideoUrlRegex.Match(content);
        if (match.Success) return match.Value;

        // 2. Try any URL in the content field
        match = AnyUrlRegex.Match(content);
        if (match.Success) return match.Value;

        // 3. Search the entire raw response body as fallback
        match = VideoUrlRegex.Match(fullResponseBody);
        if (match.Success) return match.Value;

        match = AnyUrlRegex.Match(fullResponseBody);
        if (match.Success) return match.Value;

        return null;
    }

    // ── Open video in browser ─────────────────────────────────────────────────

    [RelayCommand]
    private void OpenVideoInBrowser()
    {
        if (string.IsNullOrEmpty(GeneratedVideoUrl)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GeneratedVideoUrl,
                UseShellExecute = true   // lets the OS pick the default browser
            });
        }
        catch (Exception ex)
        {
            SetStatus($"❌ Browser konnte nicht geöffnet werden: {ex.Message}", "#F38BA8");
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
