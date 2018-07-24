using System;
using System.Threading;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
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
    public static class Debug
    {
        public static DebugVars GetVarsFromClass(Type t, object obj = null)
        {
            DebugVars ret = new DebugVars();

            MemberInfo[] proplist = t.GetProperties();

            foreach (PropertyInfo p in proplist)
            {
                if (IsPrimitiveType(p.PropertyType))
                {
                    ret.Vars.Add(p.Name, (p, p.GetValue(obj)));
                }
                else
                {
                    if (p.GetValue(obj) == null)
                    {
                        ret.Vars.Add(p.Name, (p, null));
                    }
                    else
                    {
                        ret.Childlen.Add(p.Name, GetVarsFromClass(p.GetValue(obj).GetType(), p.GetValue(obj)));
                    }
                }
            }

            MemberInfo[] fieldlist = t.GetFields();

            foreach (FieldInfo f in fieldlist)
            {
                if (IsPrimitiveType(f.FieldType))
                {
                    if (IsPrimitiveType(f.FieldType))
                    {
                        ret.Vars.Add(f.Name, (f, f.GetValue(obj)));
                    }
                    else
                    {
                        if (f.GetValue(obj) == null)
                        {
                            ret.Vars.Add(f.Name, (f, null));
                        }
                        else
                        {
                            ret.Childlen.Add(f.Name, GetVarsFromClass(f.GetValue(obj).GetType(), f.GetValue(obj)));
                        }
                    }
                }
            }

            return ret;
        }

        public static bool IsPrimitiveType(Type t)
        {
            if (t.IsPrimitive) return true;
            if (t == typeof(string)) return true;
            if (t == typeof(DateTime)) return true;
            if (t == typeof(TimeSpan)) return true;
            if (t == typeof(IPAddr)) return true;
            if (t == typeof(IPAddress)) return true;

            return false;
        }
    }

    public class DebugVars
    {
        public string BaseName = "";

        public SortedList<string, ValueTuple<MemberInfo, object>> Vars = new SortedList<string, (MemberInfo, object)>();
        public SortedList<string, DebugVars> Childlen = new SortedList<string, DebugVars>();

        public void WriteToString(StringWriter w, int depth, ImmutableList<string> parents)
        {
            string padding = Str.MakeCharArray(' ', depth);

            foreach (string name in this.Vars.Keys)
            {
                var data = this.Vars[name];
                MemberInfo p = data.Item1;
                object o = data.Item2;
                string print_str = "null";
                if (o != null) print_str = $"'{o.ToString()}'";

                w.WriteLine($"{padding}{Str.CombineStringArray(ImmutableListToArray<string>(parents), ".")}.{name} = {print_str}");
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

            WriteToString(w, 0, parents);

            return w.ToString();
        }
    }
}

