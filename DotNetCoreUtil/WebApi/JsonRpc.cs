using System;
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
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using IPA.DN.CoreUtil.Basic;
using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil.WebApi
{
    public class JsonRpcException : Exception
    {
        public JsonRpcError RpcError { get; }
        public JsonRpcException(JsonRpcError err)
            : base($"Code={err.Code}, Message={err.Message.NonNull()}" +
                  (err == null || err.Data == null ? "" : $", Data={err.Data.ObjectToJson(compact: true)}"))
        {
            this.RpcError = err;
        }
    }

    public class JsonRpcRequest
    {
        [JsonProperty("jsonrpc")]
        public string Version { get; set; } = "2.0";

        [JsonProperty("method")]
        public string Method { get; set; } = "";

        [JsonProperty("params")]
        public object Params { get; set; } = null;

        [JsonProperty("id")]
        public string Id { get; set; } = null;

        public JsonRpcRequest() { }

        public JsonRpcRequest(string method, object param, string id)
        {
            this.Method = method;
            this.Params = param;
            this.Id = id;
        }
    }

    public class JsonRpcResponse<TResult> : JsonRpcResponse
        where TResult : class
    {
        public TResult ResultData
        {
            get => Result == null ? null : base.Result.ConvertJsonObject<TResult>();
            set => Result = value;
        }
    }

    public class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")]
        public string Version { get; set; } = "2.0";

        [JsonProperty("result")]
        public object Result { get; set; } = null;

        [JsonProperty("error")]
        public JsonRpcError Error { get; set; } = null;

        [JsonProperty("id")]
        public string Id { get; set; } = null;

        [JsonIgnore]
        public bool IsError => this.Error != null;

        [JsonIgnore]
        public bool IsOk => !IsError;

        public void CheckError()
        {
            if (this.IsError) throw new JsonRpcException(this.Error);
        }

        public override string ToString()
        {
            return this.ObjectToJson(compact: true);
        }
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

    public abstract class JsonRpcServer
    {
    }

    public class JsonHttpRpcServer : JsonRpcServer
    {
    }

    public abstract class JsonRpcClient
    {
        List<(JsonRpcRequest request, JsonRpcResponse response, Type response_data_type)> call_queue = new List<(JsonRpcRequest request, JsonRpcResponse response, Type response_data_type)>();

        public void CallClear()
        {
            call_queue.Clear();
        }

        public JsonRpcResponse<TResponse> CallAdd<TResponse>(string method, object param) where TResponse: class
        {
            var ret = new JsonRpcResponse<TResponse>();

            call_queue.Add((new JsonRpcRequest(method, param, Str.NewGuid()), ret, typeof(TResponse)));

            return ret;
        }

        public async Task CallAll(bool throw_each_error = false)
        {
            if (call_queue.Count == 0)
            {
                return;
            }

            string req = "";
            bool is_single = false;

            Dictionary<string, (JsonRpcRequest request, JsonRpcResponse response, Type response_data_type)> requests_table = new Dictionary<string, (JsonRpcRequest request, JsonRpcResponse response, Type response_data_type)>();
            List<JsonRpcRequest> requests = new List<JsonRpcRequest>();

            foreach (var o in this.call_queue)
            {
                requests_table.Add(o.request.Id, (o.request, o.response, o.response_data_type));
                requests.Add(o.request);
            }

            if (requests_table.Count == 1)
            {
                req = requests[0].ObjectToJson(compact: true);
            }
            else
            {
                req = requests.ObjectToJson(compact: true);
            }

            //req.Debug();

            string ret = await GetResponse(req);

            if (ret.StartsWith("{")) is_single = true;

            List<JsonRpcResponse> ret_list = new List<JsonRpcResponse>();

            if (is_single)
            {
                JsonRpcResponse r = ret.JsonToObject<JsonRpcResponse>();
                ret_list.Add(r);
            }
            else
            {
                JsonRpcResponse[] r = ret.JsonToObject<JsonRpcResponse[]>();
                ret_list = new List<JsonRpcResponse>(r);
            }

            foreach (var res in ret_list)
            {
                if (res.Id.IsFilled())
                {
                    if (requests_table.ContainsKey(res.Id))
                    {
                        var q = requests_table[res.Id];

                        q.response.Error = res.Error;
                        q.response.Id = res.Id;
                        q.response.Result = res.Result;
                        q.response.Version = res.Version;
                    }
                }
            }

            if (throw_each_error)
            {
                foreach (var r in ret_list)
                {
                    r.CheckError();
                }
            }
        }

        public async Task<JsonRpcResponse<TResponse>> CallOne<TResponse>(string method, object param, bool throw_each_error = false) where TResponse : class
        {
            JsonRpcResponse<TResponse> res = CallAdd<TResponse>(method, param);
            await CallAll(throw_each_error);
            return res;
        }

        public abstract Task<string> GetResponse(string req);
    }

    public class JsonRpcHttpClient : JsonRpcClient
    {
        public WebApi WebApi { get; set; } = new WebApi();
        public string ApiBaseUrl { get; set; }

        public JsonRpcHttpClient(string api_url)
        {
            this.ApiBaseUrl = api_url;
        }
        
        public override async Task<string> GetResponse(string req)
        {
            WebRet ret = await this.WebApi.RequestWithPostData(this.ApiBaseUrl, req.GetBytes_UTF8(), "application/json");

            return ret.ToString();
        }
    }
}
