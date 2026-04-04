using System.Collections.ObjectModel;
using System.ComponentModel;
namespace PhotoDuplicates;

public sealed class DuplicateFileItem : INotifyPropertyChanged
{
    private bool _markedForDeletion;

    public DuplicateFileItem(string filePath, bool markedForDeletion)
    {
        FilePath = filePath;
        _markedForDeletion = markedForDeletion;
    }

    /// <summary>Ruta completa del archivo (no usar el nombre "Path" en XAML: choca con Shapes.Path).</summary>
    public string FilePath { get; }

    public bool MarkedForDeletion
    {
        get => _markedForDeletion;
        set
        {
            if (_markedForDeletion == value)
                return;
            _markedForDeletion = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkedForDeletion)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class DuplicateGroupVm
{
    public DuplicateGroupVm(string fingerprintHex, ObservableCollection<DuplicateFileItem> files)
    {
        FingerprintHex = fingerprintHex;
        Files = files;
    }

    public string FingerprintHex { get; }
    public string FingerprintShort => FingerprintHex.Length <= 24 ? FingerprintHex : FingerprintHex[..24] + "...";
    public ObservableCollection<DuplicateFileItem> Files { get; }
    public int Count => Files.Count;

    /// <summary>Texto de cabecera para el grupo (evita x:Bind en Run, no soportado en WinUI).</summary>
    public string GroupTitle => $"Grupo: {FingerprintShort} ({Count} archivos)";
}
