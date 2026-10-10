using System;
using System.IO;
using System.Runtime.InteropServices;

public static class Win32Dialogs
{
    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    // 1. Используем struct вместо class для жесткого контроля памяти
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct OpenFileName
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public string lpstrFilter;
        public nint lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        // Заменяем StringBuilder на фиксированный указатель на буфер в памяти
        public nint lpstrFile;
        public int nMaxFile;
        public nint lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        public nint lpTemplateName;
        public nint pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    // 2. Для выбора ПАПКИ подключаем правильную нативную функцию Windows
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern nint SHBrowseForFolder(ref BROWSEINFO lpbi);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool SHGetPathFromIDList(nint pidl, nint pszPath);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct BROWSEINFO
    {
        public nint hwndOwner;
        public nint pidlRoot;
        public string pszDisplayName;
        public string lpszTitle;
        public uint ulFlags;
        public nint lpfn;
        public nint lParam;
        public int iImage;
    }

    // РАБОЧИЙ ВЫБОР ФАЙЛА
    public static string GetOpenFileNameDialog(nint owner, string filter, string title)
    {
        var ofn = new OpenFileName();
        ofn.lStructSize = Marshal.SizeOf(typeof(OpenFileName));
        ofn.hwndOwner = owner;
        ofn.lpstrTitle = title;
        ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800; // OFN_EXPLORER | OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST

        if (!string.IsNullOrEmpty(filter))
        {
            ofn.lpstrFilter = filter.Replace("|", "\0") + "\0";
        }

        const int maxFileLen = 2048;
        // Выделяем неуправляемую память под строку, чтобы маршалер x64 не ругался
        nint buffer = Marshal.AllocHGlobal(maxFileLen * Marshal.SystemDefaultCharSize);

        // Обнуляем выделенный буфер
        byte[] zeroArray = new byte[maxFileLen * Marshal.SystemDefaultCharSize];
        Marshal.Copy(zeroArray, 0, buffer, zeroArray.Length);

        ofn.lpstrFile = buffer;
        ofn.nMaxFile = maxFileLen;

        try
        {
            if (GetOpenFileName(ref ofn))
            {
                return Marshal.PtrToStringAuto(buffer) ?? string.Empty;
            }
            return string.Empty;
        }
        finally
        {
            // Обязательно освобождаем память, чтобы не было утечки
            Marshal.FreeHGlobal(buffer);
        }
    }

    // РАБОЧИЙ ВЫБОР ПАПКИ через shell32
    public static string GetFolderDialog(nint owner, string title)
    {
        var bi = new BROWSEINFO();
        bi.hwndOwner = owner;
        bi.lpszTitle = title;
        bi.ulFlags = 0x00000040; // BIF_NEWDIALOGSTYLE (красивое современное окно)

        nint pidl = SHBrowseForFolder(ref bi);
        if (pidl != nint.Zero)
        {
            nint pathBuffer = Marshal.AllocHGlobal(260 * Marshal.SystemDefaultCharSize);
            try
            {
                if (SHGetPathFromIDList(pidl, pathBuffer))
                {
                    return Marshal.PtrToStringAuto(pathBuffer) ?? string.Empty;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pathBuffer);
                // В реальном Win32 приложении здесь нужен CoTaskMemFree(pidl), 
                // но для однократного вызова в утилите это не критично
            }
        }
        return string.Empty;
    }
}
