using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using vid_img_frontend_net_core.ViewModels;

namespace vid_img_frontend_net_core.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // ── Reference image upload (video mode) ─────────────────────────────────

    private async void UploadReferenceImageButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this)
                ?? throw new InvalidOperationException("TopLevel konnte nicht ermittelt werden.");

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Referenzbild auswählen",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Bilddateien")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"]
                    }
                ]
            });

            var file = files.FirstOrDefault();
            if (file is null) return;

            await using var fileStream = await file.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);

            var bytes = memoryStream.ToArray();
            var base64 = Convert.ToBase64String(bytes);
            var mimeType = GetImageMimeType(file.Name);
            var dataUrl = $"data:{mimeType};base64,{base64}";

            vm.SetReferenceImage(file.Name, dataUrl);
        }
        catch (Exception ex)
        {
            vm.NotifyVideoDownloadFailed($"Referenzbild konnte nicht geladen werden: {ex.Message}");
        }
    }

    private void ClearReferenceImageButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.ClearReferenceImage();
    }

    private static string GetImageMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png"
        };
    }

    // ── Save image ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a native Save File dialog and writes the current Base64 image bytes
    /// to the chosen file.
    /// </summary>
    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var base64Data = vm.GetCurrentBase64Image();
        if (string.IsNullOrEmpty(base64Data)) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this)
                ?? throw new InvalidOperationException("TopLevel konnte nicht ermittelt werden.");

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Bild speichern",
                SuggestedFileName = "generiertes-bild.png",
                DefaultExtension = "png",
                FileTypeChoices =
                [
                    new FilePickerFileType("PNG-Bild")  { Patterns = ["*.png"] },
                    new FilePickerFileType("JPEG-Bild") { Patterns = ["*.jpg", "*.jpeg"] },
                    new FilePickerFileType("Alle Dateien") { Patterns = ["*"] },
                ]
            });

            if (file is null) return; // user cancelled

            var imageBytes = Convert.FromBase64String(base64Data);
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(imageBytes);

            vm.NotifySaved(file.Name);
        }
        catch (Exception ex)
        {
            if (DataContext is MainWindowViewModel viewModel)
                viewModel.NotifySaveFailed(ex.Message);
        }
    }

    // ── Download video ────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a native Save File dialog, downloads the video from OpenRouter
    /// using the stored API key (authenticated GET), writes it to the chosen
    /// local file, then opens it with the system's default media player.
    /// </summary>
    private async void DownloadVideoButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this)
                ?? throw new InvalidOperationException("TopLevel konnte nicht ermittelt werden.");

            // Ask the user where to save the video
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Video speichern",
                SuggestedFileName = "generiertes-video.mp4",
                DefaultExtension = "mp4",
                FileTypeChoices =
                [
                    new FilePickerFileType("MP4-Video") { Patterns = ["*.mp4"] },
                    new FilePickerFileType("Alle Dateien") { Patterns = ["*"] },
                ]
            });

            if (file is null) return; // user cancelled

            // Download the video (authenticated) and write to the chosen file
            await using var fileStream = await file.OpenWriteAsync();
            await vm.DownloadVideoToStreamAsync(fileStream);

            // Get the local path so we can open it with the media player
            var localPath = file.TryGetLocalPath();
            vm.NotifyVideoSaved(localPath ?? file.Name);

            // Open the saved file with the system's default media player
            if (!string.IsNullOrEmpty(localPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = localPath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            if (DataContext is MainWindowViewModel viewModel)
                viewModel.NotifyVideoDownloadFailed(ex.Message);
        }
    }
}
