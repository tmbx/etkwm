using kcslib;
using System;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using ShellLib;
using System.Xml;
using System.Threading;

namespace kwmlib
{
    /// <summary>
    /// Catch-all utility class.
    /// </summary>
    public static class KwmUtil
    {
        /// <summary>
        /// Return true if the name specified is a valid workspace name.
        /// </summary>
        public static bool IsValidKwsName(String name)
        {
            String trimmedName = name.Trim();
            if (String.IsNullOrEmpty(trimmedName)) return false;
            // FIXME : what about Greek/other weird unicode chars?
            // need a unified way of testing this, since KFS also needs it.
            Regex reg = new Regex("[\\\\/:*?\"<>|]");
            return (!reg.IsMatch(trimmedName));
        }

        public static String KwmVersion
        {
            get
            {
                RegistryKey version = null;
                try
                {
                    version = KwmReg.GetKwmLMRegKey();
                }
                catch (Exception ex)
                {
                    KLogging.LogException(ex);
                }

                if (version == null)
                {
                    KLogging.Log(2, "Unable to find " + KwmStrings.Kwm + " version.");
                    return "Unknown";
                }
                else
                {
                    return (String)version.GetValue("KWM_Version", "Unknown");
                }
            }
        }

        /// <summary>
        /// Open a given file using the Windows Shell. 
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>True on error, false otherwise.</returns>
        public static bool OpenFileInWindowsShell(String fullPath, ref String errorMsg)
        {
            bool error = false;

            try
            {
                Process p = new Process();
                p.StartInfo.UseShellExecute = true;
                p.StartInfo.FileName = fullPath;
                p.Start();
            }

            catch (Win32Exception)
            {
                KLogging.Log("OpenFile(): Caught Win32Exception");
                // 1155 is the following error :
                // No application is associated with the specified file for this operation
                // Show the Open as... dialog in this case.
                if (KSyscalls.GetLastError() == 1155)
                {
                    KSyscalls.SHELLEXECUTEINFO info = new KSyscalls.SHELLEXECUTEINFO();
                    info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(info);
                    info.lpDirectory = Path.GetDirectoryName(fullPath);
                    info.lpFile = Path.GetFileName(fullPath);
                    info.nShow = (int)KSyscalls.SW.SW_SHOWDEFAULT;
                    info.lpVerb = "openas";
                    info.fMask = KSyscalls.SEE_MASK.ASYNCOK;
                    error = !KSyscalls.ShellExecuteEx(ref info);
                }

                else error = true;

                if (error) KSyscalls.GetLastErrorStringMessage();
            }

            catch (Exception ex)
            {
                errorMsg = ex.Message;
                error = true;
            }

            return error;
        }

        /// <summary>
        /// Open a given file using the Windows Shell in a worker thread (Fire and forget
        /// way).
        /// </summary>
        public static void OpenFileInWorkerThread(String p)
        {
            OpenFileThread t = new OpenFileThread(p);
            t.Start();
        }

        /// <summary>
        /// Copy the given file to the target directory. If the filename
        /// already exists, rename the destination file to a unique name.
        /// </summary>
        /// <param name="SourceFilePath">Full path of the file to copy.</param>
        /// <param name="DestDirPath">Full path to the target directory, backslash-terminated.</param>
        public static ShellFileOperation.CopyFileResult CopyFileAndRenameOnCol(
            String SourceFilePath, String DestFilePath, ref String errorMessage)
        {
            String srcFileName = Path.GetFileName(SourceFilePath);
            String srcFileNameNoExt = Path.GetFileNameWithoutExtension(SourceFilePath);
            String srcFileNameExt = Path.GetExtension(SourceFilePath);

            String dstFileName = srcFileName;

            if (File.Exists(DestFilePath + dstFileName))
            {
                int i = 1;
                while (File.Exists(DestFilePath + srcFileNameNoExt + " (" + i + ")" + srcFileNameExt))
                    i++;

                dstFileName = srcFileNameNoExt + " (" + i + ")" + srcFileNameExt;
            }


            List<String> S = new List<string>();
            List<String> D = new List<string>();

            S.Add(SourceFilePath);
            D.Add(DestFilePath + dstFileName);

            return CopyFiles(S, D, false, true, false, false, true, false, null, ref errorMessage);
        }

