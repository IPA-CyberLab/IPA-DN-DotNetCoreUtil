using System;
using System.Collections.Generic;
using System.Text;
using IPA.DN.CoreUtil;

namespace IPA.DN.CoreUtil.Helper.StrEncoding
{
    public static class StrEncodingClass
    {
        public static byte[] GetBytes_UTF8(this string s, bool bom = false) => Util.CombineByteArray(bom ? Str.GetBOM(Str.Utf8Encoding) : null, Str.Utf8Encoding.GetBytes(s));
        public static byte[] GetBytes_UTF16LE(this string s, bool bom = false) => Util.CombineByteArray(bom ? Str.GetBOM(Str.Utf8Encoding) : null, Str.UniEncoding.GetBytes(s));
        public static byte[] GetBytes_ShiftJis(this string s) => Str.ShiftJisEncoding.GetBytes(s);
        public static byte[] GetBytes_Ascii(this string s) => Str.AsciiEncoding.GetBytes(s);
        public static byte[] GetBytes_Euc(this string s) => Str.EucJpEncoding.GetBytes(s);
        public static byte[] GetBytes(this string s, bool bom = false) => Util.CombineByteArray(bom ? Str.GetBOM(Str.Utf8Encoding) : null, Str.Utf8Encoding.GetBytes(s));
        public static byte[] GetBytes(this string s, Encoding enc) => enc.GetBytes(s);

        public static string GetString_UTF8(this byte[] b) => Str.DecodeString(b, Str.Utf8Encoding, out _);
        public static string GetString_UTF16LE(this byte[] b) => Str.DecodeString(b, Str.UniEncoding, out _);
        public static string GetString_ShiftJis(this byte[] b) => Str.DecodeString(b, Str.ShiftJisEncoding, out _);
        public static string GetString_Ascii(this byte[] b) => Str.DecodeString(b, Str.AsciiEncoding, out _);
        public static string GetString_Euc(this byte[] b) => Str.DecodeString(b, Str.EucJpEncoding, out _);
        public static string GetString(this byte[] b, Encoding default_encoding) => Str.DecodeString(b, default_encoding, out _);
        public static string GetString(this byte[] b) => Str.DecodeStringAutoDetect(b, out _);

        public static string GetHexString(this byte[] b, string padding = "") => Str.ByteToHex(b, padding);
        public static byte[] GetHexBytes(this string s) => Str.HexToByte(s);
    }
}

