using System.Runtime.InteropServices;

namespace PhotoDuplicates.Services;

/// <summary>Envía archivos a la Papelera de reciclaje (Windows Shell).</summary>
internal static class FileRecycleHelper
{
    private const uint FO_DELETE = 3;
    private const ushort FOF_ALLOWUNDO = 0x40;
    private const ushort FOF_NOCONFIRMATION = 0x10;
    private const ushort FOF_SILENT = 0x4;
    private const ushort FOF_NOERRORUI = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCTW
    {
        public nint hwnd;
        public uint wFunc;
        public nint pFrom;
        public nint pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public nint hNameMappings;
        public nint lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCTW lpFileOp);

    public static void MoveToRecycleBin(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentException("Ruta vacía.", nameof(fullPath));

        var from = fullPath + '\0' + '\0';
        var ptr = Marshal.StringToHGlobalUni(from);
        try
        {
            var op = new SHFILEOPSTRUCTW
            {
                hwnd = nint.Zero,
                wFunc = FO_DELETE,
                pFrom = ptr,
                pTo = nint.Zero,
                fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI),
                fAnyOperationsAborted = 0,
                hNameMappings = nint.Zero,
                lpszProgressTitle = nint.Zero
            };
            var code = SHFileOperation(ref op);
            if (code != 0)
                throw new IOException($"No se pudo mover a la papelera (código {code}): {fullPath}");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
