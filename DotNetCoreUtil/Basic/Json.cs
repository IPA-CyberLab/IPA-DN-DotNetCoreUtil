﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil.Basic
{
    public class JsonRpcRequest
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "2.0";

        [JsonProperty("method")]
        public string Method { get; set; } = "";

        [JsonProperty("params")]
        public object Params { get; set; } = null;

        [JsonProperty("id")]
        public string Id { get; set; } = null;
    }

    public class JsonRpcResponse
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "2.0";

        [JsonProperty("result")]
        public object Result { get; set; } = null;

        [JsonProperty("error")]
        public JsonRpcError Error { get; set; } = null;

        [JsonProperty("id")]
        public string Id { get; set; } = null;
    }

    public class JsonRpcError
    {
        [JsonProperty("code")]
        public int Code { get; set; } = 0;

        [JsonProperty("message")]
        public string Message { get; set; } = null;

        [JsonProperty("data")]
        public object Data { get; set; } = null;
    }

    public static class JsonRpc
    {
        public static string ClientCallsBuild((string method, object @params, string id)[] calls)
        {
            foreach (var v in calls)
            {
            }
            return "";
        }
    }

    public static class Json
    {
        public const int DefaultMaxDepth = 8;

        public static string SerializeLog(IEnumerable item_array, bool include_null = false, bool escape_html = false, int? max_depth = Json.DefaultMaxDepth)
        {
            StringWriter w = new StringWriter();
            SerializeLogToTextWriterAsync(w, item_array, include_null, escape_html, max_depth).Wait();
            return w.ToString();
        }

        public static async Task SerializeLogToTextWriterAsync(TextWriter w, IEnumerable item_array, bool include_null = false, bool escape_html = false, int? max_depth = Json.DefaultMaxDepth)
        {
            foreach (var item in item_array)
            {
                await w.WriteLineAsync(Serialize(item, include_null, escape_html, max_depth, true));
            }
        }

        public static string Serialize(object obj, bool include_null = false, bool escape_html = false, int? max_depth = Json.DefaultMaxDepth, bool compact = false)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings()
            {
                MaxDepth = max_depth,
                NullValueHandling = include_null ? NullValueHandling.Include : NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                StringEscapeHandling = escape_html ? StringEscapeHandling.EscapeHtml : StringEscapeHandling.Default,
                
            };
            return JsonConvert.SerializeObject(obj, compact ? Formatting.None : Formatting.Indented, setting);
        }

        public static T Deserialize<T>(string str, bool include_null = false, int? max_depth = Json.DefaultMaxDepth)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings()
            {
                MaxDepth = max_depth,
                NullValueHandling = include_null ? NullValueHandling.Include : NullValueHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
            };
            return JsonConvert.DeserializeObject<T>(str, setting);
        }

        public static async Task<bool> DeserializeLargeArrayAsync<T>(TextReader txt, Func<T, bool> item_read_callback, Func<string, Exception, bool> parse_error_callback = null, bool include_null = false, int? max_depth = Json.DefaultMaxDepth)
        {
            while (true)
            {
                string line = await txt.ReadLineAsync();
                if (line == null)
                {
                    return true;
                }
                if (line.IsFilled())
                {
                    object obj = null;
                    try
                    {
                        obj = (object)Deserialize<T>(line, include_null, max_depth);
                    }
                    catch (Exception ex)
                    {
                        if (parse_error_callback(line, ex) == false)
                        {
                            return false;
                        }
                    }

                    if (item_read_callback((T)obj) == false)
                    {
                        return false;
                    }
                }
            }
        }

        public static string SerializeDynamic(dynamic d)
        {
            JObject o = (JObject)d;

            return o.ToString();
        }

        public static dynamic DeserializeDynamic(string str)
        {
            dynamic ret = JObject.Parse(str);
            return ret;
        }

        public static dynamic NewDynamicObject()
        {
            JObject o = new JObject();

            return o;
        }

        public static string Normalize(string str)
        {
            dynamic d = DeserializeDynamic(str);

            return SerializeDynamic(d);
        }
    }
}

