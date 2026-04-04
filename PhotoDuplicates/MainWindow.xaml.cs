using System.Collections.ObjectModel;
using System.IO;
using Windows.Foundation;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoDuplicates.Services;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PhotoDuplicates;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<DuplicateGroupVm> _groups = new();
    private CancellationTokenSource? _scanCts;
    private Window? _aboutCarliniToolsWindow;

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "Duplicados de fotos";
        GroupsList.ItemsSource = _groups;
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        // No usar PicturesLibrary como inicio: en algunos equipos el botón "Seleccionar carpeta"
        // permanece deshabilitado si la vista inicial coincide con la biblioteca. ComputerFolder evita eso.
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
            return;

        FolderPathBox.Text = folder.Path;
        UpdateSummary();
    }

    private async void DuplicateThumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path } || !File.Exists(path))
            return;

        BitmapImage full;
        try
        {
            full = new BitmapImage();
            full.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        }
        catch
        {
            await ShowMessageAsync("No se pudo cargar la vista previa.");
            return;
        }

        // Tamaño máximo acotado al monitor (evita layout más grande que el diálogo → recorte).
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
        var work = displayArea.WorkArea;
        const double chromeReserveW = 48;
        const double chromeReserveH = 140;
        double previewMaxW = Math.Min(1280, Math.Max(400, work.Width - chromeReserveW));
        double previewMaxH = Math.Min(900, Math.Max(300, work.Height - chromeReserveH));

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Source = full
        };

        var panel = new Border
        {
            MaxWidth = previewMaxW,
            MaxHeight = previewMaxH,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        panel.Child = image;

        var dialog = new ContentDialog
        {
            Title = Path.GetFileName(path),
            Content = panel,
            PrimaryButtonText = "Cerrar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
            FullSizeDesired = true,
            MaxWidth = previewMaxW + chromeReserveW,
            MaxHeight = previewMaxH + chromeReserveH
        };

        void OnImageOpened(object imgSender, RoutedEventArgs args)
        {
            image.ImageOpened -= OnImageOpened;
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
        }

        image.ImageOpened += OnImageOpened;

        await dialog.ShowAsync();
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        var root = FolderPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(root))
        {
            await ShowMessageAsync("Selecciona una carpeta con «Examinar…».");
            return;
        }

        if (!Directory.Exists(root))
        {
            await ShowMessageAsync("La carpeta no existe o no es accesible.");
            return;
        }

        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        ScanButton.IsEnabled = false;
        BrowseButton.IsEnabled = false;
        CancelScanButton.IsEnabled = true;
        DeleteButton.IsEnabled = false;
        _groups.Clear();
        ScanProgress.Value = 0;
        ScanProgress.Maximum = 100;
        ScanStatusText.Text = "Preparando…";

        var mode = ModeVisualRadio.IsChecked == true
            ? DuplicateScanMode.VisualFingerprint
            : DuplicateScanMode.ExactFileHash;

        var scanner = new DuplicateScanner();
        var progress = new Progress<(int done, int total)>(p =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (p.total > 0)
                {
                    ScanProgress.Maximum = p.total;
                    ScanProgress.Value = p.done;
                }
                ScanStatusText.Text = $"Procesando {p.done} / {p.total}";
            });
        });

        try
        {
            var results = await scanner.ScanAsync(root, mode, progress, token).ConfigureAwait(true);
            foreach (var g in results)
            {
                var items = new ObservableCollection<DuplicateFileItem>();
                for (var i = 0; i < g.Paths.Count; i++)
                    items.Add(new DuplicateFileItem(g.Paths[i], markedForDeletion: i > 0));

                _groups.Add(new DuplicateGroupVm(g.FingerprintHex, items));
            }

            ScanStatusText.Text = _groups.Count == 0
                ? "No se encontraron duplicados."
                : $"Listo: {_groups.Count} grupo(s) de duplicados.";
        }
        catch (OperationCanceledException)
        {
            ScanStatusText.Text = "Escaneo cancelado.";
        }
        catch (Exception ex)
        {
            ScanStatusText.Text = "Error durante el escaneo.";
            await ShowMessageAsync(ex.Message);
        }
        finally
        {
            ScanButton.IsEnabled = true;
            BrowseButton.IsEnabled = true;
            CancelScanButton.IsEnabled = false;
            _scanCts?.Dispose();
            _scanCts = null;
            DeleteButton.IsEnabled = _groups.Count > 0;
            UpdateSummary();
        }
    }

    private void CancelScanButton_Click(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var paths = new List<string>();
        foreach (var g in _groups)
        {
            foreach (var f in g.Files)
            {
                if (f.MarkedForDeletion)
                    paths.Add(f.FilePath);
            }
        }

        if (paths.Count == 0)
        {
            await ShowMessageAsync("Marca al menos un archivo duplicado para eliminar.");
            return;
        }

        var confirmDialog = new ContentDialog
        {
            Title = "Confirmar",
            Content = $"Se enviarán {paths.Count} archivo(s) a la Papelera de reciclaje. ¿Continuar?",
            PrimaryButtonText = "Sí, enviar a la papelera",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var statusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 460,
            Text = "Preparando…"
        };
        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 10
        };
        var progressPanel = new StackPanel { Spacing = 12, Children = { statusText, progressBar } };

        var progressDialog = new ContentDialog
        {
            Title = "Enviando a la papelera",
            Content = progressPanel,
            XamlRoot = Content.XamlRoot,
            PrimaryButtonText = "Espere…",
            IsPrimaryButtonEnabled = false
        };

        var succeeded = new List<string>(paths.Count);
        var errors = new List<string>();

        IAsyncOperation<ContentDialogResult>? progressShowOp = null;
        try
        {
            progressShowOp = progressDialog.ShowAsync();
            await System.Threading.Tasks.Task.Delay(32);

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                var name = Path.GetFileName(path);
                var n = i + 1;
                var pctBefore = paths.Count > 0 ? (int)(100.0 * i / paths.Count) : 0;
                statusText.Text = $"Enviando a la papelera ({n} de {paths.Count}) — {pctBefore}%\n{name}";
                progressBar.Value = paths.Count > 0 ? 100.0 * i / paths.Count : 0;
                await System.Threading.Tasks.Task.Yield();

                try
                {
                    if (File.Exists(path))
                        FileRecycleHelper.MoveToRecycleBin(path);
                    succeeded.Add(path);
                }
                catch (Exception ex)
                {
                    errors.Add($"{path}: {ex.Message}");
                }

                var pctAfter = (int)(100.0 * n / paths.Count);
                progressBar.Value = 100.0 * n / paths.Count;
                statusText.Text = $"Completado {n} de {paths.Count} ({pctAfter}%)\n{name}";
                await System.Threading.Tasks.Task.Yield();
            }
        }
        finally
        {
            progressDialog.Hide();
        }

        if (progressShowOp is not null)
            await progressShowOp;

        RemoveDeletedFromUi(succeeded);

        if (errors.Count == 0)
        {
            await ShowResultDialogAsync(
                "Eliminación completada",
                $"Se han enviado a la papelera correctamente {succeeded.Count} archivo(s).");
        }
        else if (succeeded.Count > 0)
        {
            await ShowResultDialogAsync(
                "Eliminación completada",
                $"Se enviaron correctamente {succeeded.Count} archivo(s).\n\n" +
                $"No se pudieron enviar {errors.Count}:\n" + string.Join("\n", errors.Take(6)));
        }
        else
        {
            await ShowResultDialogAsync(
                "No se pudo eliminar",
                "No se pudo enviar ningún archivo a la papelera:\n" + string.Join("\n", errors.Take(8)));
        }

        UpdateSummary();
        DeleteButton.IsEnabled = _groups.Count > 0 && HasAnyMarked();
    }

    private void RemoveDeletedFromUi(IReadOnlyCollection<string> deleted)
    {
        var set = new HashSet<string>(deleted, StringComparer.OrdinalIgnoreCase);
        foreach (var g in _groups.ToList())
        {
            foreach (var f in g.Files.ToList())
            {
                if (set.Contains(f.FilePath))
                    g.Files.Remove(f);
            }

            if (g.Files.Count <= 1)
                _groups.Remove(g);
        }
    }

    private bool HasAnyMarked()
    {
        foreach (var g in _groups)
        {
            foreach (var f in g.Files)
            {
                if (f.MarkedForDeletion)
                    return true;
            }
        }

        return false;
    }

    private void UpdateSummary()
    {
        if (_groups.Count == 0)
        {
            SummaryText.Text = "";
            return;
        }

        var dupes = 0;
        foreach (var g in _groups)
            dupes += Math.Max(0, g.Files.Count - 1);
        SummaryText.Text = $"{_groups.Count} grupo(s); ~{dupes} archivo(s) redundante(s). Marca cuáles enviar a la papelera (por defecto se conserva uno por grupo).";
    }

    private async System.Threading.Tasks.Task ShowMessageAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Duplicados de fotos",
            Content = message,
            CloseButtonText = "Aceptar",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async System.Threading.Tasks.Task ShowResultDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "Aceptar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void CarliniToolsCredit_Click(object sender, RoutedEventArgs e)
    {
        if (_aboutCarliniToolsWindow is not null)
        {
            _aboutCarliniToolsWindow.Activate();
            return;
        }

        var titleBlock = new TextBlock
        {
            Text = "CarliniTools",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var emailLink = new HyperlinkButton
        {
            Content = "info@carlinitools.com",
            NavigateUri = new Uri("mailto:info@carlinitools.com"),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 14,
            Padding = new Thickness(8, 4, 8, 4)
        };
        if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var fg) && fg is Brush fgBrush)
            emailLink.Foreground = fgBrush;

        var stack = new StackPanel
        {
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.Children.Add(titleBlock);
        stack.Children.Add(emailLink);

        var root = new Grid
        {
            Padding = new Thickness(32, 28, 32, 28)
        };
        if (Application.Current.Resources.TryGetValue("LayerFillColorDefaultBrush", out var bg) && bg is Brush bgBrush)
            root.Background = bgBrush;
        root.Children.Add(stack);

        var w = new Window
        {
            Title = "CarliniTools",
            Content = root
        };

        w.Closed += (_, _) => _aboutCarliniToolsWindow = null;
        _aboutCarliniToolsWindow = w;

        w.Activate();

        var hwnd = WindowNative.GetWindowHandle(w);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32 { Width = 420, Height = 200 });
    }
}
