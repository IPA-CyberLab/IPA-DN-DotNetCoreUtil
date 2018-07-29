using System;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using System.Security.Cryptography;

using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using System.Text;
using System.IO;

using System.Net;
using System.Net.Sockets;

using IPA.DN.CoreUtil;
using IPA.DN.CoreUtil.BigInt;

using Org.BouncyCastle;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

using static System.Console;
using IPA.DN.CoreUtil.Helper.Basic;

namespace DotNetCoreUtilTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Dbg.SetDebugMode();

            CancellationTokenSource s = new CancellationTokenSource();

            var r = IO.EnumDirsWithCancel(new string[] { @"C:\tmp\SysInst", @"C:\tmp\xp180721" }, "", s.Token);
            int num = 0;
            foreach (DirEntry e in r)
            {
                //e.InnerPrint(num++.ToString());
                e.FullPath.Print();
            }
        }

        static void process_test()
        {
            ChildProcess p = new ChildProcess(" / bin/bash", "", "#!/bin/bash\r\necho aaa > aaa.txt\r\necho bbb\ndate\n\r\n\r\n".NormalizeCrlfThisPlatform(), true, 1000);

                         //ChildProcess p = new ChildProcess(@"C:\git\dn-rlogin\rlogin_src\openssl-1.1.0h-x32\apps\openssl.exe", "", "version\n\n", true, 1000);

            WriteLine(p.StdOut);
            WriteLine(p.StdErr);

            p.InnerPrint();
        }

        static void time_test()
        {
            while (true)
            {
                WriteLine(Time.NowLong100Usecs);
                ThreadObj.Sleep(5);
            }
        }

        static void fullroute_test()
        {
            FullRouteSetThread t = new FullRouteSetThread(true);

            t.WaitForReady(ThreadObj.Infinite);

            while (true)
            {
                string ip = Con.ReadLine("IP>");

                if (Str.StrCmpi(ip, "exit"))
                {
                    break;
                }

                FullRouteSetResult ret = t.FullRouteSet.Lookup(ip);

                if (ret == null)
                {
                    Con.WriteLine("Not found.");
                }
                else
                {
                    Con.WriteLine("IP: {0}\nIPNet: {1}/{2}\nAS: {3} ({4})\nAS_PATH: {5}\nCountry: {6} ({7})",
                        ret.IPAddress, ret.IPRouteNetwork,
                        ret.IPRouteSubnetLength, ret.ASNumber, ret.ASName,
                        ret.AS_PathString, ret.CountryCode2, ret.CountryName);
                }
            }

            t.Stop();

            return;
        }

        static void rsa_test()
        {
            string hello = "Hello World";
            byte[] hello_data = hello.GetBytes();
            WriteLine("src: " + hello_data.GetHexString());

            Rsa rsa_private = new Rsa("@test1024.key");
            byte[] signed = rsa_private.SignData(hello_data);
            WriteLine("signed: " + signed.GetHexString());

            Cert cert = new Cert("@test1024.cer");
            Rsa rsa_public = new Rsa(cert);
            WriteLine("verify: " + rsa_public.VerifyData(hello_data, signed));

            byte[] encryped = rsa_public.Encrypt(hello_data);
            //encryped = "1C813B8396104AB1436C9AE208D5FC1A12CA15955A773F49F246F80FEDF13F914DF792A991B245601E13CFEE7B53B9117B35E54ACE465140D853F1901A0E8E33D603B65C6ECF0E6AB390AF7CB404D325EAF1669BD5C4F68FBE52888F44FE0CD596EF7BEEB44133A77D847FF177545D8678D6D0EFC6E4F1DB86CC48FE263C481E".GetHexBytes();
            WriteLine("encrypted: " + encryped.GetHexString());

            byte[] decrypted = rsa_private.Decrypt(encryped);
            WriteLine("decrypted: " + decrypted.GetHexString());

            WriteLine("cert_hash: " + cert.Hash.GetHexString());
        }


        static void rsa_test2()
        {
            string hello = "Hello World";
            byte[] hello_data = hello.GetBytes();
            WriteLine(hello_data.GetHexString());

            PemReader private_pem = new PemReader(new StringReader(Str.ReadTextFile("@testcert.key")));
            AsymmetricKeyParameter private_key = (AsymmetricKeyParameter)private_pem.ReadObject();

            PemReader cert_pem = new PemReader(new StringReader(Str.ReadTextFile("@testcert.cer")));
            X509Certificate cert = (X509Certificate)cert_pem.ReadObject();
            AsymmetricKeyParameter public_key = cert.GetPublicKey();

            IAsymmetricBlockCipher cipher = new Pkcs1Encoding(new RsaEngine());
            cipher.Init(true, public_key);

            byte[] encryped = cipher.ProcessBlock(hello_data, 0, hello_data.Length);

            WriteLine(encryped.GetHexString());

            cipher = new Pkcs1Encoding(new RsaEngine());
            cipher.Init(false, private_key);

            byte[] decryped = cipher.ProcessBlock(encryped, 0, encryped.Length);
            WriteLine(decryped.GetHexString());

            ISigner signer = SignerUtilities.GetSigner("SHA1withRSA");
            signer.Init(true, private_key);
            byte[] signed = signer.GenerateSignature();
            WriteLine(signed.GetHexString());

            signer = SignerUtilities.GetSigner("SHA1withRSA");
            signer.Init(false, public_key);
            WriteLine(signer.VerifySignature(signed));
        }

        static List<Sock> sock_test3_socket_list = new List<Sock>();
        static SockEvent sock_test3_event = new SockEvent();

        static void sock_test3_loop_thread(object param)
        {
            while (true)
            {
                sock_test3_event.Wait(1000);

                Sock[] socks = null;
                lock (sock_test3_socket_list)
                {
                    socks = sock_test3_socket_list.ToArray();
                }

                foreach (Sock s in socks)
                {
                    byte[] data = s.Recv(65536);
                    if (data == null)
                    {
                        WriteLine($"Client {s.RemoteIP}:{s.RemotePort} disconnected.");
                        lock (sock_test3_socket_list)
                        {
                            sock_test3_socket_list.Remove(s);
                        }
                        s.Disconnect();
                    }
                    else if (data.Length == 0)
                    {
                        // later
                    }
                    else
                    {
                        WriteLine($"Client {s.RemoteIP}:{s.RemotePort}: recv {data.Length} bytes.");
                    }
                }
            }
        }

        static void sock_test3()
        {
            int port = 80;

            ThreadObj loop_thread = new ThreadObj(sock_test3_loop_thread);

            Sock a = Sock.Listen(port);

            Console.WriteLine($"Listening {a.LocalPort} ...");

            while (true)
            {
                Sock s = a.Accept();

                WriteLine($"Connected from {s.LocalIP}");

                sock_test3_event.JoinSock(s);

                lock (sock_test3_socket_list)
                {
                    sock_test3_socket_list.Add(s);
                }

                sock_test3_event.Set();
            }
        }

        static void sock_test4_accept_proc(Listener listener, Sock sock, object param)
        {
            Sock s = sock;
            WriteLine($"Connected from {s.LocalIP}");

            s.SetTimeout(3000);

            s.SendAll("Hello\n".GetBytes_Ascii());

            byte[] recv = s.Recv(4096);
            if (recv == null)
            {
                Console.WriteLine("Disconnected.");
            }
            else
            {
                Console.WriteLine($"recv size = {recv.Length}");
            }

            s.Disconnect();
        }

        static void sock_test4()
        {
            Listener x = new Listener(80, sock_test4_accept_proc, null);
            WriteLine($"Listening {x.Port}");

            ReadLine();

            WriteLine("Stop listening...");
            x.Stop();
            WriteLine("Stopped.");
            ReadLine();
        }

        static void sock_test2()
        {
            string hostname = "www.tsukuba.ac.jp";
            WriteLine("Connecting...");
            IPAddress ip = Domain.GetIP(hostname)[0];
            IPEndPoint endPoint = new IPEndPoint(ip, 80);
            Socket s = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            s.LingerState = new LingerOption(false, 0);
            s.Connect(endPoint);
            string send_str = $"GET / HTTP/1.1\r\nHOST: {hostname}\r\n\r\n";

            s.Send(send_str.GetBytes_UTF8());
            WriteLine("Sent.");

            byte[] tmp = new byte[65536];

            int ret = s.Receive(tmp);
            Con.WriteLine($"ret = {ret}");
            s.Disconnect(false);
        }

        static void sock_test()
        {
            //Con.WriteLine("Enter key!");
            //ReadLine();
            string hostname = "www.softether.com";
            WriteLine("Connecting...");
            Sock s = Sock.Connect(hostname, 80);
            WriteLine("Connected.");

            string send_str = $"GET / HTTP/1.1\r\nHOST: {hostname}\r\n\r\n";

            if (s.SendAll(send_str.GetBytes()) == false)
            {
                throw new ApplicationException("Disconnected");
            }
            //s.Socket.Send(send_str.GetBytes());
            WriteLine("Sent.");

            byte[] recv_data = s.Recv(65536 * 100);
            if (recv_data == null)
            {
                throw new ApplicationException("Disconnected");
            }

            WriteLine($"recv_data.length = {recv_data.Length}");

            WriteLine(recv_data.GetString());
            /*
            byte[] tmp = new byte[65536];
            int ret = s.Socket.Receive(tmp);
            Con.WriteLine($"ret = {ret}");*/

            s.Disconnect();
        }

        static void dns_test()
        {
            IPAddress[] list = Domain.GetIP46("www.google.com");

            foreach (IPAddress a in list)
            {
                Con.WriteLine(a.ToString());
            }

            foreach (string hostname in Domain.GetHostName(Domain.StrToIP("130.158.6.51")))
            {
                Con.WriteLine(hostname);
            }
        }

        static void mail_test()
        {
            SendMail sm = new SendMail("10.32.0.14");
            sm.Send("Test <noreply@icscoe.jp>", "Ahosan <da.ahosan1@softether.co.jp>", "こんにちは2", "これはテストです2");
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
            Dbg.PrintObjectInnerString(typeof(Env));
            //Debug.PrintObjectInnerString(typeof(System.Runtime.InteropServices.RuntimeInformation));

            Util.DoNothing();
        }
    }
}
