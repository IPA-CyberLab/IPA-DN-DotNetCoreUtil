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
using System.Net;
using System.Net.Cache;
using System.Drawing;
using System.Runtime.InteropServices;

using IPA.DN.CoreUtil.Basic;
using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil.WebApi
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
        public int TimeoutSecs { get; set; } = 100;
        public int MaxRecvSize { get; set; } = 100 * 1024 * 1024;
        public bool SslAccentAnyCerts { get; set; } = false;
        public List<string> SslAcceptCertSHA1HashList { get; set; } = new List<string>();
        public Encoding RequestEncoding { get; set; } = Str.Utf8Encoding;

        public bool Json_IncludeNull { get; set; } = false;
        public bool Json_EscapeHtml { get; set; } = false;
        public int? MaxDepth { get; set; } = Json.DefaultMaxDepth;

        public bool DebugPrintResponse { get; set; } = false;

        public SortedList<string, string> RequestHeaders = new SortedList<string, string>();

        public string BuildQueryString(params (string name, string value)[] query_list)
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
                    w.Write($"{t.name.EncodeUrl(this.RequestEncoding)}={t.value.EncodeUrl(this.RequestEncoding)}");
                    count++;
                }
            }
            return w.ToString();
        }

        public void AddHeader(string name, string value)
        {
            if (name.IsEmpty()) return;
            lock (this.RequestHeaders)
            {
                if (value.IsEmpty() == false)
                {
                    if (this.RequestHeaders.ContainsKey(name))
                        this.RequestHeaders[name] = value;
                    else
                        this.RequestHeaders.Add(name, value);
                }
                else
                {
                    if (this.RequestHeaders.ContainsKey(name))
                        this.RequestHeaders.Remove(name);
                }
            }
        }

        virtual protected HttpWebRequest CreateWebRequest(WebApiMethods method, string url, string post_content_type = "application/x-www-form-urlencoded", params (string name, string value)[] query_list)
        {
            string qs = "";

            if (post_content_type.IsEmpty()) post_content_type = "application/x-www-form-urlencoded";

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
                r.ContentType = post_content_type;
            }

            foreach (string name in this.RequestHeaders.Keys)
            {
                string value = this.RequestHeaders[name];
                this.RequestHeaders.Add(name, value);
            }

            return r;
            
        }

        public async Task<WebRet> RequestWithQuery(WebApiMethods method, string url, string post_content_type = "application/x-www-form-urlencoded", params (string name, string value)[] query_list)
        {
            HttpWebRequest r = CreateWebRequest(method, url, null, query_list);

            if (method == WebApiMethods.POST || method == WebApiMethods.PUT)
            {
                string qs = BuildQueryString(query_list);
                byte[] qs_byte = qs.GetBytes(this.RequestEncoding);

                Stream upload = await r.GetRequestStreamAsync();
                await upload.WriteAsync(qs_byte, 0, qs_byte.Length);
            }

            using (HttpWebResponse res = (HttpWebResponse)await r.GetResponseAsync())
            {
                byte[] data = await res.GetResponseStream().ReadToEndAsync(this.MaxRecvSize);
                return new WebRet(this, res.ResponseUri.ToString(), res.ContentType, data);
            }
        }

        public async Task<WebRet> RequestWithPostData(string url, byte[] post_data, string post_contents_type = "application/json")
        {
            if (post_contents_type.IsEmpty()) post_contents_type = "application/json";
            HttpWebRequest r = CreateWebRequest(WebApiMethods.POST, url, post_contents_type, null);

            Stream upload = await r.GetRequestStreamAsync();
            await upload.WriteAsync(post_data, 0, post_data.Length);

            using (HttpWebResponse res = (HttpWebResponse)await r.GetResponseAsync())
            {
                byte[] data = await res.GetResponseStream().ReadToEndAsync(this.MaxRecvSize);
                return new WebRet(this, res.ResponseUri.ToString(), res.ContentType, data);
            }
        }

        public virtual async Task<WebRet> RequestWithJson(WebApiMethods method, string url, string json_string)
        {
            if (!(method == WebApiMethods.POST || method == WebApiMethods.PUT)) throw new ArgumentException("method");

            HttpWebRequest r = CreateWebRequest(method, url, null);

            r.ContentType = "application/json";

            json_string.Debug();

            byte[] upload_data = json_string.GetBytes(this.RequestEncoding);

            Stream upload = await r.GetRequestStreamAsync();
            await upload.WriteAsync(upload_data, 0, upload_data.Length);

            using (HttpWebResponse res = (HttpWebResponse)await r.GetResponseAsync())
            {
                byte[] data = await res.GetResponseStream().ReadToEndAsync(this.MaxRecvSize);
                return new WebRet(this, res.ResponseUri.ToString(), res.ContentType, data);
            }
        }

        public string JsonSerialize(object obj)
        {
            return Json.Serialize(obj, this.Json_IncludeNull, this.Json_EscapeHtml, this.MaxDepth);
        }

        public virtual async Task<WebRet> RequestWithJsonObject(WebApiMethods method, string url, object json_object)
        {
            return await RequestWithJson(method, url, this.JsonSerialize(json_object));
        }

        public virtual async Task<WebRet> RequestWithJsonDynamic(WebApiMethods method, string url, dynamic json_dynamic)
        {
            return await RequestWithJson(method, url, Json.SerializeDynamic(json_dynamic));
        }
    }
}

