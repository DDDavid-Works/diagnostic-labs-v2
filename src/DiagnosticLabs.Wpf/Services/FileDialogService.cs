using System.IO;
using Microsoft.Win32;

namespace DiagnosticLabs.Wpf.Services;

public sealed record PickedFile(string Name, byte[] Content);

public interface IFileDialogService
{
    /// <summary>Lets the user choose an image file; <c>null</c> when they cancel.</summary>
    PickedFile? PickImage();
}

public sealed class FileDialogService : IFileDialogService
{
    public PickedFile? PickImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an image",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
            CheckFileExists = true,
        };

        return dialog.ShowDialog() == true
            ? new PickedFile(Path.GetFileName(dialog.FileName), File.ReadAllBytes(dialog.FileName))
            : null;
    }
}
