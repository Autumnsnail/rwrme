using System;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Windows 原生打开文件 / 选择文件夹对话框。用于打包后替代 System.Windows.Forms（Unity Player 通常不包含该程序集）。
/// </summary>
public static class RuntimeWindowsDialogs
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_EXPLORER = 0x00080000;
    private const int OFN_NOCHANGEDIR = 0x00000008;

    private const uint BIF_RETURNONLYFSDIRS = 0x0001;
    private const uint BIF_NEWDIALOGSTYLE = 0x0040;

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetOpenFileNameW(ref OpenFilenameW ofn);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHBrowseForFolderW(ref BrowseInfoW bi);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder pszPath);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr ptr);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFilenameW
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BrowseInfoW
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }
#endif

    /// <summary>
    /// filter 使用与 WinForms 相同的竖线格式，例如 "PNG|*.png|All|*.*"。
    /// </summary>
    public static string ShowOpenFile(string title, string pipeSeparatedFilter, string initialDir = null)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return ShowOpenFileWin(title, pipeSeparatedFilter, initialDir);
#else
        return null;
#endif
    }

    public static string ShowBrowseFolder(string title)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return ShowBrowseFolderWin(title);
#else
        return null;
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private static string ShowOpenFileWin(string title, string pipeSeparatedFilter, string initialDir)
    {
        var filter = ToWin32Filter(pipeSeparatedFilter);
        const int maxChars = 32768;
        var fileBuffer = Marshal.AllocHGlobal(maxChars * 2);
        try
        {
            for (int i = 0; i < maxChars * 2; i++)
                Marshal.WriteByte(fileBuffer, i, 0);

            var ofn = new OpenFilenameW
            {
                lStructSize = Marshal.SizeOf(typeof(OpenFilenameW)),
                hwndOwner = GetForegroundWindow(),
                hInstance = IntPtr.Zero,
                lpstrFilter = filter,
                lpstrCustomFilter = null,
                nMaxCustFilter = 0,
                nFilterIndex = 1,
                lpstrFile = fileBuffer,
                nMaxFile = maxChars,
                lpstrFileTitle = IntPtr.Zero,
                nMaxFileTitle = 0,
                lpstrInitialDir = string.IsNullOrEmpty(initialDir) ? null : initialDir,
                lpstrTitle = title ?? "Open",
                Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR,
                nFileOffset = 0,
                nFileExtension = 0,
                lpstrDefExt = null,
                lCustData = IntPtr.Zero,
                lpfnHook = IntPtr.Zero,
                lpTemplateName = null,
                pvReserved = IntPtr.Zero,
                dwReserved = 0,
                FlagsEx = 0
            };

            if (!GetOpenFileNameW(ref ofn))
                return null;

            var path = Marshal.PtrToStringUni(fileBuffer);
            return string.IsNullOrEmpty(path) ? null : path;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
        }
    }

    private static string ShowBrowseFolderWin(string title)
    {
        var displayName = Marshal.AllocHGlobal(260 * 2);
        try
        {
            for (int i = 0; i < 260 * 2; i++)
                Marshal.WriteByte(displayName, i, 0);

            var bi = new BrowseInfoW
            {
                hwndOwner = GetForegroundWindow(),
                pidlRoot = IntPtr.Zero,
                pszDisplayName = displayName,
                lpszTitle = string.IsNullOrEmpty(title) ? "Select folder" : title,
                ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE,
                lpfn = IntPtr.Zero,
                lParam = IntPtr.Zero,
                iImage = 0
            };

            var pidl = SHBrowseForFolderW(ref bi);
            if (pidl == IntPtr.Zero)
                return null;

            try
            {
                var path = new StringBuilder(32768);
                if (!SHGetPathFromIDListW(pidl, path))
                    return null;
                var s = path.ToString();
                return string.IsNullOrEmpty(s) ? null : s;
            }
            finally
            {
                CoTaskMemFree(pidl);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(displayName);
        }
    }

    private static string ToWin32Filter(string pipeSeparatedFilter)
    {
        if (string.IsNullOrEmpty(pipeSeparatedFilter))
            return "All files\0*.*\0\0";

        var parts = pipeSeparatedFilter.Split('|');
        if (parts.Length < 2)
            return pipeSeparatedFilter + "\0*.*\0\0";

        var sb = new StringBuilder(pipeSeparatedFilter.Length + 8);
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            sb.Append(parts[i]);
            sb.Append('\0');
            sb.Append(parts[i + 1]);
            sb.Append('\0');
        }
        sb.Append('\0');
        return sb.ToString();
    }
#endif
}
