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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


using static System.Console;
using IPA.DN.CoreUtil.Helper.Basic;

namespace DotNetCoreUtilTestApp
{
    [Serializable]
    class T1
    {
        public string s1 { get; set; }
        public int i1 { get; set; }
        public double d1 { get; set; }
        public List<string> strlist { get; set; }

        public  T1 child { get; set; }
    }

    class T2
    {
        public string s1;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Dbg.SetDebugMode();

            T1 t1 = new T1();
            t1.s1 = "こんにちは";
            t1.i1 = 123;
            t1.d1 = 3.1415;
            t1.child = (T1)t1.CloneObject();
            t1.strlist = new List<string>();
            t1.strlist.Add("ねこ");
            t1.strlist.Add("いぬ\nへび");
            t1.strlist.Add("さる");

            string yaml = t1.ObjectToYaml();

            yaml.Print();

            "-------------".Print();

            T1 t2 = Yaml.Deserialize<T1>(yaml);

            Yaml.Serialize(t2).Print();
        }

        class vcp_replace_str_list
        {
            public string __PROJ_GUID__;
            public string __APPNAME__;
            public StringWriter __INCLUDE_FILE_LIST__ = new StringWriter();
            public StringWriter __COMPILE_FILE_LIST__ = new StringWriter();
            public StringWriter __NONE_FILE_LIST__ = new StringWriter();

            public StringWriter __FILTER_LIST__ = new StringWriter();
            public StringWriter __INCLUDE_LIST__ = new StringWriter();
            public StringWriter __COMPILE_LIST__ = new StringWriter();
            public StringWriter __NONE_LIST__ = new StringWriter();
        }

        public static void vc_project_maker(string base_dir)
        {
            // scan files
            var files = IO.EnumDirWithCancel(base_dir);

            SortedSet<string> dir_list = new SortedSet<string>();

            List<DirEntry> include_list = new List<DirEntry>();
            List<DirEntry> compile_list = new List<DirEntry>();
            List<DirEntry> none_list = new List<DirEntry>();

            foreach (var file in files)
            {
                if (file.IsFolder == false)
                {
                    string relative_dir = file.RelativePath.GetDirectoryName();
                    if (relative_dir.IsFilled())
                    {
                        dir_list.Add(relative_dir);

                        if (file.FileName.IsExtensionMatch(".c .cpp .s .asm"))
                        {
                            compile_list.Add(file);
                        }
                        else if (file.FileName.IsExtensionMatch(".h"))
                        {
                            include_list.Add(file);
                        }
                        else
                        {
                            none_list.Add(file);
                        }
                    }
                }
            }

            vcp_replace_str_list r = new vcp_replace_str_list()
            {
                __PROJ_GUID__ = Str.NewGuid(true),
                __APPNAME__ = base_dir.RemoteLastEnMark().GetFileName(),
            };

            foreach (var e in include_list)
            {
                r.__INCLUDE_FILE_LIST__.WriteLine($"    <ClInclude Include=\"{e.RelativePath}\" />");

                r.__INCLUDE_LIST__.WriteLine($"    <ClInclude Include=\"{e.RelativePath}\">");
                r.__INCLUDE_LIST__.WriteLine($"      <Filter>{e.RelativePath.GetDirectoryName()}</Filter>");
                r.__INCLUDE_LIST__.WriteLine($"    </ClInclude>");
            }

            foreach (var e in compile_list)
            {
                r.__COMPILE_FILE_LIST__.WriteLine($"    <ClCompile Include=\"{e.RelativePath}\" />");

                r.__COMPILE_LIST__.WriteLine($"    <ClCompile Include=\"{e.RelativePath}\">");
                r.__COMPILE_LIST__.WriteLine($"      <Filter>{e.RelativePath.GetDirectoryName()}</Filter>");
                r.__COMPILE_LIST__.WriteLine($"    </ClCompile>");
            }

            foreach (var e in none_list)
            {
                r.__NONE_FILE_LIST__.WriteLine($"    <None Include=\"{e.RelativePath}\" />");

                r.__NONE_LIST__.WriteLine($"    <None Include=\"{e.RelativePath}\">");
                r.__NONE_LIST__.WriteLine($"      <Filter>{e.RelativePath.GetDirectoryName()}</Filter>");
                r.__NONE_LIST__.WriteLine($"    </None>");
            }

            foreach (var dir in dir_list)
            {
                r.__FILTER_LIST__.WriteLine($"    <Filter Include=\"{dir}\">");
                r.__FILTER_LIST__.WriteLine($"      <UniqueIdentifier>{Str.NewGuid(true)}</UniqueIdentifier>");
                r.__FILTER_LIST__.WriteLine("    </Filter>");
            }

            string vcxproj = AppRes.vcxproj.ReplaceStrWithReplaceClass(r);
            string filters = AppRes.vcxfilter.ReplaceStrWithReplaceClass(r);

            IO.WriteAllTextWithEncoding(base_dir.CombinePath($"{r.__APPNAME__}.vcxproj"), vcxproj, Str.Utf8Encoding, false);
            IO.WriteAllTextWithEncoding(base_dir.CombinePath($"{r.__APPNAME__}.vcxproj.filters"), filters, Str.Utf8Encoding, false);
        }

        static void linux_c_h_add_autoconf_test()
        {
            var files = IO.EnumDirWithCancel(@"C:\git\DN-LinuxKernel-Learn\linux-2.6.39", "*.c *.h");
            foreach (var file in files)
            {
                if (file.IsFolder == false)
                {
                    if (file.FileName.IsSamei("autoconf.h") == false)
                    {
                        Encoding enc;

                        string tag = "#include \"linux/generated/autoconf.h\"\n";

                        try
                        {
                            string txt = IO.ReadAllTextWithAutoGetEncoding(file.FullPath, out enc, out _);
                            if (txt.StartsWith(tag) == false)
                            {
                                txt = tag + txt;
                                txt.WriteTextFile(file.FullPath, enc);
                                //file.FullPath.Print();
                            }
                        }
                        catch (Exception ex)
                        {
                            ex.ToString().Print();
                        }
                    }
                }
            }
        }

        static void linux_kernel_conf_test()
        {
            var enable_config_list = IO.ReadAllTextWithAutoGetEncoding(@"c:\tmp\test.txt").GetLines().ToList(true, true, false);

            string current_config = IO.ReadAllTextWithAutoGetEncoding(@"c:\tmp\current_config.txt").NormalizeCrlfUnix();

            foreach (string s in enable_config_list)
            {
                string old_str1 = $"\n{s}=m\n";
                string old_str2 = $"\n# {s} is not set\n";
                string new_str = $"\n{s}=y\n";

                current_config = current_config.ReplaceStr(old_str1, new_str, true);
                current_config = current_config.ReplaceStr(old_str2, new_str, true);
            }

            current_config.WriteTextFile(@"c:\tmp\new_config.txt");
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
