using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;
using System.Text;
using System.Diagnostics;

namespace kcslib
{
    /// <summary>
    /// Utility class for system call and constants.
    /// </summary>
    public static class KSyscalls
    {
        public const int INVALID_HANDLE_VALUE = -1;
        public const int HWND_TOPMOST = -1;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const int WM_USER = 0x400;
        public const int WM_COPYDATA = 0x4A;
        public const int ATTACH_PARENT_PROCESS = -1;
        public const uint INFINITE = 0xFFFFFFFF;
        /// <summary>Maximal Length of unmanaged Windows-Path-strings</summary>
        public const int MAX_PATH = 260;
        /// <summary>Maximal Length of unmanaged Typename</summary>
        public const int MAX_TYPE = 80;

        /// <summary>
        /// Used to get Process Informations
        /// </summary>
        public const int TOKEN_QUERY = 0X00000008;

        public const int ERROR_NO_MORE_ITEMS = 259;
        public const int ERROR_INSUFFICIENT_BUFFER = 0x7A;


        public enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId
        }

        public struct TOKEN_USER
        {
            public SID_AND_ATTRIBUTES User;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SID_AND_ATTRIBUTES
        {

            public IntPtr Sid;
            public int Attributes;
        }

        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpData;
        }

        public enum WindowStatus : int
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10
        }

        public enum SW : int
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_NORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_MAXIMIZE = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11,
            SW_MAX = 11
        }


        public enum ScrollBarOptions : int
        {
            EM_SCROLL = 0xb5, // Horizontal scroll
            SB_LINEUP = 0, // Scrolls one line up
            SB_LINELEFT = 0,// Scrolls one cell left
            SB_LINEDOWN = 1, // Scrolls one line down
            SB_LINERIGHT = 1,// Scrolls one cell right
            SB_PAGEUP = 2, // Scrolls one page up
            SB_PAGELEFT = 2,// Scrolls one page left
            SB_PAGEDOWN = 3, // Scrolls one page down
            SB_PAGERIGTH = 3, // Scrolls one page right
            SB_PAGETOP = 6, // Scrolls to the upper left
            SB_LEFT = 6, // Scrolls to the left
            SB_PAGEBOTTOM = 7, // Scrolls to the upper right
            SB_RIGHT = 7, // Scrolls to the right
            SB_ENDSCROLL = 8 // Ends scroll
        }

        public enum DWFLAGS
        {
            STARTF_USESHOWWINDOW = 0x00000001,
            STARTF_USESIZE = 0x00000002,
            STARTF_USEPOSITION = 0x00000004,
            STARTF_USECOUNTCHARS = 0x00000008,
            STARTF_USEFILLATTRIBUTE = 0x00000010,
            STARTF_RUNFULLSCREEN = 0x00000020,
            STARTF_FORCEONFEEDBACK = 0x00000040,
            STARTF_FORCEOFFFEEDBACK = 0x00000080,
            STARTF_USESTDHANDLES = 0x00000100,
            STARTF_USEHOTKEY = 0x00000200
        }

        public enum WAIT_RETURN_REASON
        {
            WAIT_OBJECT_0 = 0x00000000,
            WAIT_ABANDONED = 0x00000080,
            WAIT_TIMEOUT = 0x00000102
        }

        public enum CREATION_FLAGS
        {
            DEBUG_PROCESS = 0x00000001,
            DEBUG_ONLY_THIS_PROCESS = 0x00000002,
            CREATE_SUSPENDED = 0x00000004,
            DETACHED_PROCESS = 0x00000008,
            CREATE_NEW_CONSOLE = 0x00000010,
            NORMAL_PRIORITY_CLASS = 0x00000020,
            IDLE_PRIORITY_CLASS = 0x00000040,
            HIGH_PRIORITY_CLASS = 0x00000080,
            REALTIME_PRIORITY_CLASS = 0x00000100,
            CREATE_NEW_PROCESS_GROUP = 0x00000200,
            CREATE_UNICODE_ENVIRONMENT = 0x00000400,
            CREATE_SEPARATE_WOW_VDM = 0x00000800,
            CREATE_SHARED_WOW_VDM = 0x00001000,
            BELOW_NORMAL_PRIORITY_CLASS = 0x00004000,
            ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000,
            CREATE_PROTECTED_PROCESS = 0x00040000,
            EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
            CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
            CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
            CREATE_DEFAULT_ERROR_MODE = 0x04000000,
            CREATE_NO_WINDOW = 0x08000000
        }

        public enum STD_HANDLES
        {
            STD_INPUT_HANDLE = -10,
            STD_OUTPUT_HANDLE = -11,
            STD_ERROR_HANDLE = -12
        }

        [Flags]
        public enum SHGFI : int
        {
            /// <summary>get icon</summary>
            Icon = 0x000000100,
            /// <summary>get display name</summary>
            DisplayName = 0x000000200,
            /// <summary>get type name</summary>
            TypeName = 0x000000400,
            /// <summary>get attributes</summary>
            Attributes = 0x000000800,
            /// <summary>get icon location</summary>
            IconLocation = 0x000001000,
            /// <summary>return exe type</summary>
            ExeType = 0x000002000,
            /// <summary>get system icon index</summary>
            SysIconIndex = 0x000004000,
            /// <summary>put a link overlay on icon</summary>
            LinkOverlay = 0x000008000,
            /// <summary>show icon in selected state</summary>
            Selected = 0x000010000,
            /// <summary>get only specified attributes</summary>
            Attr_Specified = 0x000020000,
            /// <summary>get large icon</summary>
            LargeIcon = 0x000000000,
            /// <summary>get small icon</summary>
            SmallIcon = 0x000000001,
            /// <summary>get open icon</summary>
            OpenIcon = 0x000000002,
            /// <summary>get shell size icon</summary>
            ShellIconSize = 0x000000004,
            /// <summary>pszPath is a pidl</summary>
            PIDL = 0x000000008,
            /// <summary>use passed dwFileAttribute</summary>
            UseFileAttributes = 0x000000010,
            /// <summary>apply the appropriate overlays</summary>
            AddOverlays = 0x000000020,
            /// <summary>Get the index of the overlay in the upper 8 bits of the iIcon</summary>
            OverlayIndex = 0x000000040,
        }

        public enum SE_ERR
        {
            SE_ERR_FNF = 2,               // file not found
            SE_ERR_PNF = 3,              // path not found
            SE_ERR_ACCESSDENIED = 5,     // access denied
            SE_ERR_OOM = 8,              // out of memory
            SE_ERR_DLLNOTFOUND = 32,
            SE_ERR_SHARE = 26,
            SE_ERR_ASSOCINCOMPLETE = 27,
            SE_ERR_DDETIMEOUT = 28,
            SE_ERR_DDEFAIL = 29,
            SE_ERR_DDEBUSY = 30,
            SE_ERR_NOASSOC = 31
        }

        public enum SEE_MASK
        {
            CLASSNAME = 0x1,
            CLASSKEY = 0x3,
            IDLIST = 0x4,
            INVOKEIDLIST = 0xC,
            ICON = 0x10,
            HOTKEY = 0x20,
            NOCLOSEPROCESS = 0x40,
            CONNECTNETDRV = 0x80,
            FLAG_DDEWAIT = 0x100,
            DOENVSUBST = 0x200,
            FLAG_NO_UI = 0x400,
            UNICODE = 0x4000,
            NO_CONSOLE = 0x8000,
            ASYNCOK = 0x100000,
            HMONITOR = 0x200000,
            NOZONECHECKS = 0x800000,
            NOQUERYCLASSSTORE = 0x1000000,
            WAITFORINPUTIDLE = 0x2000000,
            FLAG_LOG_USAGE = 0x4000000
        }


        public struct STARTUPINFO
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public int lpReserved2;
            public int hStdInput;
            public int hStdOutput;
            public int hStdError;
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public SHFILEINFO(bool b)
            {
                hIcon = IntPtr.Zero;
                iIcon = 0;
                dwAttributes = 0;
                szDisplayName = "";
                szTypeName = "";
            }
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_TYPE)]
            public string szTypeName;
        };

        public struct PROCESS_INFORMATION
        {
            public int hProcess;
            public int hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        public struct SECURITY_ATTRIBUTES
        {
            public int length;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        public struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        public struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public SEE_MASK fMask;
            public IntPtr hwnd;
            public string lpVerb;
            public string lpFile;
            public string lpParameters;
            public string lpDirectory;
            public int nShow;
            public SE_ERR hInstApp;
            public IntPtr lpIDList;
            public string lpClass;
            public IntPtr hkeyClass;
            public int dwHotKey;
            public IntPtr hIcon;
            public IntPtr Process;
        }

        public struct IconInfo
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        /// <summary>
        /// Allow write to console.
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int dwProcessId);

        /// <summary>
        /// SendMessage() for use with WM_COPYDATA and COPYDATASTRUCT.
        /// </summary>
        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        public static extern int SendMessage(int hWnd, int Msg, int wParam, ref COPYDATASTRUCT lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        /* The second argument is normally a String, however we can't pass
         * null in c# instead of a string. So, I modified the type to be an int
         * so that we can pass IntPtr.zero. Don't try to use this call if
         * you want to specify a window name! */
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string strClassName, int strWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowPos")]
        public static extern bool SetWindowPos(
             int hWnd,           // window handle
             int hWndInsertAfter,    // placement-order handle
             int X,          // horizontal position
             int Y,          // vertical position
             int cx,         // width
             int cy,         // height
             uint uFlags);       // window positioning flags


        /* Used to deiconize an iconized window */
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int SendMessage(
            IntPtr hWnd,
            int wMsg,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern IntPtr ShellExecute(
            IntPtr hwnd,
            string lpOperation,
            string lpFile,
            string lpParameters,
            string lpDirectory,
            SW nShowCmd);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CreateProcess")]
        public static extern int _CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool TerminateProcess(int hProcess, int uExitCode);

        [DllImport("kernel32", SetLastError = true)]
        public static extern uint WaitForSingleObject(int hHandle, uint dwMilliseconds);

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool GetExitCodeProcess(int hProcess, ref int lpExitCode);

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int SHGetFileInfo(
          string pszPath,
          int dwFileAttributes,
          out SHFILEINFO psfi,
          uint cbfileInfo,
          SHGFI uFlags);

        [DllImport("User32.dll", SetLastError = true)]
        public static extern int DestroyIcon(System.IntPtr hIcon);

        [DllImport("Shell32", CharSet = CharSet.Auto, SetLastError = true)]
        public extern static int ExtractIconEx(
            [MarshalAs(UnmanagedType.LPTStr)] 
            string lpszFile,      //size of the icon
            int nIconIndex,       //index of the icon
            // (in case we have more
            // then 1 icon in the file
            IntPtr[] phIconLarge, //32x32 icon
            IntPtr[] phIconSmall, //16x16 icon
            int nIcons);          //how many to get

        [DllImport("advapi32", SetLastError = true)]
        public static extern bool OpenProcessToken(
            IntPtr ProcessHandle, // handle to process                                             
            int DesiredAccess, // desired access to process                                        
            ref IntPtr TokenHandle // handle to open access token                                  
        );

        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool GetTokenInformation(
            IntPtr TokenHandle,
            TOKEN_INFORMATION_CLASS TokenInformationClass,
            IntPtr TokenInformation,
            int TokenInformationLength,
            ref int ReturnLength
        );

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool ConvertSidToStringSid(
            IntPtr pSID,
            [In, Out, MarshalAs(UnmanagedType.LPTStr)] ref string pStringSid
        );

        [DllImport("advapi32", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool ConvertStringSidToSid(
            [In, MarshalAs(UnmanagedType.LPTStr)] string pStringSid,
            ref IntPtr pSID
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetFileInformationByHandle(IntPtr hFile,
           out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int UnhookWindowsHookEx(IntPtr hhook);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, uint wParam, IntPtr lParam);
        public delegate IntPtr HookProc(int nCode, uint wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreateIconIndirect(ref IconInfo icon);

        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern long StrFormatByteSize(long fileSize,
        [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessWorkingSetSize(
            IntPtr hProcess,
            Int32 dwMinimumWorkingSetSize,
            Int32 dwMaximumWorkingSetSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetForegroundWindow();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        /// <summary>
        /// Converts a Win32 error to an HRESULT.
        /// </summary>
        public static Int32 HRESULT_FROM_WIN32(Int32 win32Error)
        {
            const Int32 FACILITY_WIN32 = 7;
            UInt32 hr = win32Error <= 0
             ? (UInt32)(win32Error)
             : (((UInt32)(win32Error & 0x0000FFFF) | (FACILITY_WIN32 << 16) | 0x80000000));
            return (Int32)hr;
        }


        /// <summary>
        /// Return the value of the system's GetLastError() call.
        /// </summary>
        public static int GetLastError()
        {
            return System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        }

        /// <summary>
        /// Return the text value of the system's GetLastError() call.
        /// </summary>
        public static String GetLastErrorStringMessage()
        {
            try
            {
                return new Win32Exception(GetLastError()).Message;
            }
            catch (Exception ex)
            {
                return "<Unable to get last error message: " + ex.Message + ">";
            }
        }

        /// <summary>
        /// Return the Exception created from the GetLastError() system call.
        /// </summary>
        /// <returns></returns>
        public static Win32Exception LastErrorException()
        {
            return new Win32Exception(GetLastError());
        }

        public static Cursor CreateCursor(Icon bmp, int xHotSpot, int yHotSpot)
        {
            IntPtr ptr = bmp.Handle;
            IconInfo tmp = new IconInfo();
            GetIconInfo(ptr, ref tmp);
            tmp.xHotspot = xHotSpot;
            tmp.yHotspot = yHotSpot;
            tmp.fIcon = false;
            ptr = CreateIconIndirect(ref tmp);
            return new Cursor(ptr);
        }

        /// <summary>
        /// Show a little Help tooltip close to the mouse cursor.
        /// </summary>
        public static void ShowHelpTooltip(String text, Control sender)
        {
            Help.ShowPopup(sender, text, new Point(Cursor.Position.X, Cursor.Position.Y + 20));
        }

        public static void ShowInactiveTopmost(Form frm)
        {
            ShowWindow(frm.Handle, (int)WindowStatus.SW_SHOWNOACTIVATE);
            SetWindowPos(frm.Handle.ToInt32(), HWND_TOPMOST,
                frm.Left, frm.Top, frm.Width, frm.Height,
                SWP_NOACTIVATE);
        }

        public static void ScrollToBottom(IntPtr _controlHandle)
        {
            SendMessage(_controlHandle, (int)ScrollBarOptions.EM_SCROLL, (IntPtr)ScrollBarOptions.SB_PAGEBOTTOM, IntPtr.Zero);
        }

        /// <summary>
        /// Return the process SID, if it can be obtained. Otherwise, an empty string 
        /// is returned.
        /// </summary>
        public static String GetProcessSid(Process p)
        {
            IntPtr procTokenHandle = IntPtr.Zero;
            IntPtr pSid = IntPtr.Zero;
            String strSID = "";
            IntPtr tokenInfo = IntPtr.Zero;
            TOKEN_USER userToken;
            int tokenInfoLength = 0;

            try
            {
                // Open the process in order to fill out procToken.
                if (!OpenProcessToken(p.Handle, TOKEN_QUERY, ref procTokenHandle))
                    throw LastErrorException();

                // Get the required buffer size by making a first call that will
                // fail, then allocate tokenInfo accordingly and make the call again.
                if (!GetTokenInformation(
                        procTokenHandle,
                        TOKEN_INFORMATION_CLASS.TokenUser,
                        tokenInfo,
                        0,
                        ref tokenInfoLength))
                {
                    // ERROR_INSUFFICIENT_BUFFER is expected. Fail on anything else.
                    if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
                        throw LastErrorException();
                }

                // Allocate the right buffer, now that we know the requried size.
                tokenInfo = Marshal.AllocHGlobal(tokenInfoLength);

                // Make the real call.
                if (!GetTokenInformation(
                        procTokenHandle,
                        TOKEN_INFORMATION_CLASS.TokenUser,
                        tokenInfo,
                        tokenInfoLength,
                        ref tokenInfoLength))
                {
                    throw LastErrorException();
                }

                userToken = (TOKEN_USER)Marshal.PtrToStructure(tokenInfo, typeof(TOKEN_USER));
                pSid = userToken.User.Sid;

                ConvertSidToStringSid(pSid, ref strSID);
                return strSID;
            }

            catch (Exception)
            {
                return "";
            }

            finally
            {
                if (procTokenHandle != IntPtr.Zero) CloseHandle(procTokenHandle);
                if (tokenInfo != IntPtr.Zero) Marshal.FreeHGlobal(tokenInfo);
            }
        }

        /// <summary>
        /// Hackish method used to reduce the application's working set.
        /// </summary>
        public static void MinimizeAppWorkingSet()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
        }
    }

    public class ProcEndEventArgs : EventArgs
    {
        public int ExitCode;

        public ProcEndEventArgs(int exitCode)
        {
            ExitCode = exitCode;
        }
    }

    /// <summary>
    /// Process handling.
    /// </summary>
    public class KProcess
    {
        public String CommandLine;
        public KSyscalls.STARTUPINFO StartupInfo = new KSyscalls.STARTUPINFO();
        public KSyscalls.PROCESS_INFORMATION ProcessInfo = new KSyscalls.PROCESS_INFORMATION();
        public bool InheritHandles = false;
        public uint CreationFlags = (int)KSyscalls.CREATION_FLAGS.NORMAL_PRIORITY_CLASS;

        /// <summary>
        /// True if the process is running.
        /// </summary>
        private bool m_runningFlag = false;

        /// <summary>
        /// Reference to the worker thread used to wait for the process.
        /// </summary>
        private KWorkerThread m_thread = null;

        /// <summary>
        /// This event is fired in the context of the UI thread when the worker
        /// thread has completed its work.
        /// </summary>
        public event EventHandler<ProcEndEventArgs> ProcessEnd;

        private static void CreateProcess(String lpApplicationName,
                                          String lpCommandLine,
                                          bool bInheritHandles,
                                          uint dwCreationFlags,
                                          ref KSyscalls.STARTUPINFO lpStartupInfo,
                                          out KSyscalls.PROCESS_INFORMATION lpProcessInformation)
        {
            int retval;
            retval = KSyscalls._CreateProcess(lpApplicationName,
                                              lpCommandLine,
                                              new IntPtr(0), new IntPtr(0),
                                              bInheritHandles,
                                              dwCreationFlags,
                                              new IntPtr(0), null,
                                              ref lpStartupInfo,
                                              out lpProcessInformation);

            if (retval == 0)
            {
                int errcode = Marshal.GetLastWin32Error();
                throw new Exception("Cannot create process (" + errcode + ": " + KSyscalls.GetLastErrorStringMessage() + ")");
            }
        }

        public KProcess(string commandLine)
        {
            if (commandLine[0] == '"') CommandLine = commandLine;
            else
            {
                string[] splited = commandLine.Split(new char[] { ' ' }, 2);
                string progname;
                string arguments = "";
                switch (splited.Length)
                {
                    case 1:
                        progname = splited[0];
                        break;
                    case 2:
                        progname = splited[0];
                        arguments = splited[1];
                        break;
                    default:
                        throw new Exception("Invalid command line specified");
                }

                CommandLine = "\"" + progname + "\"" + arguments;
            }
        }

        /// <summary>
        /// Return true if the process is running.
        /// </summary>
        public bool IsRunning()
        {
            return m_runningFlag;
        }

        /// <summary>
        /// Start the process.
        /// </summary>
        public void Start()
        {
            Debug.Assert(m_runningFlag == false);
            ProcessInfo = new KSyscalls.PROCESS_INFORMATION();
            CreateProcess(null, CommandLine, InheritHandles, CreationFlags, ref StartupInfo, out ProcessInfo);
            m_runningFlag = true;
            m_thread = new KProcessThread(this);
            m_thread.Start();
        }

        /// <summary>
        /// Terminate the process if it is running. No notification will be
        /// delivered after Terminate() has been called.
        /// </summary>
        public void Terminate()
        {
            if (!m_runningFlag) return;
            m_runningFlag = false;
            KSyscalls.TerminateProcess(ProcessInfo.hProcess, 256);
        }

        /// <summary>
        /// This method is called in the context of the UI thread when the
        /// worker thread has completed.
        /// </summary>
        public void OnCompletion(int exitCode)
        {
            m_thread = null;

            // The caller has already terminated the process. Bail out.
            if (!m_runningFlag) return;

            m_runningFlag = false;
            if (ProcessEnd != null) ProcessEnd(this, new ProcEndEventArgs(exitCode));
        }
    }

    /// <summary>
    /// Worker thread used by KProcess.
    /// </summary>
    public class KProcessThread : KWorkerThread
    {
        /// <summary>
        /// Reference to the process.
        /// </summary>
        private KProcess m_process = null;

        /// <summary>
        /// Exit code set by the worker thread.
        /// </summary>
        private int m_exitCode = 1;


        public KProcessThread(KProcess process)
        {
            m_process = process;
        }

        protected override void Run()
        {
            uint reason = KSyscalls.WaitForSingleObject(m_process.ProcessInfo.hProcess, KSyscalls.INFINITE);
            Debug.Assert(reason == (uint)KSyscalls.WAIT_RETURN_REASON.WAIT_OBJECT_0);
            KSyscalls.GetExitCodeProcess(m_process.ProcessInfo.hProcess, ref m_exitCode);
        }

        protected override void OnCompletion()
        {
            // Ignore failures here.
            m_process.OnCompletion(m_exitCode);
        }
    }
}