        /// <summary>
        /// Copy one file using the Shell API. Set LockUI to true if you want frmMain to be
        /// disabled during the operation, since the progress dialog is not modal.
        /// </summary>
        public static ShellFileOperation.CopyFileResult CopyFile(String SourceFilePath,
                                                                 String DestFilePath,
                                                                 bool HideUiErrors,
                                                                 bool SilentOverwrite,
                                                                 bool RenameDestOnCollision,
                                                                 bool SimpleProgress,
                                                                 bool LockUI,
                                                                 Control owner,
                                                                 ref String errorMessage)
        {
            List<String> S = new List<string>();
            List<String> D = new List<string>();

            S.Add(SourceFilePath);
            D.Add(DestFilePath);

            return CopyFiles(S, D, HideUiErrors, SilentOverwrite, RenameDestOnCollision, SimpleProgress, false, LockUI, owner, ref errorMessage);
        }

        /// <summary>
        /// Copy one file using the Shell API. The progress dialog displayed is not modal.
        /// </summary>
        /// <param name="Sources">Source paths of the files to copy.</param>
        /// <param name="Destinations">Destination paths of the files to copy.</param>
        /// <param name="HideUiErrors">No user interface will be displayed if an error occurs.</param>
        /// <param name="SilentOverwrite">Do not prompt the user before overwritting the destination file. </param>
        /// <param name="RenameDestOnCollision">Give the file being operated on a new name in a move, copy, or rename operation if a file with the target name already exists.</param>
        /// <param name="SimpleProgress">Displays a progress dialog box but does not show the file names.</param>
        /// <param name="Silent">Does not display a progress dialog box. </param>
        /// <param name="LockUI">Set to true if you want the entire application to be disabled during the operation.</param>
        /// <returns>True if the operation completed with success, false if the user aborted or if an error occured.</returns>
        public static ShellFileOperation.CopyFileResult CopyFiles(List<String> Sources,
                                                                  List<String> Destinations, 
                                                                  bool HideUiErrors,
                                                                  bool SilentOverwrite,
                                                                  bool RenameDestOnCollision,
                                                                  bool SimpleProgress, 
                                                                  bool Silent, 
                                                                  bool LockUI,
                                                                  Control owner,
                                                                  ref String errorMessage)
        {
            // Prepare the Shell.
            ShellLib.ShellFileOperation FileOp = new ShellLib.ShellFileOperation();

            if (HideUiErrors)
                FileOp.OperationFlags |= ShellLib.ShellFileOperation.ShellFileOperationFlags.FOF_NOERRORUI;

            if (RenameDestOnCollision)
                FileOp.OperationFlags |= ShellLib.ShellFileOperation.ShellFileOperationFlags.FOF_RENAMEONCOLLISION;

            if (SilentOverwrite)
                FileOp.OperationFlags |= ShellLib.ShellFileOperation.ShellFileOperationFlags.FOF_NOCONFIRMATION;

            if (SimpleProgress)
                FileOp.OperationFlags |= ShellLib.ShellFileOperation.ShellFileOperationFlags.FOF_SIMPLEPROGRESS;

            if (owner != null) FileOp.OwnerWindow = owner.Handle;
            FileOp.SourceFiles = Sources.ToArray();
            FileOp.DestFiles = Destinations.ToArray();

            // Disable the entire application while the copy is being done, if required.
            // Necessary since we can't make the progress dialog modal.
            try
            {
                if (LockUI) owner.Enabled = false;

                return FileOp.DoOperation(ref errorMessage);
            }

            finally
            {
                if (LockUI) owner.Enabled = true;
            }
        }
    }

    /// <summary>
    /// KWM string definitions.
    /// </summary>
    public static class KwmStrings
    {
        public const String Kwm = "Teambox Manager";
        public const String Kws = "Teambox";
        public const String Kwses = "Teamboxes";
        public const String BuyNowUrl = "https://www.teambox.co/";
        public const String MoreInfos = "http://www.teambox.co/downloads/";
        public const String PwdPrompt = "You are inviting people to a Secure Teambox. You must assign a password to the following users:";
        
        public const String StdKwsDescription =
            "A Standard " + Kws + " can be accessed by users " +
            "who receive a " + Kws + " invitation email without " +
            "having to supply a password.";
        
        public const String SecureKwsDescription =
            "A Secure " + Kws +
            " ensures an enhanced level of security by requiring a " +
            "password-based authentication for every user.";
    }

