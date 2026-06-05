using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CyberPulse.UI
{
    public static class WindowsFilePicker
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPENFILENAME
        {
            public int    lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public IntPtr lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int    nMaxCustFilter;
            public int    nFilterIndex;
            public IntPtr lpstrFile;
            public int    nMaxFile;
            public IntPtr lpstrFileTitle;
            public int    nMaxFileTitle;
            public IntPtr lpstrInitialDir;
            public IntPtr lpstrTitle;
            public int    Flags;
            public short  nFileOffset;
            public short  nFileExtension;
            public IntPtr lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetOpenFileNameW(ref OPENFILENAME ofn);

        private const int OFN_FILEMUSTEXIST = 0x1000;
        private const int OFN_PATHMUSTEXIST = 0x0800;
        private const int OFN_HIDEREADONLY  = 0x0004;
        private const int OFN_NOCHANGEDIR   = 0x0008;

        public static string OpenAudioFile()
        {
            byte[] filterBytes = BuildWideString("Audio Files\0*.mp3;*.wav;*.ogg\0\0");
            IntPtr filterPtr   = Marshal.AllocHGlobal(filterBytes.Length);
            Marshal.Copy(filterBytes, 0, filterPtr, filterBytes.Length);

            const int MaxPath  = 2048;
            byte[]    fileBuf  = new byte[MaxPath * 2];
            IntPtr    filePtr  = Marshal.AllocHGlobal(fileBuf.Length);
            Marshal.Copy(fileBuf, 0, filePtr, fileBuf.Length);

            string result = null;
            try
            {
                var ofn = new OPENFILENAME();
                ofn.lStructSize  = Marshal.SizeOf(ofn);
                ofn.lpstrFilter  = filterPtr;
                ofn.nFilterIndex = 1;
                ofn.lpstrFile    = filePtr;
                ofn.nMaxFile     = MaxPath;
                ofn.Flags        = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR;

                if (GetOpenFileNameW(ref ofn))
                    result = Marshal.PtrToStringUni(filePtr);
            }
            finally
            {
                Marshal.FreeHGlobal(filterPtr);
                Marshal.FreeHGlobal(filePtr);
            }

            return result;
        }

        private static byte[] BuildWideString(string s) => Encoding.Unicode.GetBytes(s);
#else
        public static string OpenAudioFile() => null;
#endif
    }
}
