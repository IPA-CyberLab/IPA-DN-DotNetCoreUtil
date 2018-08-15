using System;
using System.Threading;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Web;
using System.IO;
using System.Drawing;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace IPA.DN.CoreUtil
{
    public static class Dbg
    {
        static bool is_debug_mode = false;
        public static bool IsDebugMode => is_debug_mode;

        public static void SetDebugMode(bool b = true)
        {
            is_debug_mode = b;
        }

        public static void WriteCurrentThreadId(string str = "")
        {
            if (Dbg.IsDebugMode == false) return;

            string a = $"Thread[{ThreadObj.CurrentThreadId}]";

            if (Str.IsFilledStr(str))
            {
                a += ": " + str;
            }

            WriteLine(a);
        }

        public static string WriteLine()
        {
            WriteLine("");
            return "";
        }
        public static string WriteLine(string str)
        {
            if (str == null) str = "null";
            if (Dbg.IsDebugMode)
            {
                Console.WriteLine(str);
            }
            return str;
        }
        public static void WriteLine(string str, params object[] args)
        {
            if (Dbg.IsDebugMode)
            {
                Console.WriteLine(str, args);
            }
        }

        public static string GetObjectInnerString(object obj, string instance_base_name = "")
        {
            return GetObjectInnerString(obj.GetType(), obj, instance_base_name);
        }
        public static string GetObjectInnerString(Type t)
        {
            return GetObjectInnerString(t, null, null);
        }
        public static string GetObjectInnerString(Type t, object obj, string instance_base_name)
        {
            DebugVars v = GetVarsFromClass(t, instance_base_name, obj);

            return v.ToString();
        }

        public static void WriteObject(object obj, string instance_base_name = "")
        {
            WriteObject(obj.GetType(), obj, instance_base_name);
        }
        public static void WriteObject(Type t)
        {
            WriteObject(t, null, null);
        }
        public static void WriteObject(Type t, object obj, string instance_base_name)
        {
            if (Dbg.IsDebugMode == false)
            {
                return;
            }

            DebugVars v = GetVarsFromClass(t, instance_base_name, obj);

            string str = v.ToString();

            Console.WriteLine(str);
        }

        public static void PrintObjectInnerString(object obj, string instance_base_name = "")
        {
            PrintObjectInnerString(obj.GetType(), obj, instance_base_name);
        }
        public static void PrintObjectInnerString(Type t)
        {
            PrintObjectInnerString(t, null, null);
        }
        public static void PrintObjectInnerString(Type t, object obj, string instance_base_name)
        {
            DebugVars v = GetVarsFromClass(t, instance_base_name, obj);

            string str = v.ToString();

            Console.WriteLine(str);
        }

        public static DebugVars GetVarsFromClass(Type t, string name = null, object obj = null, ImmutableHashSet<object> duplicate_check = null)
        {
            if (duplicate_check == null) duplicate_check = ImmutableHashSet<object>.Empty;

            if (Str.IsEmptyStr(name)) name = t.Name;

            DebugVars ret = new DebugVars();

            var members_list = GetAllMembersFromType(t, obj != null, obj == null);

            foreach (MemberInfo info in members_list)
            {
                bool ok = false;
                if (info.MemberType == MemberTypes.Field)
                {
                    FieldInfo fi = info as FieldInfo;

                    ok = true;

                    if (fi.IsInitOnly)
                    {
                        ok = false;
                    }
                }
                else if (info.MemberType == MemberTypes.Property)
                {
                    PropertyInfo pi = info as PropertyInfo;

                    ok = true;
                }

                if (ok)
                {
                    //if (info.Name == "lockFile") Debugger.Break();

                    object data = GetValueOfFieldOrProperty(info, obj);
                    Type data_type = data?.GetType() ?? null;

                    if (IsPrimitiveType(data_type))
                    {
                        ret.Vars.Add((info, data));
                    }
                    else
                    {
                        if (data == null)
                        {
                            ret.Vars.Add((info, null));
                        }
                        else
                        {
                            if (data is IEnumerable)
                            {
                                int n = 0;
                                foreach (object item in (IEnumerable)data)
                                {
                                    if (duplicate_check.Contains(item) == false)
                                    {
                                        Type data_type2 = item?.GetType() ?? null;

                                        if (IsPrimitiveType(data_type2))
                                        {
                                            ret.Vars.Add((info, item));
                                        }
                                        else if (item == null)
                                        {
                                            ret.Vars.Add((info, null));
                                        }
                                        else
                                        {
                                            ret.Childlen.Add(GetVarsFromClass(data_type2, info.Name, item, duplicate_check.Add(data)));
                                        }
                                    }

                                    n++;
                                }
                            }
                            else
                            {
                                if (duplicate_check.Contains(data) == false)
                                {
                                    ret.Childlen.Add(GetVarsFromClass(data_type, info.Name, data, duplicate_check.Add(data)));
                                }
                            }
                        }
                    }
                }
            }

            ret.BaseName = name;

            return ret;
        }

        public static MemberInfo[] GetAllMembersFromType(Type t, bool hide_static, bool hide_instance)
        {
            HashSet<MemberInfo> a = new HashSet<MemberInfo>();

            if (hide_static == false)
            {
                a.UnionWith(t.GetMembers(BindingFlags.Static | BindingFlags.Public));
                a.UnionWith(t.GetMembers(BindingFlags.Static | BindingFlags.NonPublic));
            }

            if (hide_instance == false)
            {
                a.UnionWith(t.GetMembers(BindingFlags.Instance | BindingFlags.Public));
                a.UnionWith(t.GetMembers(BindingFlags.Instance | BindingFlags.NonPublic));
            }

            MemberInfo[] ret = new MemberInfo[a.Count];
            a.CopyTo(ret);
            return ret;
        }

        public static object GetValueOfFieldOrProperty(MemberInfo m, object obj)
        {
            switch (m)
            {
                case PropertyInfo p:
                    try
                    {
                        return p.GetValue(obj);
                    }
                    catch
                    {
                        return null;
                    }

                case FieldInfo f:
                    return f.GetValue(obj);
            }

            return null;
        }

        public static bool IsPrimitiveType(Type t)
        {
            if (t == null) return true;
            if (t.IsSubclassOf(typeof(System.Type))) return true;
            if (t.IsSubclassOf(typeof(System.Delegate))) return true;
            if (t.IsEnum) return true;
            if (t.IsPrimitive) return true;
            if (t == typeof(System.Delegate)) return true;
            if (t == typeof(System.MulticastDelegate)) return true;
            if (t == typeof(string)) return true;
            if (t == typeof(DateTime)) return true;
            if (t == typeof(TimeSpan)) return true;
            if (t == typeof(IPAddr)) return true;
            if (t == typeof(IPAddress)) return true;
            if (t == typeof(System.Numerics.BigInteger)) return true;

            return false;
        }

        public static void Suspend() => Kernel.SuspendForDebug();

        public static void Break() => Debugger.Break();
    }

    public class DebugVars
    {
        public string BaseName = "";

        public List<ValueTuple<MemberInfo, object>> Vars = new List<(MemberInfo, object)>();
        public List<DebugVars> Childlen = new List<DebugVars>();

        public void WriteToString(StringWriter w, ImmutableList<string> parents)
        {
            this.Vars.Sort((a, b) => string.Compare(a.Item1.Name, b.Item1.Name));
            this.Childlen.Sort((a, b) => string.Compare(a.BaseName, b.BaseName));

            foreach (DebugVars var in Childlen)
            {
                var.WriteToString(w, parents.Add(var.BaseName));
            }

            foreach (var data in Vars)
            {
                MemberInfo p = data.Item1;
                object o = data.Item2;
                string print_str = "null";
                string closure = "'";
                if ((o?.GetType().IsPrimitive ?? true) || (o?.GetType().IsEnum ?? false)) closure = "";
                if (o != null)
                {
                    print_str = $"{closure}{o.ToString()}{closure}";
                }

                w.WriteLine($"{Str.CombineStringArray(ImmutableListToArray<string>(parents), ".")}.{p.Name} = {print_str}");
            }
        }

        public static T[] ImmutableListToArray<T>(ImmutableList<T> input)
        {
            T[] ret = new T[input.Count];
            input.CopyTo(ret);
            return ret;
        }

        public override string ToString()
        {
            ImmutableList<string> parents = ImmutableList.Create<string>(this.BaseName);
            StringWriter w = new StringWriter();

            WriteToString(w, parents);

            return w.ToString();
        }
    }
}

