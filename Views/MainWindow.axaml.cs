using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using vid_img_frontend_net_core.ViewModels;

namespace vid_img_frontend_net_core.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens a native Save File dialog via Avalonia's StorageProvider API,
    /// then writes the current Base64 image bytes to the chosen file.
    /// </summary>
    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var base64Data = vm.GetCurrentBase64Image();
        if (string.IsNullOrEmpty(base64Data))
            return;

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
                    new FilePickerFileType("PNG-Bild") { Patterns = ["*.png"] },
                    new FilePickerFileType("JPEG-Bild") { Patterns = ["*.jpg", "*.jpeg"] },
                    new FilePickerFileType("Alle Dateien") { Patterns = ["*"] },
                ]
            });

            if (file is null)
                return; // user cancelled

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
}