    /// <summary>
    /// Definitions of various paths used by the KWM that are not 
    /// user-modifiable.
    /// </summary>
    public static class KwmPath
    {
        /// <summary>
        /// Return where the KWM is installed on the file system, backslash-terminated..
        /// </summary>
        public static String GetKwmInstallationPath()
        {
            string defaultVal = @"c:\program files\teambox\Teambox Manager";

            RegistryKey kwmKey = null;
            try
            {
                kwmKey = KwmReg.GetKwmLMRegKey();
                return (String)kwmKey.GetValue("InstallDir", defaultVal) + "\\";
            }
            catch (Exception ex)
            {
                KLogging.LogException(ex);
                return defaultVal;
            }
            finally
            {
                if (kwmKey != null) kwmKey.Close();
            }
        }

        /// <summary>
        /// Full path to ktlstunnel.exe.
        /// </summary>
        public static String KwmKtlstunnelPath { get { return KwmPath.GetKwmInstallationPath() + @"ktlstunnel\ktlstunnel.exe"; } }

        /// <summary>
        /// Full path to the quick start document.
        /// </summary>
        public static String KwmQuickStartPath { get { return GetKwmInstallationPath() + "quickstart.pdf"; } }

        /// <summary>
        /// Full path to the user guide document.
        /// </summary>
        public static String KwmUserGuidePath { get { return GetKwmInstallationPath() + "userguide.pdf"; } }

        public static String GetKcsLocalDataPath()
        {
            String appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return appData + "\\teambox\\kcs\\";
        }

        /// <summary>
        /// Return the path to the KWM information file.
        /// </summary>
        public static String GetKwmInfoPath()
        {
            return GetKcsLocalDataPath() + "info.txt";
        }

        public static String GetKcsLogFilePath()
        {
            return GetKcsLocalDataPath() + "logs\\";
        }

        public static String GetKwmLogFilePath()
        {
            return GetKcsLogFilePath() + "kwm\\";
        }

        public static String GetKmodDirPath()
        {
            return GetKcsLogFilePath() + "kmod";
        }

        public static String GetKtlstunnelLogFilePath()
        {
            return GetKcsLogFilePath() + "ktlstunnel\\";
        }

        public static String GetLogFileName()
        {
            return String.Format("{0:yyyy_MM_dd-HH_mm_ss}", DateTime.Now) + ".log";
        }

        public static String GetKcsRoamingDataPath()
        {
            String appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return appData + "\\teambox\\kcs\\";
        }

        public static String GetKwmRoamingStatePath()
        {
            return GetKcsRoamingDataPath() + "kwm\\state\\";
        }

        // FIXME Using temporary DB path to avoid clashes with regular KWM.
        public static String GetKwmDbPath()
        {
            return GetKwmRoamingStatePath() + "kwm.db";
        }

