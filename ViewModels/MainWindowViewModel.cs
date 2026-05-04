using System;
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

namespace vid_img_frontend_net_core.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly HttpClient _httpClient = new();
    private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";
    private const string DefaultModel = "black-forest-labs/flux-schnell";

    [ObservableProperty]
    private string _promptText = string.Empty;

    [ObservableProperty]
    private int _selectedMediaTypeIndex = 0; // 0 = Bild, 1 = Video

    [ObservableProperty]
    private Bitmap? _generatedImage;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _statusColor = "#A6ADC8";

    private string LoadApiKey()
    {
        // Look for secrets.json next to the executable, then in the project root
        var exeDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(exeDir, "secrets.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secrets.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "secrets.json"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile(path, optional: false, reloadOnChange: false)
                    .Build();

                var key = config["OpenRouter:ApiKey"];
                if (!string.IsNullOrWhiteSpace(key) && key != "YOUR_OPENROUTER_API_KEY_HERE")
                    return key;
            }
        }

        throw new InvalidOperationException(
            "API-Key nicht gefunden. Bitte trage deinen OpenRouter API-Key in die Datei 'secrets.json' ein " +
            "(Feld: OpenRouter:ApiKey) und stelle sicher, dass die Datei neben der ausführbaren Datei liegt.");
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(PromptText))
        {
            SetStatus("⚠️ Bitte gib einen Prompt ein.", "#F38BA8");
            return;
        }

        IsLoading = true;
        GeneratedImage = null;
        SetStatus("🔄 Verbinde mit OpenRouter API...", "#89B4FA");

        try
        {
            var apiKey = LoadApiKey();

            // Build the request payload
            var requestBody = new
            {
                model = DefaultModel,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = PromptText
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("HTTP-Referer", "https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop");
            request.Headers.Add("X-Title", "Media Generator");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            SetStatus("⏳ Anfrage wird gesendet...", "#89B4FA");

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"❌ API-Fehler {(int)response.StatusCode}: {response.ReasonPhrase}\n{responseBody}", "#F38BA8");
                return;
            }

            // Parse the response and extract the Base64 image
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string? base64Image = null;

            // Try: data.choices[0].message.images[0].image_url.url  (OpenRouter flux-schnell format)
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message))
                {
                    // Check for images array (flux-schnell specific)
                    if (message.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                    {
                        var firstImage = images[0];
                        if (firstImage.TryGetProperty("image_url", out var imageUrl) &&
                            imageUrl.TryGetProperty("url", out var urlProp))
                        {
                            base64Image = urlProp.GetString();
                        }
                    }

                    // Fallback: content field might contain a data URI
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
                SetStatus($"⚠️ Kein Bild in der Antwort gefunden.\nAntwort: {responseBody[..Math.Min(500, responseBody.Length)]}", "#FAB387");
                return;
            }

            // Strip data URI prefix if present (e.g. "data:image/png;base64,...")
            var base64Data = base64Image;
            if (base64Data.Contains(','))
                base64Data = base64Data[(base64Data.IndexOf(',') + 1)..];

            var imageBytes = Convert.FromBase64String(base64Data);
            using var stream = new MemoryStream(imageBytes);

            // Must update UI on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                GeneratedImage = new Bitmap(stream);
            });

            SetStatus("✅ Bild erfolgreich generiert!", "#A6E3A1");
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

    private void SetStatus(string message, string color)
    {
        StatusMessage = message;
        StatusColor = color;
    }
}
