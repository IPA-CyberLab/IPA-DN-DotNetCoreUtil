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
using System.Runtime.InteropServices;

using System.Text;
using System.IO;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

using System.Net;
using System.Net.Sockets;

using System.Web;

using IPA.DN.CoreUtil.Basic;
using IPA.DN.CoreUtil.Basic.BigInt;
using IPA.DN.CoreUtil.WebApi;

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
using YamlDotNet.Serialization;


using static System.Console;

using IPA.DN.CoreUtil.Helper.Basic;
using IPA.DN.CoreUtil.Helper.SlackApi;

using YamlDotNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Routing;

#pragma warning disable 162

namespace DotNetCoreUtilTestApp
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        // GET api/values
        [HttpGet]
        public ActionResult<IEnumerable<string>> Get()
        {
            return new string[] { "value1", "value2 " + DateTime.Now.ToString() };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public ActionResult<string> Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }

    [Serializable]
    public class DBTestSettings
    {
        public string DBConnectStr { get; set; }
    }

    public struct STTEST
    {
        public string A;
    }


    class Program
    {
        [DllImport("MyLib.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern long NativeMethod();

        static void Main(string[] args)
        {
            "Test Program".Print();

            Dbg.SetDebugMode();

            //twitter_test();

            //slack_test();

            //async_test();

            //db_test();

            //DbTest.db_test();

            //json_test();

            //jsonrpc_test_with_random_api();

            //jsonrpc_http_server_test();

            //Benchmark b = new Benchmark();

            // sleep_test();

            sleep_task_gc_test();

            //sleep_task_test2();

            //event_test();

            //event_gc_test();
        }

        static void event_gc_test()
        {
            Benchmark b = new Benchmark("event_gc_test");

            List<AsyncEvent> events2 = new List<AsyncEvent>();

            while (true)
            {
                //b.IncrementMe++;

                int num = 10000;
                List<AsyncEvent> events = new List<AsyncEvent>();
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < num; i++)
                {
                    AsyncEvent ae = new AsyncEvent(false);
                    Task t = ae.WaitAsync();

                    events.Add(ae);
                    events2.Add(ae);

                    tasks.Add(t);
                }

                foreach (AsyncEvent e in events)
                {
                    e.Set();
                }

                foreach (Task t in tasks)
                {
                    t.Wait();
                }

                events = null;
                tasks = null;

                int num_a = 0, num_total = 0;
                foreach (AsyncEvent ae in events2)
                {
                    if (ae.IsAbandoned)
                    {
                        num_a++;
                    }
                    num_total++;
                }
                Con.WriteLine($"{num_a} {num_total}");
            }
        }

        static void event_test()
        {
            AsyncEvent ae = new AsyncEvent(false);

            ThreadObj t = new ThreadObj(param =>
            {
                while (true)
                {
                    //Kernel.SleepThread(1000);
                    ae.Set();
                }
            });

            IntervalDebug id = new IntervalDebug();
            while (true)
            {
                id.Start();
                ae.WaitAsync().Wait();
                id.PrintElapsed();
                ae.Reset();

                GC.Collect();
            }
        }

        static void sleep_task_test2()
        {
            Task t = TaskUtil.Sleep(1000);

            Dbg.Where();
            t.Wait();
            Dbg.Where();

            Kernel.SleepThread(-1);
        }

        static void sleep_task_gc_test()
        {
            Benchmark b = new Benchmark("num_newtask");
            while (true)
            {
                List<Task> o = new List<Task>();
                for (int i = 0; i < 100000; i++)
                {
                    b.IncrementMe++;
                    Task t = TaskUtil.Sleep(1000);
                    o.Add(t);
                }
                //t.Wait();
                //Dbg.Where();
                //Task.Delay(1000);
                //Util.AddToBlackhole(t);
                Dbg.Where();
                foreach (Task t in o)
                {
                    t.Wait();
                }
                Dbg.Where();
            }
            while (true)
            {
                b.IncrementMe++;
                Task t = TaskUtil.Sleep(1000);
                //t.Wait();
                //Dbg.Where();
                //Task.Delay(1000);
                Util.AddToBlackhole(t);
            }
        }

        static void sleep_test()
        {
            Dbg.SetDebugMode(false);

            Ref<int> interval = new Ref<int>(100);

            new ThreadObj(param =>
            {
                while (true)
                {
                    long tick = Time.Tick64;

                    Task.Delay(interval.Value).Wait();
                    //Thread.Sleep(50);
                    //new genstr_params();
                    //Task.Delay(1000);
                    //TaskUtil.Sleep(interval.Value).Wait();
                    //b.IncrementMe++;

                    long tick2 = Time.Tick64;

                    long diff = tick2 - tick;
                    Con.WriteLine(diff);
                    //GlobalIntervalReporter.Singleton.Report("diff", diff);
                }
            });

            while (true)
            {
                string line = Con.ReadLine("interval(msec)>");
                int msec = line.ToInt();
                interval.Set(msec);
            }

        }

        public class genstr_params
        {
            public string apiKey;
            public int n, length;
            public string characters;
        }

        public class genstr_response
        {
            public class random_t
            {
                public string[] data { get; set; }
                public DateTime completionTime { get; set; }
            }

            public random_t random { get; set; }
            public int bitsUsed { get; set; }
            public int bitsLeft { get; set; }
            public int requestsLeft { get; set; }
            public int advisoryDelay { get; set; }
        }

        public class rpc_handler_test : JsonRpcServerHandler
        {
            [RpcMethod]
            public string Test(int a, int b, int c, int d=1)
            {
                Dbg.Where("こんにちは");
                return $"Hello {a},{b},{c},{d}";
            }

            [RpcMethod]
            public async Task<string> Test2(int a)
            {
                await TaskUtil.Sleep(500);
                return "Hello " + a.ToString();
            }

            [RpcMethod]
            public async Task<string> Test3(int a)
            {
                await TaskUtil.Sleep(500);
                return "! " + a;
            }

            [RpcMethod]
            public string Test4(object o)
            {
                return ((object)o).ObjectToJson(compact: true);
            }

            [RpcMethod]
            public string Ping()
            {
                return "Hello";
            }

            [RpcMethod]
            public void Ping2()
            {
                throw new ApplicationException("ha");
            }

            [RpcMethod]
            public async Task Ping3()
            {
                await TaskUtil.Sleep(500);
                //throw new ApplicationException("ha");
            }
        }

        public static void jsonrpc_http_server_test()
        {
            /*rpc_handler_test x = new rpc_handler_test();
            object o = x.InvokeMethod("Test", 3).Result;
            string r = (string)o;
            r.Print();
            return;*/

            HttpServerBuilderConfig http_cfg = new HttpServerBuilderConfig()
            {
            };
            JsonRpcServerConfig rpc_cfg = new JsonRpcServerConfig()
            {
            };
            rpc_handler_test h = new rpc_handler_test();
            var s = JsonHttpRpcListener.StartServer(http_cfg, rpc_cfg, h);

            Con.ReadLine("Enter>");

            s.StopAsync().Wait();
        }

        public static void jsonrpc_test_with_random_api()
        {
            string key = "193ede53-7bd8-44b1-9662-40bd17ff0e67";
            string url = "https://api.random.org/json-rpc/1/invoke";

            JsonRpcHttpClient c = new JsonRpcHttpClient(url);

            genstr_params p = new genstr_params()
            {
                apiKey = key,
                n = 8,
                length = 8,
                characters = "0123456789",
            };

            /*
            var res = c.CallAdd<genstr_response>("generateStrings", p);
            var res2 = c.CallAdd<genstr_response>("generateStrings", p);
            c.CallAll(false).Wait();

            Con.WriteLine(res.ToString());
            Con.WriteLine(res2.ToString());*/

            var res3 = c.CallOne<genstr_response>("generateStrings", p, true).Result;
            Con.WriteLine(res3.ToString());
        }

        public static void json_test()
        {
            /*List<DBTestSettings> o = new List<DBTestSettings>();
            o.Add(new DBTestSettings() { DBConnectStr = "Hello" });
            o.Add(new DBTestSettings() { DBConnectStr = "Neko" });
            o.Add(new DBTestSettings() { DBConnectStr = "Cat" });
            o.Add(new DBTestSettings() { DBConnectStr = "Dog\ncat\"z" });
            o.Add(null);
            string json = Json.SerializeLog(o.ToArray());
            json.Print();

            StringReader r = new StringReader(json.ReplaceStr("}",""));
            Json.DeserializeLargeArrayAsync<DBTestSettings>(r, item => { item.ObjectToJson().Print(); return true; }, (str, exc) => { exc.ToString().Print(); return true; }).Wait();*/

            DBTestSettings db1 = new DBTestSettings();
            DBTestSettings db2 = new DBTestSettings();
            DBTestSettings db3 = new DBTestSettings();

            DBTestSettings[] dbs = new DBTestSettings[] { db1, db2, db3, };
            List<DBTestSettings> o = new List<DBTestSettings>(dbs);

            o.ObjectToJson(true).Print();
        }

        public static void db_test()
        {
            Cfg<DBTestSettings> cfg = new Cfg<DBTestSettings>();

            Database db = new Database(cfg.ConfigSafe.DBConnectStr);

            db.Tran(() =>
            {
                db.Query("select * from test");

                Data d = db.ReadAllData();

                Json.Serialize(d).Print();

                return true;
            });
        }

        [Serializable]
        public class SlackTestSecretSettings
        {
            public string ClientId { get; set; }
            public string ClientSecret { get; set; }
            public string AccessToken { get; set; }
        }

        public static void slack_test()
        {
            Cfg<SlackTestSecretSettings> secret = new Cfg<SlackTestSecretSettings>();
            {
                SlackApi a = new SlackApi(secret.ConfigSafe.ClientId, secret.ConfigSafe.AccessToken);

                a.DebugPrintResponse = true;

                a.AuthGenerateAuthorizeUrl("client identify", "https://tools.sehosts.com/", "abc").Print();

                //var at = a.AuthGetAccessToken(secret.ConfigSafe.ClientSecret, "47656437648.414467157330.e53934f3cd8a1d28c64b2e17b2f97422f609bf702fd6eb5267765e7ddfbd7011", "https://tools.sehosts.com/");
                //at.InnerDebug();

                var cl = a.GetChannelsListAsync().Result;
                string channel_id = "";
                foreach (var c in cl.Channels)
                {
                    if (c.name.IsSamei("test"))
                    {
                        channel_id = c.id;
                        Con.WriteLine(c.created.ToDateTimeOfSlack().ToLocalTime().ToDtStr());
                    }
                }

                a.PostMessageAsync(channel_id, $"こんにちは！ \t{Time.NowDateTime.ToDtStr(true, DtstrOption.All, true)}", true).Wait();
            }
        }

        public class TwConfig
        {
        }

        public static void twitter_test()
        {
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
            DnHttpClient c = new DnHttpClient();
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

        private static readonly ConcurrentExclusiveSchedulerPair _concurrentPair
    = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, 2);

        static void async_test()
        {
            //Task.Run(async_task1);

            //Func<Task> x = async_task1;
            //Task.Factory.StartNew(async_task1);

            /*TaskVm<string, int> tt = new TaskVm<string, int>(async_task1, 12345);

            Dbg.WriteCurrentThreadId("abort");
            string str = tt.GetResult();
            Con.WriteLine("ret = " + str);*/

            //string s = async_test_x().Result;
            //Dbg.WriteCurrentThreadId("ret = " + s);

            try
            {
                var x = async_test_x().Result;
                //Task<string> t = async_task_main_proc(123);
                //CancellationTokenSource tsc = new CancellationTokenSource(2000);
                //t.Wait(tsc.Token);
            }
            catch (Exception ex)
            {
                ex.ToString().Print();
            }

            Con.ReadLine(">");
        }

        static async Task<string> async_test_x()
        {
            CancellationTokenSource glaceful = new CancellationTokenSource(100);
            CancellationTokenSource abort = new CancellationTokenSource(1000);
            Task<string> task1 = TaskVm<string, int>.NewTask(async_task_main_proc_2, 123, glaceful.Token, abort.Token);

            Dbg.WriteCurrentThreadId("async_test_x: before await");
            string ret = await task1;
            Dbg.WriteCurrentThreadId("async_test_x: after await. ret = " + ret);

            return ret;
        }

        static async Task<string> async_task_main_proc_2(int arg)
        {
            await Task.Delay(100);

            try
            {
                string s = await async_task_main_proc(arg);

                return s;
            }
            finally
            {
                Dbg.WriteCurrentThreadId("Finally2 start");
                try
                {
                    string s = await async_task_main_proc(arg);
                }
                finally
                {
                    Dbg.WriteCurrentThreadId("Finally2 end");
                }
            }
        }

        static async Task<string> async_task_main_proc(int arg)
        {
            try
            {
                long last = Time.Tick64;
                long start = Time.Tick64;
                while (true)
                {
                    long now = Time.Tick64;
                    long diff = now - last;
                    last = now;

                    Dbg.WriteCurrentThreadId("tick = " + diff);

                    var e = new AsyncEvent();

                    //if (TaskUtil.CurrentTaskVmGracefulCancel.IsCancellationRequested)
                    //{
                    //    throw new TaskCanceledException();
                    //}

                    if (true)
                    {
                        await fire_test(e);

                        //await Task.Delay(5);
                        //await AsyncWaiter.Sleep(5);
                        //await e.Wait();
                        //await e.Wait();
                    }
                    else
                    {
                        ThreadObj.Sleep(100);
                    }

                    //await Task.Delay(100, tsc.Token);

                    if ((now - start) >= 3000)
                    {
                        //break;
                        throw new ApplicationException("ねこ");
                    }
                }
                return "Hello";
            }
            finally
            {
                Dbg.WriteCurrentThreadId("Finally1");
            }
        }

        static async Task fire_test(AsyncEvent e)
        {
            await AsyncWaiter.Sleep(200);
            e.Set();
        }

        static async Task<string> async_task1(int arg)
        {
            Dbg.WriteCurrentThreadId("a " + arg.ToString());

            //throw new ApplicationException("zzz");
            await Task.Delay(200);

            Dbg.WriteCurrentThreadId("u");

            await Task.Yield();

            Dbg.WriteCurrentThreadId("v");

            await async_task2();
            Dbg.WriteCurrentThreadId("b");

            CancellationTokenSource tsc = new CancellationTokenSource();

            Dbg.WriteCurrentThreadId("cancel test start");

            async_task_cancel_fire_test(tsc);

            Dbg.WriteCurrentThreadId("cancel test c");

            await TaskUtil.WhenCanceledOrTimeouted(tsc.Token, 1000);

            Dbg.WriteCurrentThreadId("cancel test end");

            return "aho";
        }

        static async void async_task_cancel_fire_test(CancellationTokenSource tsc)
        {
            Dbg.WriteCurrentThreadId("async_task_cancel_fire_test a");
            await Task.Delay(200);
            Dbg.WriteCurrentThreadId("async_task_cancel_fire_test b");

            tsc.Cancel();
        }

        static async Task async_task2()
        {
            Dbg.WriteCurrentThreadId("c");
            await Task.Delay(200);
            //throw new ApplicationException("aho");
            Dbg.WriteCurrentThreadId("d");
        }


        static int race_test_int = 0;

        static async Task<int> task_race_main()
        {
            int num = 10;
            List<Task<int>> tasks = new List<Task<int>>();
            for (int i = 0; i < num; i++)
            {
                tasks.Add(task_race_worker());
            }

            var t = Task.WhenAny(tasks.ToArray());

            await t;

            int a = t.Result.Result;

            return 0;
        }

        static async Task<int> task_race_worker()
        {
            while (true)
            {
                if ((race_test_int % 2) != 0)
                {
                    throw new ApplicationException("race_test_int != 0");
                }

                race_test_int++;

                Thread.Sleep(Secure.Rand31i() % 100);

                race_test_int++;

                await Task.Delay(Secure.Rand31i() % 1000);

                Con.WriteLine(race_test_int);
            }
        }

        static async Task<string> async1()
        {
            //await Task.WhenAny(Task.Run(async2), Task.Run(async3));
            await Task.WhenAny(async2(), async3());
            //await async2();
            return "";
        }

        static async Task<string> async2()
        {
            while (true)
            {
                Console.WriteLine("async2 start ID=" + ThreadObj.CurrentThreadId);
                Thread.Sleep(Secure.Rand31i() % 1000);
                Console.WriteLine("async2 stop ID=" + ThreadObj.CurrentThreadId);
                await Task.Delay(Secure.Rand31i() % 1000);
            }
        }

        static async Task<string> async3()
        {
            while (true)
            {
                Console.WriteLine("async3 start ID=" + ThreadObj.CurrentThreadId);
                Thread.Sleep(Secure.Rand31i() % 1000);
                Console.WriteLine("async3 stop ID=" + ThreadObj.CurrentThreadId);
                await Task.Delay(Secure.Rand31i() % 1000);
            }
        }
    }
}
