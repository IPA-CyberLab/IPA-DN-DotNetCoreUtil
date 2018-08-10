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
using System.Net;
using System.Net.Cache;
using System.Drawing;
using System.Runtime.InteropServices;

using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil
{
    public class WebResponseException : Exception
    {
        public WebResponseException(string message) : base(message) { }
    }

    public abstract class WebResponseBasic
    {
        public abstract void CheckError();
    }

    public class WebRet
    {
        public string Url { get; }
        public string ContentsType { get; }
        public byte[] Data { get; }
        public string MediaType { get; }
        public string CharSet { get; }
        public Encoding DefaultEncoding { get; }
        public WebApi Api { get; }

        public WebRet(WebApi webapi, string url, string contents_type, byte[] data)
        {
            this.Api = webapi;
            this.Url = url.NonNull();
            this.ContentsType = contents_type.NonNull();

            try
            {
                var ct = new System.Net.Mime.ContentType(this.ContentsType);
                this.MediaType = ct.MediaType.NonNull();
                this.CharSet = ct.CharSet.NonNull();
            }
            catch
            {
                this.MediaType = this.ContentsType;
                this.CharSet = "";
            }

            try
            {
                this.DefaultEncoding = Encoding.GetEncoding(this.CharSet);
            }
            catch
            {
                this.DefaultEncoding = Str.Utf8Encoding;
            }

            this.Data = data.NonNull();

            if (this.Api.DebugPrintResponse)
            {
                Json.Normalize(this.ToString()).Debug();
            }
        }

        public override string ToString() => this.Data.GetString(this.DefaultEncoding);
        public string ToString(Encoding encoding) => this.Data.GetString(encoding);

        dynamic json_dynamic = null;
        public dynamic JsonDynamic
        {
            get
            {
                if (json_dynamic == null)
                {
                    json_dynamic = Json.DeserializeDynamic(this.ToString());
                }
                return json_dynamic;
            }
        }

        public T Deserialize<T>()
        {
            return Json.Deserialize<T>(this.ToString(), this.Api.Json_IncludeNull, this.Api.MaxDepth);
        }

        public T DeserializeAndCheckError<T>() where T: WebResponseBasic
        {
            T t = Deserialize<T>();

            t.CheckError();

            return t;
        }
    }

    public enum WebApiMethods
    {
        GET,
        DELETE,
        POST,
        PUT,
    }

    public class WebApi
    {
        public int TimeoutSecs { get; set; } = 5;
        public int MaxRecvSize { get; set; } = 100 * 1024 * 1024;
        public bool SslAccentAnyCerts { get; set; } = false;
        public List<string> SslAcceptCertSHA1HashList { get; set; } = new List<string>();
        public Encoding RequestEncoding { get; set; } = Str.Utf8Encoding;

        public bool Json_IncludeNull { get; set; } = false;
        public bool Json_EscapeHtml { get; set; } = false;
        public int? MaxDepth { get; set; } = Json.DefaultMaxDepth;

        public bool DebugPrintResponse { get; set; } = false;

        public string BuildQueryString(params Tuple<string, string>[] query_list)
        {
            StringWriter w = new StringWriter();
            int count = 0;
            if (query_list != null)
            {
                foreach (var t in query_list)
                {
                    if (count != 0)
                    {
                        w.Write("&");
                    }
                    w.Write($"{t.Item1.EncodeUrl(this.RequestEncoding)}={t.Item2.EncodeUrl(this.RequestEncoding)}");
                    count++;
                }
            }
            return w.ToString();
        }

        virtual protected HttpWebRequest CreateWebRequest(WebApiMethods method, string url, params Tuple<string, string>[] query_list)
        {
            string qs = "";

            if (method == WebApiMethods.GET || method == WebApiMethods.DELETE)
            {
                qs = BuildQueryString(query_list);
                if (qs.IsEmpty() == false)
                {
                    url = url + "?" + qs;
                }
            }

            HttpWebRequest r = HttpWebRequest.CreateHttp(url);
            if (this.SslAccentAnyCerts)
            {
                r.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            else if (this.SslAcceptCertSHA1HashList != null && SslAcceptCertSHA1HashList.Count >= 1)
            {
                r.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    foreach (var s in this.SslAcceptCertSHA1HashList)
                        if (certificate.GetCertHashString().IsSamei(s)) return true;
                    return false;
                };
            }
            r.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            r.MaximumAutomaticRedirections = 10;
            r.AllowAutoRedirect = true;
            r.Timeout = r.ReadWriteTimeout = r.ContinueTimeout = this.TimeoutSecs * 1000;
            r.Method = method.ToString();

            if (method == WebApiMethods.POST || method == WebApiMethods.PUT)
            {
                r.ContentType = "application/x-www-form-urlencoded";
            }

            return r;
            
        }

        public WebRet RequestWithQuery(WebApiMethods method, string url, params Tuple<string, string>[] query_list)
        {
            HttpWebRequest r = CreateWebRequest(method, url, query_list);

            if (method == WebApiMethods.POST || method == WebApiMethods.PUT)
            {
                string qs = BuildQueryString(query_list);
                byte[] qs_byte = qs.GetBytes(this.RequestEncoding);

                Stream upload = r.GetRequestStream();
                upload.Write(qs_byte, 0, qs_byte.Length);
            }

            using (HttpWebResponse res = (HttpWebResponse)r.GetResponse())
            {
                byte[] data = res.GetResponseStream().ReadToEnd(this.MaxRecvSize);
                return new WebRet(this, res.ResponseUri.ToString(), res.ContentType, data);
            }
        }

        public virtual WebRet RequestWithJson(WebApiMethods method, string url, string json_string)
        {
            if (!(method == WebApiMethods.POST || method == WebApiMethods.PUT)) throw new ArgumentException("method");

            HttpWebRequest r = CreateWebRequest(method, url);

            r.ContentType = "application/json";

            json_string.Debug();

            byte[] upload_data = json_string.GetBytes(this.RequestEncoding);

            Stream upload = r.GetRequestStream();
            upload.Write(upload_data, 0, upload_data.Length);

            using (HttpWebResponse res = (HttpWebResponse)r.GetResponse())
            {
                byte[] data = res.GetResponseStream().ReadToEnd(this.MaxRecvSize);
                return new WebRet(this, res.ResponseUri.ToString(), res.ContentType, data);
            }
        }

        public string JsonSerialize(object obj)
        {
            return Json.Serialize(obj, this.Json_IncludeNull, this.Json_EscapeHtml, this.MaxDepth);
        }

        public virtual WebRet RequestWithJsonObject(WebApiMethods method, string url, object json_object)
        {
            return RequestWithJson(method, url, this.JsonSerialize(json_object));
        }

        public virtual WebRet RequestWithJsonDynamic(WebApiMethods method, string url, dynamic json_dynamic)
        {
            return RequestWithJson(method, url, Json.SerializeDynamic(json_dynamic));
        }
    }
}