        public static String GetKfsDefaultStorePath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Teambox Shares\\";
        }
    }

    /// <summary>
    /// Catch-all registry methods.
    /// </summary>
    public static class KwmReg
    {
        /// <summary>
        /// KWM registry root (both in HKCU and HKLM).
        /// </summary>
        public static String GetKwmRegKeyString()
        {
            return @"Software\Teambox\Teambox Manager";
        }

        /// <summary>
        /// Open and return the user's KWM configuration registry key, 
        /// creating it if it does not already exist. Caller must make sure
        /// to close the returned value after use.
        /// </summary>
        public static RegistryKey GetKwmCURegKey()
        {
            RegistryKey regKey = Registry.CurrentUser.CreateSubKey(GetKwmRegKeyString());
            if (regKey == null)
                throw new Exception("Unable to read or create the registry key HKCU\\" + KwmReg.GetKwmRegKeyString() + ".");
            return regKey;
        }

        /// <summary>
        /// Open and return the local machine's KWM configuration registry key.
        /// If the key does not exist, throw an exception.
        /// Caller must make sure to close the returned value after use.
        /// </summary>
        public static RegistryKey GetKwmLMRegKey()
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(GetKwmRegKeyString());
            if (key == null)
                throw new Exception("Unable to read or create the registry key HKLM\\" + KwmReg.GetKwmRegKeyString() + ".");
            return key;
        }

        public static String GetVncServerRegKey()
        {
            return KwmReg.GetKwmRegKeyString() + "\\vnc";
        }
    }

    /// <summary>
    /// This class contains the KWM application user settings.
    /// 
    /// Since the user settings can be used by multiple threads, there are 
    /// synchronization issues to be careful of. One simple way of avoiding
    /// trouble is to keep a single instance of this class in a static volatile
    /// member of a static class and to never modify its content again.
    /// 
    /// To update the settings, spawn a new instance of this class and reload 
    /// the data from the registry. This handles the case where the data has
    /// been updated by an external process. Then, update the data of the new
    /// instance, update the registry and replace the reference to the old 
    /// instance with the reference to the new instance. Since the reference is
    /// declared volatile this won't cause problems.
    /// </summary>
    public class KwmCfg
    {
        /// <summary>
        /// Current value of the configuration.
        /// </summary>
        public static volatile KwmCfg Cur = null;

        /// <summary>
        /// Address of the KPS.
        /// </summary>
        public String KpsAddr;

        /// <summary>
        /// User name on the KPS.
        /// </summary>
        public String KpsUserName;

        /// <summary>
        /// Login token returned by the KPS.
        /// </summary>
        public String KpsLoginToken;

        /// <summary>
        /// Powers of the user listed in the last KPS login ticket obtained.
        /// </summary>
        public UInt32 KpsUserPower;

        /// <summary>
        /// Address of the KCD listed in the last KPS login ticket obtained.
        /// </summary>
        public String KpsKcdAddr;

        /// <summary>
        /// Enable KWM debugging.
        /// </summary>
        public bool KwmDebuggingFlag;

        /// <summary>
        /// Log KWM messages to a file.
        /// </summary>
        public bool KwmLogToFileFlag;

        /// <summary>
        /// Debugging level of ktlstunnel.
        /// </summary>
        public int KtlstunnelLoggingLevel;

        /// <summary>
        /// Custom KCD address. This overrides the KCD address obtained from
        /// the KPS.
        /// </summary>
        public String CustomKcdAddress;

        /// <summary>
        /// True if the custom KCD address should be used.
        /// </summary>
        public bool CustomKcdFlag;

        /// <summary>
        /// Path to the directory where the KFS shares are created. If empty,
        /// this defaults to a path in the application data folder of the user. 
        /// </summary>
        public String KfsStorePath;

        /// <summary>
        /// True if notifications should be shown.
        /// </summary>
        public bool ShowNotificationFlag;

        /// <summary>
        /// Notification fade delay, in milliseconds.
        /// </summary>
        public int NotificationDelay;

        /// <summary>
        /// True if the workspaces created should have the thin KFS flag set
        /// by default.
        /// </summary>
        public bool NewKwsThinKfsDefaultFlag;

        /// <summary>
        /// True if a warning should be displayed for screen sharing sessions
        /// where the remote user can control the computer.
        /// </summary>
        public bool AppSharingWarnOnSupportSessionFlag;

        /// <summary>
        /// True if the wizard must not be shown to the user when the KWM 
        /// starts. The wizard should be shown until the user configures his
        /// KWM.
        /// </summary>
        public bool NoAutomaticWizardFlag;

        /// <summary>
        /// True if this KWM should offer the user the choice to use the
        /// Freemium registration interface.
        /// </summary>
        public bool AllowFreemiumFlag;

        /// <summary>
        /// True if the configuration wizard must skip the Freemium 
        /// registration page and go directly to the sign-in page. Useful when
        /// a customer wants to deploy the KWM on-site.
        /// </summary>
        public bool SkipRegistrationFlag;


        /// <summary>
        /// Fired whenever the registry is written. Used to notify any
        /// interested party that some settings may have changed, such as
        /// Outlook. Event delegates will always be invoked in the UI thread.
        /// </summary>
        public static event EventHandler OnRegistryWritten;

        /// <summary>
        /// Return an instance of this class having the values read from the
        /// registry.
        /// </summary>
        public static KwmCfg Spawn()
        {
            KwmCfg self = new KwmCfg();
            self.ReadRegistry();
            return self;
        }

        /// <summary>
        /// Write the values of this instance in the Registry and set this
        /// instance has the current configuration instance.
        /// </summary>
        public void Commit()
        {
            WriteRegistry();
            Cur = this;
        }

        /// <summary>
        /// Read the values of this instance from the current user's registry.
        /// </summary>
        public void ReadRegistry()
        {
            RegistryKey regKey = null;
            try
            {
                regKey = KwmReg.GetKwmCURegKey();
                KpsAddr = (String)regKey.GetValue("KpsAddr", "");
                KpsUserName = (String)regKey.GetValue("KpsUserName", "");
                KpsLoginToken = (String)regKey.GetValue("KpsLoginToken", "");
                KpsUserPower = (UInt32)(Int32)regKey.GetValue("KpsUserPower", 0);
                KpsKcdAddr = (String)regKey.GetValue("KpsKcdAddr", "");
                KwmDebuggingFlag = ((Int32)regKey.GetValue("KwmDebuggingFlag", 0) > 0);
                KwmLogToFileFlag = ((Int32)regKey.GetValue("KwmLogToFileFlag", 0) > 0);
                KtlstunnelLoggingLevel = (Int32)regKey.GetValue("KtlstunnelLoggingLevel", 0);
                CustomKcdAddress = (String)regKey.GetValue("CustomKcdAddress", "");
                CustomKcdFlag = ((Int32)regKey.GetValue("CustomKcdFlag", 0) > 0);
                KfsStorePath = (String)regKey.GetValue("KfsStorePath", "");
                ShowNotificationFlag = ((Int32)regKey.GetValue("ShowNotificationFlag", 1) > 0);
                NotificationDelay = (Int32)regKey.GetValue("NotificationDelay", 8000);
                NewKwsThinKfsDefaultFlag = ((Int32)regKey.GetValue("NewKwsThinKfsDefaultFlag", 1) > 0);
                AppSharingWarnOnSupportSessionFlag = ((Int32)regKey.GetValue("AppSharingWarnOnSupportSessionFlag", 1) > 0);
                NoAutomaticWizardFlag = ((Int32)regKey.GetValue("NoAutomaticWizardFlag", 0) > 0);
                AllowFreemiumFlag = ((Int32)regKey.GetValue("AllowFreemiumFlag", 1) > 0);
                SkipRegistrationFlag = ((Int32)regKey.GetValue("SkipRegistrationFlag", 0) > 0);
            }

            catch (Exception ex)
            {
                KBase.HandleException(ex, true);
            }

            finally
            {
                if (regKey != null) regKey.Close();
            }
        }

        /// <summary>
        /// Write the values of this instance to the current user's registry.
        /// </summary>
        public void WriteRegistry()
        {
            RegistryKey regKey = null;
            try
            {
                regKey = KwmReg.GetKwmCURegKey();
                regKey.SetValue("KpsAddr", KpsAddr);
                regKey.SetValue("KpsUserName", KpsUserName);
                regKey.SetValue("KpsLoginToken", KpsLoginToken);
                regKey.SetValue("KpsUserPower", (Int32)KpsUserPower);
                regKey.SetValue("KpsKcdAddr", KpsKcdAddr);
                regKey.SetValue("KwmDebuggingFlag", Convert.ToInt32(KwmDebuggingFlag));
                regKey.SetValue("KwmLogToFileFlag", Convert.ToInt32(KwmLogToFileFlag));
                regKey.SetValue("KtlstunnelLoggingLevel", KtlstunnelLoggingLevel);
                regKey.SetValue("CustomKcdAddress", CustomKcdAddress);
                regKey.SetValue("CustomKcdFlag", Convert.ToInt32(CustomKcdFlag));
                regKey.SetValue("KfsStorePath", KfsStorePath);
                regKey.SetValue("ShowNotificationFlag", Convert.ToInt32(ShowNotificationFlag));
                regKey.SetValue("NotificationDelay", NotificationDelay);
                regKey.SetValue("NewKwsThinKfsDefaultFlag", Convert.ToInt32(NewKwsThinKfsDefaultFlag));
                regKey.SetValue("AppSharingWarnOnSupportSessionFlag", Convert.ToInt32(AppSharingWarnOnSupportSessionFlag));
                regKey.SetValue("NoAutomaticWizardFlag", Convert.ToInt32(NoAutomaticWizardFlag));
                regKey.SetValue("AllowFreemiumFlag", Convert.ToInt32(AllowFreemiumFlag));
                regKey.SetValue("SkipRegistrationFlag", Convert.ToInt32(SkipRegistrationFlag));
                if (OnRegistryWritten != null) OnRegistryWritten(this, new EventArgs());
            }

            catch (Exception ex)
            {
                KBase.HandleException(ex, true);
            }

            finally
            {
                if (regKey != null) regKey.Close();
            }
        }

        /// <summary>
        /// Return the effective address of the KCD.
        /// </summary>
        public String GetKcdAddress()
        {
            if (CustomKcdFlag) return CustomKcdAddress;
            return KpsKcdAddr;
        }

        /// <summary>
        /// Return true if the registry contains the necessary informations to 
        /// login on a KPS.
        /// </summary>
        public bool CanLoginOnKps()
        {
            return (KpsAddr != "" && KpsUserName != "" && KpsLoginToken != "");
        }

        /// <summary>
        /// Return true if the user can create a workspace.
        /// </summary>
        public bool CanCreateKws()
        {
            return (CanLoginOnKps() && GetKcdAddress() != "");
        }
    }

    /// <summary>
    /// This class allows for opening a file in a worker thread. It provides
    /// a kind of "Fire and forget" way to do so: failure will be reported from
    /// that worker thread only.
    /// </summary>
    public class OpenFileThread : KWorkerThread
    {
        /// <summary>
        /// Full path to the file to be opened.
        /// </summary>
        private String m_path;

        public OpenFileThread(String path)
        {
            m_path = path;
        }

        protected override void Run()
        {
            String msg = null; 
            if (KwmUtil.OpenFileInWindowsShell(m_path, ref msg))
            {
                KLogging.Log(2, msg);
                MessageBox.Show("Unable to open " + m_path + Environment.NewLine + msg,
                                "Error on file open", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>
    /// Excellent discussion about timers in .NET here :
    /// http://msdn.microsoft.com/en-us/magazine/cc164015.aspx.
    /// Note that the callback method may be called even if the timer has
    /// been disabled due to race conditions.
    /// </summary>
    public class KWakeupTimer
    {
        private System.Threading.Timer m_timer;

        public delegate void OnTimerWakeUpDelegate(object[] args);

        /// <summary>
        /// Callback invoked in the UI thread on every timer tick.
        /// </summary>
        public OnTimerWakeUpDelegate TimerWakeUpCallback;

        public Object[] Args = null;

        public KWakeupTimer()
        {
            m_timer = new System.Threading.Timer(
                new TimerCallback(HandleOnTimerElapsed),
                null,
                Timeout.Infinite,
                Timeout.Infinite);
        }

        /// <summary>
        /// Wake me in x milliseconds. 
        /// 0 to wake me ASAP, -1 to disable.
        /// </summary>
        public void WakeMeUp(long milliseconds)
        {
            // If the timer is already enabled when the Start 
            // method is called, the interval is reset. 
            if (milliseconds == -1)
                m_timer.Change(Timeout.Infinite, Timeout.Infinite);
            else
                m_timer.Change(milliseconds, Timeout.Infinite);
        }

        private void HandleOnTimerElapsed(object source)
        {
            try
            {
                if (TimerWakeUpCallback != null) KBase.ExecInUI(TimerWakeUpCallback, new object[] { Args });
            }

            catch (Exception ex)
            {
                KLogging.LogException(ex);
            }
        }
    }

    /// <summary>
    /// Helper methods to handle XML documents.
    /// </summary>
    public static class KwmXml
    {
        /// <summary>
        /// Create a new XmlElement and add it to the specified parent, if any.
        /// </summary>
        public static XmlElement CreateXmlElement(XmlDocument doc, XmlElement parent, String name, String text)
        {
            XmlElement elem = doc.CreateElement(name);
            if (text != "")
            {
                XmlText txtElem = doc.CreateTextNode(text);
                elem.AppendChild(txtElem);
            }

            if (parent != null)
                parent.AppendChild(elem);

            return elem;
        }

        /// <summary>
        /// Return the child element 'name' in 'parent', if any.
        /// </summary>
        public static XmlElement GetXmlChildElement(XmlElement parent, String name)
        {
            XmlNodeList list = parent.GetElementsByTagName(name);
            return (list.Count == 0) ? null : list.Item(0) as XmlElement;
        }

        /// <summary>
        /// Return the value associated to the child element 'name' in
        /// 'parent'. The string 'defaultText' is returned if the child
        /// element is not found.
        /// </summary>
        public static String GetXmlChildValue(XmlElement parent, String name, String defaultText)
        {
            XmlElement elem = GetXmlChildElement(parent, name);
            return (elem == null) ? defaultText : elem.InnerText;
        }
    }
}