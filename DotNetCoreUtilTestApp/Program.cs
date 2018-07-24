using System;
using IPA.DN.CoreUtil;
using IPA.DN.CoreUtil.BigInt;

using static System.Console;

namespace DotNetCoreUtilTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Kernel.SelfKill();
            WriteLine(Kernel.InternalCheckIsWow64());
            WriteLine(Kernel.GetOsPlatform().ToString());
            WriteLine(System.Environment.OSVersion.VersionString);
        }
    }
}
