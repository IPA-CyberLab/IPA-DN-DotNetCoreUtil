﻿using System;

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
using IPA.DN.CoreUtil.Helper.StrEncoding;

namespace DotNetCoreUtilTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Dbg.SetDebugMode();

            rsa_test();
        }

        static void rsa_test()
        {
            string hello = "Hello World";
            byte[] hello_data = hello.GetBytes();
            WriteLine("src: " + hello_data.GetHexString());

            Rsa rsa_private = new Rsa("@testcert.key");
            byte[] signed = rsa_private.SignData(hello_data);
            WriteLine("signed: " + signed.GetHexString());

            Cert cert = new Cert("@testcert.cer");
            Rsa rsa_public = new Rsa(cert);
            WriteLine("verify: " + rsa_public.VerifyData(hello_data, signed));

            byte[] encryped = rsa_public.Encrypt(hello_data);
            WriteLine("encrypted: " + signed.GetHexString());

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
