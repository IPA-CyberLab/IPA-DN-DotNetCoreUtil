using System;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using System.Text;
using System.IO;

using IPA.DN.CoreUtil;
using IPA.DN.CoreUtil.BigInt;

using static System.Console;

namespace DotNetCoreUtilTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            basic_test();
            Console.WriteLine();

            io_test();
        }

        static void io_test()
        {
            IO f = IO.FileCreate("@test.txt");
            f.Write(Str.ShiftJisEncoding.GetBytes("This is a test. こんにちは。\n"));
            f.Close();

            string tmp = IO.GetRelativeFileName("/tmp/abc/def.txt", "/tmp/");
            Console.WriteLine(tmp);
        }

        static void basic_test()
        {
            WriteLine(Kernel.InternalCheckIsWow64());
            WriteLine(Kernel.GetOsPlatform().ToString());
            WriteLine(System.Environment.OSVersion.VersionString);
            WriteLine("home: " + Env.HomeDir);
            WriteLine("path char: " + System.IO.Path.DirectorySeparatorChar);

            //Console.WriteLine(Debug.GetVarsFromClass(typeof(Env)));
            Debug.PrintObjectInnerString(typeof(Env));
            //Debug.PrintObjectInnerString(typeof(System.Runtime.InteropServices.RuntimeInformation));

            Util.DoNothing();
        }
    }
}
