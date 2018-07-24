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
            BigInteger a = new BigInteger(12345678);

            for (int i = 0;i < 6;i++)
            a = a * a;

            WriteLine(a.ToHexString());
        }
    }
}
