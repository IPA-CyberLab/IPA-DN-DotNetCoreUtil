using System;
using System.Collections.Generic;
using System.Text;
using IPA.DN.CoreUtil;

namespace IPA.DN.CoreUtil.Helper.String
{
    public static class HelperString
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

        public static bool IsEmpty(this string s) => Str.IsEmptyStr(s);
        public static bool IsSolid(this string s) => !Str.IsEmptyStr(s);
        public static bool ToBool(this string s) => Str.StrToBool(s);
        public static byte[] ToByte(this string s) => Str.StrToByte(s);
        public static DateTime ToDate(this string s, bool to_utc = false) => Str.StrToDate(s, to_utc);
        public static DateTime Time(this string s, bool to_utc = false) => Str.StrToTime(s, to_utc);
        public static DateTime ToDateTime(this string s, bool to_utc = false) => Str.StrToDateTime(s, to_utc);
        public static object ToEnum(this string s, object default_value) => Str.StrToEnum(s, default_value);
        public static int ToInt(this string s) => Str.StrToInt(s);
        public static long ToLong(this string s) => Str.StrToLong(s);
        public static uint ToUInt(this string s) => Str.StrToUInt(s);
        public static ulong ToULong(this string s) => Str.StrToULong(s);
        public static double ToDouble(this string s) => Str.StrToDouble(s);
        public static decimal ToDecimal(this string s) => Str.StrToDecimal(s);
        public static bool IsSame(this string s, string t, bool ignore_case = false) => ((s == null && t == null) ? true : ((s == null || t == null) ? false : (ignore_case ? Str.StrCmpi(s, t) : Str.StrCmp(s, t))));
        public static bool IsSamei(this string s, string t) => IsSame(s, t, true);
        public static int Cmp(this string s, string t, bool ignore_case = false) => ((s == null && t == null) ? 0 : ((s == null ? 1 : t == null ? -1 : (ignore_case ? Str.StrCmpiRetInt(s, t) : Str.StrCmpRetInt(s, t)))));
        public static int Cmpi(this string s, string t, bool ignore_case = false) => Cmp(s, t, true);
        public static string[] GetLines(this string s) => Str.GetLines(s);
        public static bool GetKeyAndValue(this string s, out string key, out string value, string split_str = "") => Str.GetKeyAndValue(s, out key, out value, split_str);
        public static bool IsDouble(this string s) => Str.IsDouble(s);
        public static bool IsLong(this string s) => Str.IsLong(s);
        public static bool IsInt(this string s) => Str.IsInt(s);
        public static bool IsNumber(this string s) => Str.IsNumber(s);
        public static bool InStr(this string s, string keyword, bool ignore_case = false) => Str.InStr(s, keyword, !ignore_case);
        public static string NormalizeCrlfWindows(this string s) => Str.NormalizeCrlfWindows(s);
        public static string NormalizeCrlfUnix(this string s) => Str.NormalizeCrlfUnix(s);
        public static string NormalizeCrlfThisPlatform(this string s) => Str.NormalizeCrlfThisPlatform(s);
        public static string[] ParseCmdLine(this string s) => Str.ParseCmdLine(s);
        public static object XmlToObjectPublic(this string s, Type t) => Str.XMLToObjectSimple(s, t);
        public static bool IsXmlStrForObjectPubllic(this string s) => Str.IsStrOkForXML(s);
        public static StrToken ToToken(this string s, string split_str = " ,\t\r\n") => new StrToken(s, split_str);
        public static string OneLine(this string s) => Str.OneLine(s);
        public static string FormatC(this string s) => Str.FormatC(s);
        public static string FormatC(this string s, params object[] args) => Str.FormatC(s, args);
        public static void Printf(this string s) => Str.Printf(s, new object[0]);
        public static void Printf(this string s, params object[] args) => Str.Printf(s, args);
        public static void Print(this string s, bool newline = true) => Console.Write(s + (newline ? Env.NewLine : ""));
        public static void Debug(this string s) => Dbg.WriteLine(s);
        public static int Search(this string s, string keyword, int start = 0, bool case_senstive = false) => Str.SearchStr(s, keyword, start, case_senstive);

        public static string LinesToStr(this string[] lines) => Str.LinesToStr(lines);
        public static string[] UniqueToken(this string[] t) => Str.UniqueToken(t);

        public static string MakeCharArray(this char c, int len) => Str.MakeCharArray(c, len);

        public static byte[] NormalizeCrlfWindows(this byte[] s) => Str.NormalizeCrlfWindows(s);
        public static byte[] NormalizeCrlfUnix(this byte[] s) => Str.NormalizeCrlfUnix(s);
        public static byte[] NormalizeCrlfThisPlatform(this byte[] s) => Str.NormalizeCrlfThisPlatform(s);

        public static void InnerDebug(this object o, string instance_base_name = "") => Dbg.WriteObject(o, instance_base_name);
        public static void InnerPrint(this object o, string instance_base_name = "") => Dbg.PrintObjectInnerString(o, instance_base_name);
        public static string GetInnerStr(this object o, string instance_base_name = "") => Dbg.GetObjectInnerString(o, instance_base_name);
        public static string ObjectToXmlPublic(this object o, Type t = null) => Str.ObjectToXMLSimple(o, t ?? o.GetType());

        public static string ToStr3(this long s) => Str.ToStr3(s);
        public static string ToStr3(this int s) => Str.ToStr3(s);
        public static string ToStr3(this ulong s) => Str.ToStr3(s);
        public static string ToStr3(this uint s) => Str.ToStr3(s);

        public static string ToDtStr(this DateTime dt, bool with_msecs = false, DtstrOption option = DtstrOption.All, bool with_nanosecs = false) => Str.DateTimeToDtstr(dt, with_msecs, option, with_nanosecs);
    }
}

