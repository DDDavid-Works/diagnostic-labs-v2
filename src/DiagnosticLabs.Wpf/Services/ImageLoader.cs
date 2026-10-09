using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DiagnosticLabs.Wpf.Services;

public static class ImageLoader
{
    /// <summary>Turns stored image bytes into something an <c>Image</c> can show; <c>null</c> when there are none or they can not be decoded.</summary>
    public static ImageSource? FromBytes(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
            return null;

        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad; // decode now so the stream can be closed
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or InvalidOperationException)
        {
            return null;
        }
    }
}
