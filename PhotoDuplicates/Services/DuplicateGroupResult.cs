namespace PhotoDuplicates.Services;

/// <summary>Un grupo de rutas que comparten la misma huella (duplicados).</summary>
public sealed class DuplicateGroupResult
{
    public DuplicateGroupResult(string fingerprintHex, IReadOnlyList<string> paths)
    {
        FingerprintHex = fingerprintHex;
        Paths = paths;
    }

    public string FingerprintHex { get; }
    public IReadOnlyList<string> Paths { get; }
    public int Count => Paths.Count;
}
