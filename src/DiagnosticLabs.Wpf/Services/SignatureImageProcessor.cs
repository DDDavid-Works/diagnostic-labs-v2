using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiagnosticLabs.Application.Entries;

namespace DiagnosticLabs.Wpf.Services;

/// <summary>
/// Turns a scan or photo of a signature into what is stored and printed: the paper made transparent (so the signature sits on the line of the
/// printout like ink), the empty margins trimmed off, and shrunk to fit <see cref="SignatureImages.MaxWidth"/> x <see cref="SignatureImages.MaxHeight"/>.
/// The ink keeps its colour (blue stays blue).
/// </summary>
public static class SignatureImageProcessor
{
    /// <summary>Larger pictures (phone photos) are first brought down to this width, which is plenty for a signature and keeps the work quick.</summary>
    private const int WorkingWidth = 1800;

    /// <summary>A pixel counts as ink for the trimming when it is at least this opaque.</summary>
    private const byte InkAlpha = 40;

    /// <summary>The prepared PNG, or <c>null</c> when the file is not a picture or has no ink on it.</summary>
    public static byte[]? Prepare(byte[] file)
    {
        var source = Decode(file);
        if (source is null)
            return null;

        if (source.PixelWidth > WorkingWidth)
        {
            var scale = (double)WorkingWidth / source.PixelWidth;
            source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        }

        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = bgra.PixelWidth, height = bgra.PixelHeight, stride = width * 4;
        var pixels = new byte[stride * height];
        bgra.CopyPixels(pixels, stride, 0);

        ClearPaper(pixels);
        if (InkBounds(pixels, width, height) is not { } box)
            return null;

        // A little room around the ink, then only the box is kept.
        const int pad = 4;
        var x0 = Math.Max(0, box.X0 - pad);
        var y0 = Math.Max(0, box.Y0 - pad);
        var x1 = Math.Min(width - 1, box.X1 + pad);
        var y1 = Math.Min(height - 1, box.Y1 + pad);
        BitmapSource result = new CroppedBitmap(
            BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride),
            new System.Windows.Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1));

        var fit = Math.Min(1.0, Math.Min((double)SignatureImages.MaxWidth / result.PixelWidth, (double)SignatureImages.MaxHeight / result.PixelHeight));
        if (fit < 1.0)
            result = new TransformedBitmap(result, new ScaleTransform(fit, fit));

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(result));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static BitmapSource? Decode(byte[] file)
    {
        try
        {
            using var stream = new MemoryStream(file);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad).Frames[0];
            frame.Freeze();
            return frame;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or InvalidOperationException or ArgumentException or IOException)
        {
            return null;
        }
    }

    private static int Lightness(byte[] p, int i) => ((p[i + 2] * 299) + (p[i + 1] * 587) + (p[i] * 114)) / 1000;

    /// <summary>
    /// The paper becomes transparent and the ink opaque, with a soft step between them so the strokes keep smooth edges. The paper's own
    /// brightness is measured (a photo of white paper is often grey), so the threshold follows it.
    /// </summary>
    private static void ClearPaper(byte[] p)
    {
        var histogram = new int[256];
        var counted = 0;
        for (var i = 0; i < p.Length; i += 4)
        {
            if (p[i + 3] < 128)
                continue;
            histogram[Lightness(p, i)]++;
            counted++;
        }

        // The paper is the bright majority: the lightness that 90% of the visible pixels are at or below.
        var paper = 255;
        for (int level = 0, sum = 0; level < 256 && counted > 0; level++)
        {
            sum += histogram[level];
            if (sum >= counted * 0.9)
            {
                paper = level;
                break;
            }
        }

        double light = Math.Max(paper - 30, 40), dark = Math.Max(light - 100, 0);
        for (var i = 0; i < p.Length; i += 4)
        {
            var lightness = Lightness(p, i);
            var ink = lightness >= light ? 0.0 : lightness <= dark ? 1.0 : (light - lightness) / (light - dark);
            p[i + 3] = (byte)Math.Round(p[i + 3] * ink);
        }
    }

    private static (int X0, int Y0, int X1, int Y1)? InkBounds(byte[] p, int width, int height)
    {
        int x0 = width, y0 = height, x1 = -1, y1 = -1;
        for (var y = 0; y < height; y++)
        {
            var row = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                if (p[row + (x * 4) + 3] < InkAlpha)
                    continue;
                x0 = Math.Min(x0, x);
                x1 = Math.Max(x1, x);
                y0 = Math.Min(y0, y);
                y1 = Math.Max(y1, y);
            }
        }

        return x1 < 0 ? null : (x0, y0, x1, y1);
    }
}
