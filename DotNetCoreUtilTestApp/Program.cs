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
    class Program
    {
        static void Main(string[] args)
        {
            WriteLine(Kernel.InternalCheckIsWow64());
            WriteLine(Kernel.GetOsPlatform().ToString());
            WriteLine(System.Environment.OSVersion.VersionString);
            WriteLine("home: " + Env.HomeDir);
            WriteLine("path char: " + System.IO.Path.DirectorySeparatorChar);

            //Console.WriteLine(Debug.GetVarsFromClass(typeof(Env)));
            Debug.PrintObjectInnerString(typeof(Env));

            DirEntry[] e = IO.EnumDir(@"/root/");
            Debug.PrintObjectInnerString(e);

            Util.DoNothing();
        }
    }
}
