﻿using System;

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
            httpclient_test();
        }

        static void httpclient_test()
        {
            HttpClient c = new HttpClient();
            Buf b = c.Get(new Uri("https://www.vpngate.net/ja/"));
            WriteLine(Str.Utf8Encoding.GetString(b.ByteData));
        }

        static void ipinfo_test()
        {
            while (true)
            {
                Console.WriteLine();
                Console.Write("IP>");
                string line = Console.ReadLine();
                if (Str.IsEmptyStr(line) == false)
                {
                    if (Str.StrCmp(line, "exit"))
                    {
                        break;
                    }

                    try
                    {
                        IPInfoEntry e = IPInfo.Search(line);

                        if (e == null)
                        {
                            Console.WriteLine("not found.");
                        }
                        else
                        {
                            Console.WriteLine(e.Country2);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            }
        }

        static void mutex_test3_thread(object param)
        {
            GlobalLock g = new GlobalLock("test");

            Con.WriteLine($"thread #{ThreadObj.CurrentThreadId}: before lock");
            using (g.Lock())
            {
                Con.WriteLine($"thread #{ThreadObj.CurrentThreadId}:locked");
                Con.WriteLine($"thread #{ThreadObj.CurrentThreadId}:sleeping.");
                Thread.Sleep(1000);
                Con.WriteLine($"thread #{ThreadObj.CurrentThreadId}:before release");
            }
            Con.WriteLine($"thread #{ThreadObj.CurrentThreadId}:released");
        }

        static void mutex_test3()
        {
            List<ThreadObj> tl = new List<ThreadObj>();
            for (int i = 0; i < 5; i++)
            {
                ThreadObj t = new ThreadObj(mutex_test3_thread);

                tl.Add(t);
            }
            foreach (ThreadObj t in tl) t.WaitForEnd();
        }

        static void mutex_test2()
        {
            GlobalLock g = new GlobalLock("test");

            Con.WriteLine("before lock");
            using (g.Lock())
            {
                Con.WriteLine("locked");
                Con.WriteLine("sleeping.");
                Thread.Sleep(5000);
                Con.WriteLine("before release");
            }
            Con.WriteLine("released");
        }

        static void mutex_test()
        {
            Mutant m = Mutant.Create("test1");
            Con.WriteLine("before acquire");
            m.Lock();
            Con.WriteLine("acquired.");
            Con.WriteLine("sleeping.");
            Thread.Sleep(5000);
            Con.WriteLine("before release");
            m.Unlock();
            Con.WriteLine("released");
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
