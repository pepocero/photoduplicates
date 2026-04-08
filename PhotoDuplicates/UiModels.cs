using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

public sealed class DuplicateGroupVm : INotifyPropertyChanged
{
    /// <summary>Máximo de miniaturas cargadas a la vez por grupo (reduce memoria y bloqueo de UI).</summary>
    public const int PageSize = 50;

    private int _pageIndex;

    public DuplicateGroupVm(string fingerprintHex, ObservableCollection<DuplicateFileItem> files)
    {
        FingerprintHex = fingerprintHex;
        Files = files;
        PageItems = new ObservableCollection<DuplicateFileItem>();
        Files.CollectionChanged += OnFilesCollectionChanged;
        _pageIndex = 0;
        RebuildPageItems();
    }

    public string FingerprintHex { get; }
    public string FingerprintShort => FingerprintHex.Length <= 24 ? FingerprintHex : FingerprintHex[..24] + "...";
    public ObservableCollection<DuplicateFileItem> Files { get; }
    public ObservableCollection<DuplicateFileItem> PageItems { get; }
    public int Count => Files.Count;

    /// <summary>Número de página mostrado (1-based).</summary>
    public int DisplayPage => _pageIndex + 1;

    public int PageCount => Files.Count == 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Files.Count / (double)PageSize));

    public bool HasMultiplePages => PageCount > 1;

    public bool CanPrevPage => _pageIndex > 0;

    public bool CanNextPage => _pageIndex < PageCount - 1;

    /// <summary>Texto de cabecera para el grupo (evita x:Bind en Run, no soportado en WinUI).</summary>
    public string GroupTitle
    {
        get
        {
            var baseTitle = $"Grupo: {FingerprintShort} ({Count} archivos)";
            if (!HasMultiplePages)
                return baseTitle;
            return $"{baseTitle} — Página {DisplayPage} de {PageCount}";
        }
    }

    public void GoToPage(int zeroBasedPage)
    {
        var max = Math.Max(0, PageCount - 1);
        zeroBasedPage = Math.Clamp(zeroBasedPage, 0, max);
        _pageIndex = zeroBasedPage;
        RebuildPageItems();
        RaisePageProps();
    }

    public void PrevPage() => GoToPage(_pageIndex - 1);

    public void NextPage() => GoToPage(_pageIndex + 1);

    private void OnFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Files.Count == 0)
        {
            PageItems.Clear();
            RaisePageProps();
            return;
        }

        var maxPage = PageCount - 1;
        if (_pageIndex > maxPage)
            _pageIndex = maxPage;
        RebuildPageItems();
        RaisePageProps();
    }

    private void RebuildPageItems()
    {
        PageItems.Clear();
        if (Files.Count == 0)
            return;

        var start = _pageIndex * PageSize;
        if (start >= Files.Count)
        {
            _pageIndex = Math.Max(0, PageCount - 1);
            start = _pageIndex * PageSize;
        }

        var count = Math.Min(PageSize, Files.Count - start);
        for (var i = 0; i < count; i++)
            PageItems.Add(Files[start + i]);
    }

    private void RaisePageProps()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayPage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GroupTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMultiplePages)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanPrevPage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanNextPage)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
