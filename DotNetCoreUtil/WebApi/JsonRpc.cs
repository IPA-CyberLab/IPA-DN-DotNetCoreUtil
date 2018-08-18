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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using IPA.DN.CoreUtil.Basic;
using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil.WebApi
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

        public JsonRpcRequest() { }

        public JsonRpcRequest(string method, object param, string id)
        {
            this.Method = method;
            this.Params = param;
            this.Id = id;
        }
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

    // 193ede53-7bd8-44b1-9662-40bd17ff0e67
    // https://api.random.org/json-rpc/1/invoke
    public abstract class JsonRpcClient
    {
        public int TimeoutSecs { get; set; } = 10;
        public int MaxRecvSize { get; set; } = 100 * 1024 * 1024;

        Queue<(JsonRpcRequest request, Ref<JsonRpcResponse> response)> call_queue = new Queue<(JsonRpcRequest request, Ref<JsonRpcResponse> response)>();

        public void CallAll()
        {
        }

        public void CallClear()
        {
            call_queue.Clear();
        }

        public Ref<JsonRpcResponse> CallAdd(string method, object param)
        {
            var ret = new Ref<JsonRpcResponse>();
            call_queue.Enqueue((new JsonRpcRequest(method, param, Str.NewGuid()), ret));
            return ret;
        }
    }

    public class JsonRpcHttpClient : JsonRpcClient
    {
        public WebApi InternalWebApi { get; set; } = new WebApi();
        public string ApiUrl { get; set; }

        public JsonRpcHttpClient(string api_url)
        {
            this.ApiUrl = api_url;
            this.InternalWebApi.TimeoutSecs = this.TimeoutSecs;
            this.MaxRecvSize = this.MaxRecvSize;
        }
    }
}
