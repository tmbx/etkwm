using System;
using System.Text.RegularExpressions;

namespace kcslib
{
    /// <summary>
    /// Catch-all utility class.
    /// </summary>
    public static class KUtil
    {
        /// <summary>
        /// Return a formatted error message.
        /// </summary>
        public static String FormatErrorMsg(String msg)
        {
            return InternalFormatError(msg, true, true);
        }

        /// <summary>
        /// Return a formatted error message composed of two parts.
        /// </summary>
        public static String FormatErrorMsg(String first, String last)
        {
            return InternalFormatError(first, true, false) + ": " + InternalFormatError(last, false, true);
        }

        /// <summary>
        /// Return a formatted error message composed of two parts.
        /// </summary>
        public static String FormatErrorMsg(String first, Exception last)
        {
            return InternalFormatError(first, true, false) + ": " +
                   InternalFormatError(FormatErrorMsg(last), false, true);
        }

        /// <summary>
        /// Return a formatted error message based on the exception specified.
        /// </summary>
        public static String FormatErrorMsg(Exception ex)
        {
            if (ex.InnerException == null) return FormatErrorMsg(ex.Message);
            return FormatErrorMsg(ex.Message, ex.InnerException.Message);
        }

        /// <summary>
        /// Format the string specified with the specified leading character
        /// case and ending punctuation.
        /// </summary>
        private static String InternalFormatError(String s, bool upperFlag, bool puncFlag)
        {
            // Ensure we have a valid string.
            if (s.Length == 0) s = "unknown error";

            // Split 's' in parts and analyze.
            String first = s[0].ToString(), mid = "", last = "";
            if (s.Length > 2) mid = s.Substring(1, s.Length - 2);
            if (s.Length > 1) last = s[s.Length - 1].ToString();
            bool lastPuncFlag = (last == "." || last == "!" || last == "?");

            // Format.
            String res = upperFlag ? first.ToUpper() : first.ToLower();
            res += mid;
            if (puncFlag && !lastPuncFlag) last += ".";
            else if (!puncFlag && lastPuncFlag) last = "";
            res += last;

            return res;
        }

        /// <summary>
        /// Return true if the string passed in parameter is a valid
        /// email address. If it is null or empty, or an invalid email
        /// address, false is returned. 
        /// </summary>
        public static bool IsEmail(string inputEmail)
        {
            if (String.IsNullOrEmpty(inputEmail)) return false;

            string strRegex = @"[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?";

            Regex re = new Regex(strRegex, RegexOptions.IgnoreCase);
            return re.IsMatch(inputEmail);
        }

        public static bool IsNumeric(String val)
        {
            return IsNumeric(val, System.Globalization.NumberStyles.Integer);
        }

        public static bool IsNumeric(String val, System.Globalization.NumberStyles NumberStyle)
        {
            Double result;
            return Double.TryParse(val, NumberStyle, System.Globalization.CultureInfo.CurrentCulture, out result);
        }

        /// <summary>
        /// Truncates the given string to a maximum of 'size' characters,
        /// including 3 trailing dots.
        /// </summary>
        public static String TroncateString(String str, int size)
        {
            if (str.Length <= size) return str;
            return str.Substring(0, size - 3) + "...";
        }

        /// <summary>
        /// Return the string representation of the byte array specified.
        /// </summary>
        public static String HexStr(byte[] p)
        {
            if (p == null) return "";
            char[] c = new char[p.Length * 3 + 2];
            byte b;

            c[0] = '0'; c[1] = 'x';

            for (int y = 0, x = 2; y < p.Length; ++y, ++x)
            {
                b = ((byte)(p[y] >> 4));
                c[x] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                b = ((byte)(p[y] & 0xF));
                c[++x] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                c[++x] = ' ';
            }

            return new String(c);
        }

        /// <summary>
        /// Return true if the byte arrays specified are equal.
        /// </summary>
        public static bool ByteArrayEqual(byte[] a1, byte[] a2)
        {
            if (a1 == null && a2 == null) return true;
            if (a1 == null || a2 == null) return false;
            if (a1.Length != a2.Length) return false;

            for (int i = 0; i < a1.Length; i++)
            {
                if (a1[i] != a2[i]) return false;
            }

            return true;
        }
    }
}