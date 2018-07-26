using System;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

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

            mutex_test();
            Console.WriteLine();
        }

        static void mutex_test()
        {
            bool f = false;
            Mutex m = new Mutex(false, "180726", out f);
            Con.WriteLine($"mutex new = {f}");
            Con.WriteLine("Wait for acquire mutex...");
            m.WaitOne();
            Con.WriteLine("Wait finished.");
            try
            {
                Con.WriteLine("Sleeping...");
                ThreadObj.Sleep(8000);
            }
            finally
            {
                m.ReleaseMutex();
                Con.WriteLine("Released.");
            }
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
