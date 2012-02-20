using System;
using System.Text;
using System.IO;

namespace kcslib
{
    /// <summary>
    /// This class contains static methods to manipulate paths and obtain
    /// file information.
    /// </summary>
    public static class KFile
    {
        /// <summary>
        /// Return a reference to the StringComparer object used to perform 
        /// case-insensitive string comparisons.
        /// </summary>
        public static StringComparer Comparer { get { return StringComparer.OrdinalIgnoreCase; } }

        /// <summary>
        /// Return true if the two strings specified are equal without 
        /// considering the case.
        /// </summary>
        public static bool Eq(String a, String b)
        {
            return Comparer.Equals(a, b);
        }

        /// <summary>
        /// Obtain the low-level information of the file stream specified.
        /// </summary>
        public static void GetLowLevelFileInfo(FileStream stream, out UInt64 fileID, out UInt64 fileSize, out DateTime fileDate)
        {
            KSyscalls.BY_HANDLE_FILE_INFORMATION bhfi;
            KSyscalls.GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(), out bhfi);
            fileID = (bhfi.FileIndexHigh << 32) + bhfi.FileIndexLow;
            fileSize = (bhfi.FileSizeHigh << 32) + bhfi.FileSizeLow;
            fileDate = DateTime.FromFileTime((Int64)(((UInt64)bhfi.LastWriteTime.dwHighDateTime << 32) +
                                                     (UInt64)bhfi.LastWriteTime.dwLowDateTime));
        }

