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
using System.Security.Cryptography.X509Certificates;
using System.Web;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
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
        public Encoding DefaultEncoding { get; } = null;
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
                if (this.CharSet.IsFilled())
                {
                    this.DefaultEncoding = Encoding.GetEncoding(this.CharSet);
                }
            }
            catch
            {
            }

            if (this.DefaultEncoding == null)
            {
                this.DefaultEncoding = webapi.RequestEncoding;
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

    public class WebApi : IDisposable
    {
        public const int DefaultTimeoutMsecs = 60 * 1000;
        public int TimeoutMsecs { get => (int)Client.Timeout.TotalMilliseconds; set => Client.Timeout = new TimeSpan(0, 0, 0, 0, value); }

        public const long DefaultMaxRecvSize = 100 * 1024 * 1024;
        public long MaxRecvSize { get => this.Client.MaxResponseContentBufferSize; set => this.Client.MaxResponseContentBufferSize = value; }
        public bool SslAccentAnyCerts { get; set; } = false;
        public List<string> SslAcceptCertSHA1HashList { get; set; } = new List<string>();
        public Encoding RequestEncoding { get; set; } = Str.Utf8Encoding;

        public bool Json_IncludeNull { get; set; } = false;
        public bool Json_EscapeHtml { get; set; } = false;
        public int? MaxDepth { get; set; } = Json.DefaultMaxDepth;

        public bool DebugPrintResponse { get; set; } = false;

        public SortedList<string, string> RequestHeaders = new SortedList<string, string>();

        HttpClientHandler client_handler = new HttpClientHandler();

        public X509CertificateCollection ClientCerts { get => this.client_handler.ClientCertificates; }

        public HttpClient Client { get; private set; }

        public WebApi()
        {
            this.client_handler.AllowAutoRedirect = true;
            this.client_handler.MaxAutomaticRedirections = 10;

            this.Client = new HttpClient(this.client_handler, true);
            this.MaxRecvSize = WebApi.DefaultMaxRecvSize;
            this.TimeoutMsecs = WebApi.DefaultTimeoutMsecs;
        }

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

        virtual protected HttpRequestMessage CreateWebRequest(WebApiMethods method, string url, params (string name, string value)[] query_list)
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

            HttpRequestMessage req_msg = new HttpRequestMessage(new HttpMethod(method.ToString()), url);

            CacheControlHeaderValue cache_control = new CacheControlHeaderValue();
            cache_control.NoStore = true;
            cache_control.NoCache = true;
            req_msg.Headers.CacheControl = cache_control;

            try
            {
                if (this.SslAccentAnyCerts)
                {
                    this.client_handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                }
                else if (this.SslAcceptCertSHA1HashList != null && SslAcceptCertSHA1HashList.Count >= 1)
                {
                    this.client_handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                    {
                        foreach (var s in this.SslAcceptCertSHA1HashList)
                            if (cert.GetCertHashString().IsSamei(s)) return true;
                        return false;
                    };
                }
            }
            catch
            {
            }

            foreach (string name in this.RequestHeaders.Keys)
            {
                string value = this.RequestHeaders[name];
                req_msg.Headers.Add(name, value);
            }

            return req_msg;
        }

        public static void ThrowIfError(HttpResponseMessage res)
        {
            res.EnsureSuccessStatusCode();
        }

        public async Task<WebRet> RequestWithQuery(WebApiMethods method, string url, string post_contents_type = "application/x-www-form-urlencoded", params (string name, string value)[] query_list)
        {
            if (post_contents_type.IsEmpty()) post_contents_type = "application/x-www-form-urlencoded";
            HttpRequestMessage r = CreateWebRequest(method, url, query_list);

            if (method == WebApiMethods.POST || method == WebApiMethods.PUT)
            {
                string qs = BuildQueryString(query_list);

                r.Content = new StringContent(qs, this.RequestEncoding, post_contents_type);
            }

            using (HttpResponseMessage res = await this.Client.SendAsync(r, HttpCompletionOption.ResponseContentRead))
            {
                ThrowIfError(res);
                byte[] data = await res.Content.ReadAsByteArrayAsync();
                return new WebRet(this, url, res.Content.Headers.TryGetContentsType(), data);
            }
        }

        public async Task<WebRet> RequestWithPostData(string url, byte[] post_data, string post_contents_type = "application/json")
        {
            if (post_contents_type.IsEmpty()) post_contents_type = "application/json";
            HttpRequestMessage r = CreateWebRequest(WebApiMethods.POST, url,  null);

            r.Content = new ByteArrayContent(post_data);
            r.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(post_contents_type);
            
            using (HttpResponseMessage res = await this.Client.SendAsync(r, HttpCompletionOption.ResponseContentRead))
            {
                ThrowIfError(res);
                byte[] data = await res.Content.ReadAsByteArrayAsync();
                string type = res.Content.Headers.TryGetContentsType();
                return new WebRet(this, url, res.Content.Headers.TryGetContentsType(), data);
            }
        }

        public virtual async Task<WebRet> RequestWithJson(WebApiMethods method, string url, string json_string)
        {
            if (!(method == WebApiMethods.POST || method == WebApiMethods.PUT)) throw new ArgumentException($"Invalid method: {method.ToString()}");

            HttpRequestMessage r = CreateWebRequest(method, url, null);

            byte[] upload_data = json_string.GetBytes(this.RequestEncoding);

            r.Content = new ByteArrayContent(upload_data);
            r.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            using (HttpResponseMessage res = await this.Client.SendAsync(r, HttpCompletionOption.ResponseContentRead))
            {
                ThrowIfError(res);
                byte[] data = await res.Content.ReadAsByteArrayAsync();
                return new WebRet(this, url, res.Content.Headers.TryGetContentsType(), data);
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

        Once dispose_once;
        public void Dispose()
        {
            if (dispose_once.IsFirstCall())
            {
                this.Client.Dispose();
                this.Client = null;
            }
        }
    }
}

