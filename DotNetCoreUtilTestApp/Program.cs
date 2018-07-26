using System;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using IPA.DN.CoreUtil;
using IPA.DN.CoreUtil.BigInt;

using static System.Console;

namespace DotNetCoreUtilTestApp
{
    static class SClass1
    {
        static public List<string> StrList = new List<string>();

        public static C1 c1;
    }

    class C1
    {
        public C2 c2;
        public string s = "C1";
    }

    class C2
    {
        public C1 c1;
        public string s = "C2";
    }

    class Program
    {
        static void Main(string[] args)
        {
            WriteLine(Kernel.InternalCheckIsWow64());
            WriteLine(Kernel.GetOsPlatform().ToString());
            WriteLine(System.Environment.OSVersion.VersionString);
            WriteLine("home: " + Env.HomeDir);
            WriteLine("path char: " + System.IO.Path.DirectorySeparatorChar);

            Console.WriteLine(Debug.GetVarsFromClass(typeof(Env)));

            SClass1.StrList.Add("a");
            SClass1.StrList.Add("b");
            SClass1.StrList.Add("c");

            C1 c1 = new C1();
            C2 c2 = new C2();

            c1.c2 = c2;
            c2.c1 = c1;
            SClass1.c1 = c1;

            Console.WriteLine(Debug.GetVarsFromClass(typeof(SClass1)));



            Util.DoNothing();
        }
    }
}
