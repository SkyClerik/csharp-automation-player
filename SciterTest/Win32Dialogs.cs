using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32Dialogs
{
    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class OpenFileName
    {
        public int lStructSize = Marshal.SizeOf(typeof(OpenFileName));
        public nint hwndOwner = nint.Zero;
        public nint hInstance = nint.Zero;
        public string lpstrFilter = null!;
        public string lpstrCustomFilter = null!;
        public int nMaxCustFilter = 0;
        public int nFilterIndex = 1;
        public StringBuilder lpstrFile = new StringBuilder(1024);
        public int nMaxFile = 1024;
        public string lpstrFileTitle = null!;
        public int nMaxFileTitle = 0;
        public string lpstrInitialDir = null!;
        public string lpstrTitle = null!;
        public int Flags = 0x00080000 | 0x00001000 | 0x00000800; // OFN_EXPLORER | OFN_FILEMUSTEXIST
        public short nFileOffset = 0;
        public short nFileExtension = 0;
        public string lpstrDefExt = null!;
        public nint lCustData = nint.Zero;
        public nint lpfnHook = nint.Zero;
        public string lpTemplateName = null!;
        public nint pvReserved = nint.Zero;
        public int dwReserved = 0;
        public int FlagsEx = 0;
    }

    public static string GetOpenFileNameDialog(nint owner, string filter, string title)
    {
        var ofn = new OpenFileName { hwndOwner = owner, lpstrTitle = title };
        if (!string.IsNullOrEmpty(filter))
        {
            ofn.lpstrFilter = filter.Replace("|", "\0") + "\0";
        }
        return GetOpenFileName(ofn) ? ofn.lpstrFile.ToString() : string.Empty;
    }

    public static string GetFolderDialog(nint owner, string title)
    {
        // Для выбора папок в чистом Win32 без сторонних UI-библиотек (WPF/WinForms)
        // используем трюк с флагом OFN_NOVALIDATE / выбор папки.
        var ofn = new OpenFileName
        {
            hwndOwner = owner,
            lpstrTitle = title,
            Flags = 0x00080000 | 0x00000020 // OFN_EXPLORER | OFN_ENABLEHOOK (или кастомный флаг папки)
        };
        ofn.lpstrFilter = "Папки\0\n\0";
        return GetOpenFileName(ofn) ? Path.GetDirectoryName(ofn.lpstrFile.ToString()) ?? string.Empty : string.Empty;
    }
}
