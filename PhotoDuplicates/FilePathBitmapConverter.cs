using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace PhotoDuplicates;

/// <summary>Convierte una ruta de archivo local en <see cref="BitmapImage"/> (miniatura vía ConverterParameter = ancho DecodePixelWidth).</summary>
public sealed class FilePathBitmapConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return DependencyProperty.UnsetValue;

        try
        {
            if (!File.Exists(path))
                return DependencyProperty.UnsetValue;

            var full = Path.GetFullPath(path);
            var uri = new Uri(full, UriKind.Absolute);

            var bmp = new BitmapImage();
            if (parameter is string s && int.TryParse(s, out var w) && w > 0)
                bmp.DecodePixelWidth = w;
            bmp.UriSource = uri;
            return bmp;
        }
        catch
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
