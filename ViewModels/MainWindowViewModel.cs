using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private const string VideoApiUrl = "https://openrouter.ai/api/v1/videos";

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
        OnPropertyChanged(nameof(IsVideoMode));

        if (!IsVideoMode)
        {
            ReferenceImageDataUrl = null;
            ReferenceImageName = string.Empty;
        }

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

    // ── Video reference image ────────────────────────────────────────────────

    [ObservableProperty]
    private string? _referenceImageDataUrl;

    [ObservableProperty]
    private string _referenceImageName = string.Empty;

    public bool HasReferenceImage => !string.IsNullOrWhiteSpace(ReferenceImageDataUrl);

    partial void OnReferenceImageDataUrlChanged(string? value)
        => OnPropertyChanged(nameof(HasReferenceImage));

    // ── Debug log ─────────────────────────────────────────────────────────────

    /// <summary>Full error protocol shown in the UI debug panel.</summary>
    [ObservableProperty]
    private string _debugLog = string.Empty;

    /// <summary>True when a debug log entry is available to display.</summary>
    public bool HasDebugLog => !string.IsNullOrEmpty(DebugLog);

    partial void OnDebugLogChanged(string value)
        => OnPropertyChanged(nameof(HasDebugLog));

    [RelayCommand]
    private void ClearDebugLog() => DebugLog = string.Empty;

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

    public bool IsVideoMode => SelectedMediaType == "Video";

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
        DebugLog = string.Empty;

        var modeLabel = IsVideoMode ? "Video" : "Bild";
        SetStatus($"🔄 Verbinde mit OpenRouter API ({modeLabel}-Modus)...", "#89B4FA");

        try
        {
            if (IsVideoMode)
            {
                await GenerateVideoAsync(SelectedModel!, PromptText, _apiKey!);
            }
            else
            {
                await GenerateImageAsync(SelectedModel!, PromptText, _apiKey!);
            }
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

    // ── Video generation: POST /videos → poll until completed ─────────────────

    private async Task GenerateVideoAsync(string model, string prompt, string apiKey)
    {
        // ── Step 1: Start the job ─────────────────────────────────────────────
        // Build models array: selected first, then all others as fallback
        var allModels = ModelConfig.VideoModels;
        var modelsList = new List<string> { model };
        modelsList.AddRange(allModels.Where(m => m != model));

        var startBody = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["models"] = modelsList,
            ["prompt"] = prompt
        };

        if (!string.IsNullOrWhiteSpace(ReferenceImageDataUrl))
        {
            startBody["input_references"] = new object[]
            {
                new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = ReferenceImageDataUrl
                    }
                }
            };
        }

        var startPayload = JsonSerializer.Serialize(startBody, new JsonSerializerOptions { WriteIndented = true });

        using var startRequest = new HttpRequestMessage(HttpMethod.Post, VideoApiUrl);
        startRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        startRequest.Headers.Add("HTTP-Referer", "https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop");
        startRequest.Headers.Add("X-Title", "Media Generator");
        startRequest.Content = new StringContent(startPayload, Encoding.UTF8, "application/json");

        SetStatus($"⏳ Starte Video-Job mit Modell '{model}'...", "#89B4FA");

        using var startResponse = await _httpClient.SendAsync(startRequest);
        var startBody2 = await startResponse.Content.ReadAsStringAsync();

        if (!startResponse.IsSuccessStatusCode)
        {
            var code = (int)startResponse.StatusCode;
            SetStatus($"❌ API-Fehler {code}: {startResponse.ReasonPhrase}", "#F38BA8");
            DebugLog =
                $"═══ HTTP-FEHLERPROTOKOLL (Video-Start) ═══\n" +
                $"Zeitstempel : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"Endpunkt    : POST {VideoApiUrl}\n" +
                $"Modell      : {model}\n" +
                $"Statuscode  : {code} {startResponse.ReasonPhrase}\n\n" +
                $"── Gesendeter Payload ──────────────────────────────────\n" +
                $"{startPayload}\n\n" +
                $"── Antwort von OpenRouter ──────────────────────────────\n" +
                $"{startBody2}";
            return;
        }

        // Extract polling_url from the start response
        using var startDoc = JsonDocument.Parse(startBody2);
        var pollingUrl = startDoc.RootElement
            .TryGetProperty("polling_url", out var pollingProp)
            ? pollingProp.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(pollingUrl))
        {
            SetStatus("⚠️ Keine polling_url in der Start-Antwort gefunden.", "#FAB387");
            DebugLog =
                $"═══ FEHLENDE polling_url ═══\n" +
                $"Zeitstempel : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"Endpunkt    : POST {VideoApiUrl}\n\n" +
                $"── Start-Antwort ───────────────────────────────────────\n" +
                $"{startBody2}";
            return;
        }

        // ── Step 2: Poll until completed or failed ────────────────────────────
        var pollCount = 0;
        while (true)
        {
            pollCount++;
            SetStatus($"🔄 Warte auf Video... (Abfrage #{pollCount}, alle 5 Sek.)", "#89B4FA");

            await Task.Delay(5000);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, pollingUrl);
            pollRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            pollRequest.Headers.Add("HTTP-Referer", "https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop");
            pollRequest.Headers.Add("X-Title", "Media Generator");

            using var pollResponse = await _httpClient.SendAsync(pollRequest);
            var pollBody = await pollResponse.Content.ReadAsStringAsync();

            if (!pollResponse.IsSuccessStatusCode)
            {
                var code = (int)pollResponse.StatusCode;
                SetStatus($"❌ Polling-Fehler {code}: {pollResponse.ReasonPhrase}", "#F38BA8");
                DebugLog =
                    $"═══ HTTP-FEHLERPROTOKOLL (Polling) ═══\n" +
                    $"Zeitstempel : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Endpunkt    : GET {pollingUrl}\n" +
                    $"Statuscode  : {code} {pollResponse.ReasonPhrase}\n\n" +
                    $"── Antwort ─────────────────────────────────────────────\n" +
                    $"{pollBody}";
                return;
            }

            using var pollDoc = JsonDocument.Parse(pollBody);
            var pollRoot = pollDoc.RootElement;

            var status = pollRoot.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? string.Empty
                : string.Empty;

            if (status == "completed")
            {
                // Extract first URL from unsigned_urls array
                string? videoUrl = null;
                if (pollRoot.TryGetProperty("unsigned_urls", out var urlsArr) &&
                    urlsArr.ValueKind == JsonValueKind.Array &&
                    urlsArr.GetArrayLength() > 0)
                {
                    videoUrl = urlsArr[0].GetString();
                }

                if (string.IsNullOrWhiteSpace(videoUrl))
                {
                    SetStatus("⚠️ Video fertig, aber keine URL in 'unsigned_urls' gefunden.", "#FAB387");
                    DebugLog =
                        $"═══ FEHLENDE VIDEO-URL ═══\n" +
                        $"Status war 'completed', aber unsigned_urls ist leer.\n\n" +
                        $"── Polling-Antwort ─────────────────────────────────────\n" +
                        $"{pollBody}";
                    return;
                }

                GeneratedVideoUrl = videoUrl;
                SetStatus("✅ Video fertig! Klicke '▶ Video öffnen' um es im Browser anzusehen.", "#A6E3A1");
                return;
            }

            if (status == "failed")
            {
                var errorMsg = pollRoot.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() ?? "Unbekannter Fehler"
                    : "Unbekannter Fehler";

                SetStatus($"❌ Video-Generierung fehlgeschlagen: {errorMsg}", "#F38BA8");
                DebugLog =
                    $"═══ VIDEO-JOB FEHLGESCHLAGEN ═══\n" +
                    $"Zeitstempel : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Modell      : {model}\n" +
                    $"Fehler      : {errorMsg}\n\n" +
                    $"── Polling-Antwort ─────────────────────────────────────\n" +
                    $"{pollBody}";
                return;
            }

            // status is "pending", "processing", etc. – keep polling
        }
    }

    // ── Image generation (chat completions endpoint) ──────────────────────────

    private async Task GenerateImageAsync(string model, string prompt, string apiKey)
    {

        // Build models array: selected first, then all others as fallback
        var allModels = ModelConfig.ImageModels;
        var modelsList = new List<string> { model };
        modelsList.AddRange(allModels.Where(m => m != model));

        var requestBody = new
        {
            model,
            models = modelsList,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var requestPayload = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { WriteIndented = true });

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("HTTP-Referer", "https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop");
        request.Headers.Add("X-Title", "Media Generator");
        request.Content = new StringContent(requestPayload, Encoding.UTF8, "application/json");

        SetStatus($"⏳ Sende Anfrage an Modell '{model}'...", "#89B4FA");

        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            SetStatus($"❌ API-Fehler {statusCode}: {response.ReasonPhrase}", "#F38BA8");
            DebugLog =
                $"═══ HTTP-FEHLERPROTOKOLL ═══\n" +
                $"Zeitstempel : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"Endpunkt    : POST {ApiUrl}\n" +
                $"Modell      : {model}\n" +
                $"Statuscode  : {statusCode} {response.ReasonPhrase}\n\n" +
                $"── Gesendeter Payload ──────────────────────────────────\n" +
                $"{requestPayload}\n\n" +
                $"── Antwort von OpenRouter ──────────────────────────────\n" +
                $"{responseBody}";
            return;
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        string? base64Image = null;
        string? contentStr = null;

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

                if (message.TryGetProperty("content", out var contentProp))
                    contentStr = contentProp.GetString() ?? string.Empty;

                if (base64Image == null && contentStr?.StartsWith("data:image") == true)
                    base64Image = contentStr;
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

    // ── Authenticated video download (called from code-behind) ────────────────

    /// <summary>
    /// Downloads the video from <see cref="GeneratedVideoUrl"/> using the stored
    /// API key (required – the URL is not publicly accessible) and writes it to
    /// <paramref name="destinationStream"/>.
    /// </summary>
    public async Task DownloadVideoToStreamAsync(Stream destinationStream)
    {
        if (string.IsNullOrEmpty(GeneratedVideoUrl))
            throw new InvalidOperationException("Keine Video-URL vorhanden.");

        SetStatus("⏳ Lade Video herunter...", "#89B4FA");

        using var request = new HttpRequestMessage(HttpMethod.Get, GeneratedVideoUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey ?? string.Empty);
        request.Headers.Add("HTTP-Referer", "https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop");
        request.Headers.Add("X-Title", "Media Generator");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Download fehlgeschlagen: {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
        }

        await using var videoStream = await response.Content.ReadAsStreamAsync();
        await videoStream.CopyToAsync(destinationStream);
    }

    public void NotifyVideoSaved(string filePath)
        => SetStatus($"✅ Video erfolgreich gespeichert: {filePath}", "#A6E3A1");

    public void NotifyVideoDownloadFailed(string reason)
        => SetStatus($"❌ Download fehlgeschlagen: {reason}", "#F38BA8");

    // ── Save helpers (called from code-behind) ────────────────────────────────

    public string? GetCurrentBase64Image() => _currentBase64Image;

    public void SetReferenceImage(string fileName, string dataUrl)
    {
        ReferenceImageName = fileName;
        ReferenceImageDataUrl = dataUrl;
        SetStatus($"🖼️ Referenzbild gesetzt: {fileName}", "#A6E3A1");
    }

    public void ClearReferenceImage()
    {
        ReferenceImageName = string.Empty;
        ReferenceImageDataUrl = null;
        SetStatus("🧹 Referenzbild entfernt.", "#A6ADC8");
    }

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
