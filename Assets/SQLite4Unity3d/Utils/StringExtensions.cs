using System;
using System.Text;

namespace SQLite4Unity3d.Utils
{
    public static class StringExtensions
    {
        public static byte[] ToNullTerminatedUTF8(this string str)
        {
            var bytesCount = Encoding.UTF8.GetByteCount(str);
            var bytes = new byte[bytesCount + 1];
            Encoding.UTF8.GetBytes(str, 0, str.Length, bytes, 0);
            return bytes;
        }

        public static string Format(this string str, params object[] args)
        {
            return string.Format(str, args);
        }

        public static int CompareTo(this string original, string comparingTo, StringComparison stringComparison)
        {
            return string.Compare(original, comparingTo, stringComparison);
        }
    }
}