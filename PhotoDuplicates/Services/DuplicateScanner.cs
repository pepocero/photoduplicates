using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PhotoDuplicates.Services;

/// <summary>Escanea carpetas en busca de imágenes duplicadas.</summary>
public sealed class DuplicateScanner
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff"
    };

    public static IEnumerable<string> EnumerateImageFiles(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
            yield break;

        var stack = new Stack<string>();
        stack.Push(rootDirectory);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                    stack.Push(sub);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            string[] filesInDir;
            try
            {
                filesInDir = Directory.GetFiles(dir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var path in filesInDir)
            {
                string ext;
                try
                {
                    ext = Path.GetExtension(path);
                }
                catch
                {
                    continue;
                }

                if (ImageExtensions.Contains(ext))
                    yield return path;
            }
        }
    }

    public async Task<IReadOnlyList<DuplicateGroupResult>> ScanAsync(
        string rootDirectory,
        DuplicateScanMode mode,
        IProgress<(int done, int total)>? progress,
        CancellationToken cancellationToken)
    {
        var list = EnumerateImageFiles(rootDirectory).ToList();
        var total = list.Count;
        var completed = 0;
        var map = new ConcurrentDictionary<string, ConcurrentBag<string>>(StringComparer.Ordinal);

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
        };

        await Parallel.ForEachAsync(list, parallelOptions, async (path, ct) =>
        {
            try
            {
                string key;
                if (mode == DuplicateScanMode.ExactFileHash)
                    key = await ComputeFileSha256HexAsync(path, ct).ConfigureAwait(false);
                else
                    key = await ComputeVisualFingerprintHexAsync(path, ct).ConfigureAwait(false);

                map.GetOrAdd(key, _ => new ConcurrentBag<string>()).Add(path);
            }
            catch
            {
                // Archivo inaccesible o imagen corrupta: omitir
            }
            finally
            {
                var n = Interlocked.Increment(ref completed);
                progress?.Report((n, total));
            }
        }).ConfigureAwait(false);

        var groups = map
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => new DuplicateGroupResult(kv.Key, kv.Value.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Paths[0], StringComparer.OrdinalIgnoreCase)
            .ToList();

        return groups;
    }

    private static async Task<string> ComputeFileSha256HexAsync(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1 << 20,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task<string> ComputeVisualFingerprintHexAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        using var image = await Image.LoadAsync<Rgba32>(fs, ct).ConfigureAwait(false);

        image.Mutate(ctx =>
        {
            ctx.AutoOrient();
            ctx.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(64, 64),
                Mode = ResizeMode.Pad,
                PadColor = SixLabors.ImageSharp.Color.Black
            });
        });

        var w = image.Width;
        var h = image.Height;
        var bytes = new byte[checked(w * h * 4)];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                var dest = bytes.AsSpan(y * w * 4, w * 4);
                MemoryMarshal.AsBytes(row).CopyTo(dest);
            }
        });
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
