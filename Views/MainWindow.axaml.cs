using System;
using System.Diagnostics;
using System.IO;
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
