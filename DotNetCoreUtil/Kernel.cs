// CoreUtil
// 
// Copyright (C) 1997-2010 Daiyuu Nobori. All Rights Reserved.
// Copyright (C) 2004-2010 SoftEther Corporation. All Rights Reserved.

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
using System.Security.Cryptography;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Net.Mail;
using System.Net.Mime;
using System.Runtime.InteropServices;

namespace IPA.DN.CoreUtil
{
    public static class Kernel
    {
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool wow64Process
        );

        public static bool InternalCheckIsWow64()
        {
            if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
                Environment.OSVersion.Version.Major >= 6)
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    bool retVal;
                    if (!IsWow64Process(p.Handle, out retVal))
                    {
                        return false;
                    }
                    return retVal;
                }
            }
            else
            {
                return false;
            }
        }

        // スリープ
        public static void SleepThread(int millisec)
        {
            ThreadObj.Sleep(millisec);
        }

        // 環境変数文字列の取得
        public static string GetEnvStr(string name)
        {
            string ret = Environment.GetEnvironmentVariable(name);

            if (ret == null)
            {
                ret = "";
            }

            return ret;
        }

        // 現在のプロセスを強制終了する
        static public void SelfKill()
        {
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }

        // プログラムを起動する
        public static Process Run(string exeName, string args)
        {
            Process p = new Process();
            p.StartInfo.FileName = IO.InnerFilePath(exeName);
            p.StartInfo.Arguments = args;

            p.Start();

            return p;
        }
    }
}