        /// <summary>
        /// Return the last modification date of a given file system path.
        /// If the path leads to a directory, return the creation time of the directory.
        /// </summary>
        public static DateTime GetLastModificationDate(string fullPath)
        {
            FileStream stream = null;
            try
            {
                if (File.Exists(fullPath))
                {
                    KSyscalls.BY_HANDLE_FILE_INFORMATION bhfi;
                    stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    KSyscalls.GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(), out bhfi);
                    DateTime observedDate =
                        DateTime.FromFileTime((Int64)(((UInt64)bhfi.LastWriteTime.dwHighDateTime << 32) +
                                                      (UInt64)bhfi.LastWriteTime.dwLowDateTime));
                    return observedDate;
                }
                else if (Directory.Exists(fullPath))
                {
                    return new DirectoryInfo(fullPath).CreationTime.ToLocalTime();
                }
                else
                {
                    return DateTime.MinValue;
                }
            }
            catch (IOException)
            {
                // If we can't open the file directly, try going with the fileinfo method.
                try
                {
                    return new FileInfo(fullPath).LastWriteTime.ToLocalTime();
                }
                catch (Exception)
                {
                    return DateTime.MinValue;
                }
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                    stream = null;
                }
            }
        }

        /// <summary>
        /// Try hard to obtain the real size of the file specified.
        /// </summary>
        public static UInt64 GetFileSize(String path)
        {
            FileStream stream = null;
            try
            {
                KSyscalls.BY_HANDLE_FILE_INFORMATION bhfi;
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                KSyscalls.GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(), out bhfi);
                return ((UInt64)bhfi.FileSizeHigh << 32) + bhfi.FileSizeLow;
            }
            catch (IOException)
            {
                // If we can't open the file directly, try going with the fileinfo method.
                try
                {
                    return GetFileSizeFast(path);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                    stream = null;
                }
            }
        }

        /// <summary>
        /// Return of the file specified using FileInfo. This is less reliable 
        /// than GetFileSize().
        /// </summary>
        public static UInt64 GetFileSizeFast(String path)
        {
            return (UInt64)(new FileInfo(path)).Length;
        }

        /// <summary>
        /// Return a human-readable file size.
        /// </summary>
        public static String GetHumanFileSize(UInt64 size)
        {
            StringBuilder sb = new StringBuilder(11);
            KSyscalls.StrFormatByteSize((long)size, sb, sb.Capacity);
            return sb.ToString();
        }

        /// <summary>
        /// Test if fileName contains invalid characters for a Windows file name. 
        /// </summary>
        public static bool IsValidFileName(String fileName)
        {
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1 ||
                fileName.Length == 0 ||
                fileName.StartsWith(" "))
            {
                return false;
            }

            try
            {
                Encoding latinEuropeanEncoding = Encoding.GetEncoding("iso-8859-1", EncoderExceptionFallback.ExceptionFallback, DecoderExceptionFallback.ExceptionFallback);
                Encoding uniCode = Encoding.Unicode;
                Encoding.Convert(uniCode, latinEuropeanEncoding, uniCode.GetBytes(fileName));
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// This method converts every backslash in the path to slash. 
        /// If the slashTerminated param is true, the path is appended a trailing
        /// delimiter if necessary, otherwise it is removed if necessary.
        /// </summary>
        /// <param name="pathToConvert">The path to convert.</param>
        /// <param name="slashTerminated">True if a trailing delimiter must be appended.</param>
        public static String GetUnixFilePath(String pathToConvert, bool slashTerminated)
        {
            String tempPath = pathToConvert.Replace("\\", "/");
            if (tempPath.Length > 0)
            {
                if (slashTerminated)
                {
                    if (tempPath[tempPath.Length - 1] != '/')
                    {
                        tempPath = tempPath + "/";
                    }
                }
                else
                {
                    if (tempPath[tempPath.Length - 1] == '/')
                    {
                        tempPath = tempPath.Substring(0, tempPath.Length - 1);
                    }
                }
            }
            return tempPath;
        }

        /// <summary>
        /// This method converts every slash in the path to backslashes. 
        /// If the backslashTerminated param is true, the path is appended a trailing
        /// delimiter if necessary, otherwise it is removed if necessary.
        /// </summary>
        /// <param name="pathToConvert">The path to convert.</param>
        /// <param name="backslashTerminated">True if a trailing delimiter must be appended.</param>
        public static String GetWindowsFilePath(String pathToConvert, bool backslashTerminated)
        {
            String tempPath = pathToConvert.Replace("/", "\\");
            if (tempPath.Length > 0)
            {
                if (backslashTerminated)
                {
                    if (tempPath[tempPath.Length - 1] != '\\')
                    {
                        tempPath = tempPath + "\\";
                    }
                }
                else
                {
                    if (tempPath[tempPath.Length - 1] == '\\')
                    {
                        tempPath = tempPath.Substring(0, tempPath.Length - 1);
                    }
                }
            }
            return tempPath;
        }

        /// <summary>
        /// Return a list with each portion of the path. 
        /// Example : a\b\c\allo.txt
        /// The list returned is:
        /// |a|b|c|allo.txt|
        /// 
        /// The function doesn't care whether the path is a UNIX or a Windows path. It
        /// splits portions at slashses and backslashes. The function does not work on
        /// absolute paths.
        /// </summary>
        public static String[] SplitRelativePath(String relativePath)
        {
            return relativePath.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Return the directory portion of the path specified. The directory
        /// portion will have a trailing delimiter if it is non-empty.
        /// </summary>
        public static String DirName(String path)
        {
            if (path == "") return "";
            int LastIndex = path.Length - 1;
            for (; LastIndex > 0 && !IsDelim(path[LastIndex]); LastIndex--) { }
            if (!IsDelim(path[LastIndex])) return "";
            return path.Substring(0, LastIndex + 1);
        }

        /// <summary>
        /// Return the file portion of the path specified.
        /// </summary>
        public static String BaseName(String path)
        {
            if (path == "") return "";
            int LastIndex = path.Length - 1;
            for (; LastIndex > 0 && !IsDelim(path[LastIndex]); LastIndex--) { }
            if (!IsDelim(path[LastIndex])) return path;
            return path.Substring(LastIndex + 1, path.Length - LastIndex - 1);
        }

        /// <summary>
        /// Add a trailing slash to the path specified if the path is non-empty
        /// and it does not already end with a delimiter.
        /// </summary>
        public static String AddTrailingSlash(String path)
        {
            return AddTrailingSlash(path, false);
        }

        /// <summary>
        /// Add a trailing slash to the path specified if the path does not already 
        /// end with a delimiter, or if the path is empty and slashIfEmpty is set to true.
        /// </summary>
        public static String AddTrailingSlash(String path, bool slashIfEmpty)
        {
            if (path == "")
            {
                if (slashIfEmpty)
                    return "/";
                else
                    return "";
            }

            if (IsDelim(path[path.Length - 1])) return path;
            return path + "/";
        }

        /// <summary>
        /// Add a trailing backslash to the path specified if the path is non-empty
        /// and it does not already end with a delimiter.
        /// </summary>
        public static String AddTrailingBackslash(String path)
        {
            if (path == "") return "";
            if (IsDelim(path[path.Length - 1])) return path;
            return path + @"\";
        }

        /// <summary>
        /// Remove the trailing delimiter from the string specified, if there
        /// is one.
        /// </summary>
        public static String StripTrailingDelim(String path)
        {
            if (path == "") return "";
            if (IsDelim(path[path.Length - 1])) return path.Substring(0, path.Length - 1);
            return path;
        }

        /// <summary>
        /// Return true if the character specified is a slash or a backslash.
        /// </summary>
        public static bool IsDelim(Char c)
        {
            return (c == '/' || c == '\\');
        }

        /// <summary>
        /// Move the file or directory specified at the location specified.
        /// Be careful: if only the case is changed, Windows Explorer will
        /// NOT report the change even though it has been made. For 
        /// directories, be careful: both paths must be absolute and have 
        /// the same format. This method handles case sensitivity issues. 
        /// I hate all the fucktards at Microsoft.
        /// </summary>
        public static void MovePath(bool dirFlag, String src, String dst)
        {
            // Work around directory already exists crap.
            if (dirFlag && Eq(src, dst))
            {
                int i = 0;
                String tmpPath;
                while (true)
                {
                    tmpPath = dst + i++;
                    if (!File.Exists(tmpPath) && !Directory.Exists(tmpPath)) break;
                }
                Directory.Move(src, tmpPath);
                src = tmpPath;
            }

            if (dirFlag) Directory.Move(src, dst);
            else File.Move(src, dst);
        }

        /// <summary>
        /// Copy an existing file to a new file. Overwriting a file of the same name is allowed.
        /// This method makes sure the target directory exists before making the actual copy.
        /// </summary>
        public static void SafeCopy(String source, String dest, bool overwrite)
        {
            String destDir = Path.GetDirectoryName(dest);
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            File.Copy(source, dest, overwrite);
        }

        /// <summary>
        /// Copies the content of the given directory to another.
        /// </summary>
        public static void CopyDirContent(string sourceDirectory, string targetDirectory)
        {
            if (sourceDirectory == null)
                throw new ArgumentNullException("sourceDirectory");
            if (targetDirectory == null)
                throw new ArgumentNullException("targetDirectory");

            // Call the recursive method.
            CopyAllFiles(new DirectoryInfo(sourceDirectory), new DirectoryInfo(targetDirectory));
        }

        // Copies all files from one directory to another.
        private static void CopyAllFiles(DirectoryInfo source, DirectoryInfo target)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (target == null)
                throw new ArgumentNullException("target");

            // If the source doesn't exist, we have to throw an exception.
            if (!source.Exists)
                throw new DirectoryNotFoundException("Source directory not found: " + source.FullName);
            // If the target doesn't exist, we create it.
            if (!target.Exists)
                target.Create();

            // Get all files and copy them over.
            foreach (FileInfo file in source.GetFiles())
            {
                file.CopyTo(Path.Combine(target.FullName, file.Name), true);
            }

            // Do the same for all sub directories.
            foreach (DirectoryInfo directory in source.GetDirectories())
            {
                CopyAllFiles(directory, new DirectoryInfo(Path.Combine(target.FullName, directory.Name)));
            }
        }
    }
}