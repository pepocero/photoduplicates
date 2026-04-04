namespace PhotoDuplicates;

/// <summary>Modo de comparación para detectar duplicados.</summary>
public enum DuplicateScanMode
{
    /// <summary>Mismo contenido binario del archivo (copias exactas).</summary>
    ExactFileHash,
    /// <summary>Misma imagen visual tras normalizar tamaño y formato (ignora metadatos y recompresión).</summary>
    VisualFingerprint,
}
