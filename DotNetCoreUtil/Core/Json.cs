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
using System.Web;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil
{
    public static class Json
    {
        public const int DefaultMaxDepth = 8;

        public static string Serialize(object obj, bool include_null = false, bool escape_html = false, int? max_depth = Json.DefaultMaxDepth)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings()
            {
                MaxDepth = max_depth,
                NullValueHandling = include_null ? NullValueHandling.Include : NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                StringEscapeHandling = escape_html ? StringEscapeHandling.EscapeHtml : StringEscapeHandling.Default,
                
            };
            return JsonConvert.SerializeObject(obj, Formatting.Indented, setting);
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

