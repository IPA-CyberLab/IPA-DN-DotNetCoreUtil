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
using System.Dynamic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castle.DynamicProxy;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using IPA.DN.CoreUtil.Basic;
using IPA.DN.CoreUtil.Helper.Basic;
using Microsoft.AspNetCore.Routing;

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
        [JsonIgnore]
        public TResult ResultData
        {
            get => Result == null ? null : base.Result.ConvertJsonObject<TResult>();
            set => Result = value;
        }
    }

    public class JsonRpcResponseOk : JsonRpcResponse
    {
        [JsonIgnore]
        public override JsonRpcError Error { get => null; set { } }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Include)]
        public override object Result { get; set; } = null;
    }

    public class JsonRpcResponseError : JsonRpcResponse
    {
        [JsonIgnore]
        public override object Result { get => null; set { } }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Include)]
        public override JsonRpcError Error { get; set; } = null;
    }

    public class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")]
        public virtual string Version { get; set; } = "2.0";

        [JsonProperty("result")]
        public virtual object Result { get; set; } = null;

        [JsonProperty("error")]
        public virtual JsonRpcError Error { get; set; } = null;

        [JsonProperty("id", NullValueHandling = NullValueHandling.Include)]
        public virtual string Id { get; set; } = null;

        [JsonIgnore]
        public virtual bool IsError => this.Error != null;

        [JsonIgnore]
        public virtual bool IsOk => !IsError;

        public virtual void CheckError()
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
        public JsonRpcError() { }
        public JsonRpcError(int code, string message, object data = null)
        {
            this.Code = code;
            this.Message = message.NonNull();
            if (this.Message.IsEmpty()) this.Message = $"JSON-RPC Error {code}";
            this.Data = data;
        }

        [JsonProperty("code")]
        public int Code { get; set; } = 0;

        [JsonProperty("message")]
        public string Message { get; set; } = null;

        [JsonProperty("data")]
        public object Data { get; set; } = null;
    }

    public class RpcInterfaceAttribute : Attribute { }

    public class RpcMethodInfo
    {
        public string Name { get; }
        public MethodInfo Method { get; }
        public Dictionary<string, (ParameterInfo info, int index)> ParametersByName { get; } = new Dictionary<string, (ParameterInfo info, int index)>();
        public ParameterInfo[] ParametersByIndex { get; }
        public ParameterInfo ReturnParameter { get; }
        public bool IsTask { get; }
        public Type TaskType { get; }
        public bool IsGenericTask { get; }
        public Type GeneticTaskType { get; }

        public RpcMethodInfo(Type target_class, string method_name)
        {
            MethodInfo method_info = target_class.GetMethod(method_name);
            if (method_info == null)
            {
                throw new JsonRpcException(new JsonRpcError(-32601, "Method not found"));
            }

            var r = method_info.ReturnParameter;
            bool is_task = false;
            if (r.ParameterType == typeof(Task) || r.ParameterType.IsSubclassOf(typeof(Task))) is_task = true;

            if (is_task == false)
            {
                throw new ApplicationException($"The return value of the function '{method_info.Name}' is not a Task.");
            }

            if (is_task)
            {
                this.TaskType = r.ParameterType;
                Type[] generic_types = TaskType.GenericTypeArguments;
                if (generic_types.Length == 1)
                {
                    this.IsGenericTask = true;
                    this.GeneticTaskType = generic_types[0];
                }
                else if (generic_types.Length >= 2)
                {
                    throw new ApplicationException("generic_types.Length >= 2");
                }
            }

            this.IsTask = is_task;
            this.Method = method_info;
            this.Name = method_name;
            this.ReturnParameter = r;
            this.ParametersByIndex = method_info.GetParameters();

            var method_params = ParametersByIndex;
            for (int i = 0; i < method_params.Length; i++)
                this.ParametersByName.Add(method_params[i].Name, (method_params[i], i));
        }

        public async Task<object> InvokeMethod(object target_instance, string method_name, JObject param)
        {
            object[] in_params = new object[this.ParametersByIndex.Length];
            if (this.ParametersByIndex.Length == 1 && this.ParametersByIndex[0].ParameterType == typeof(System.Object))
            {
                in_params = new object[1] { param };
            }
            else
            {
                for (int i = 0; i < this.ParametersByIndex.Length; i++)
                {
                    ParameterInfo pi = this.ParametersByIndex[i];
                    if (param != null && param.TryGetValue(pi.Name, out var value))
                        in_params[i] = value.ToObject(pi.ParameterType);
                    else if (pi.HasDefaultValue)
                        in_params[i] = pi.DefaultValue;
                    else throw new ArgumentException($"The parameter '{pi.Name}' is missing.");
                }
            }

            object retobj = this.Method.Invoke(target_instance, in_params);

            if (this.IsTask == false)
                return Task.FromResult<object>(retobj);
            else
            {
                Type t = retobj.GetType();
                Task task = (Task)retobj;

                Dbg.WhereThread();

                await task;

                Dbg.WhereThread();

                var prop_mi = t.GetProperty("Result");
                object retvalue = prop_mi.GetValue(retobj);

                return retvalue;
            }
        }
    }

    public abstract class JsonRpcServerApi
    {
        public Type RpcInterface { get; }

        public JsonRpcServerApi()
        {
            this.RpcInterface = get_rpc_interface();
        }

        public CancellationTokenSource CancelSource { get; } = new CancellationTokenSource();
        public CancellationToken CancelToken { get => this.CancelSource.Token; }

        Dictionary<string, RpcMethodInfo> method_info_cache = new Dictionary<string, RpcMethodInfo>();
        public RpcMethodInfo GetMethodInfo(string method_name)
        {
            RpcMethodInfo m = null;
            lock (method_info_cache)
            {
                if (method_info_cache.ContainsKey(method_name) == false)
                    m = get_method_info_main(method_name);
                else
                    m = method_info_cache[method_name];
            }
            return m;
        }
        RpcMethodInfo get_method_info_main(string method_name)
        {
            RpcMethodInfo mi = new RpcMethodInfo(this.GetType(), method_name);
            if (this.RpcInterface.GetMethod(mi.Name) == null)
            {
                throw new ApplicationException($"The method '{method_name}' is not defined on the interface '{this.RpcInterface.Name}'.");
            }
            return mi;
        }

        public virtual Task<object> InvokeMethod(string method_name, JObject param)
        {
            var method_info = GetMethodInfo(method_name);
            return method_info.InvokeMethod(this, method_name, param);
        }

        virtual protected Type get_rpc_interface()
        {
            Type ret = null;
            Type t = this.GetType();
            var ints = t.GetTypeInfo().GetInterfaces();
            int num = 0;
            foreach (var f in ints)
                if (f.GetCustomAttribute<RpcInterfaceAttribute>() != null)
                {
                    ret = f;
                    num++;
                }
            if (num == 0) throw new ApplicationException($"The class '{t.Name}' has no interface with the RpcInterface attribute.");
            if (num >= 2) throw new ApplicationException($"The class '{t.Name}' has two or mode interfaces with the RpcInterface attribute.");
            return ret;
        }
    }

    public abstract class JsonRpcServer
    {
        public JsonRpcServerApi Api { get; }
        public JsonRpcServerConfig Config { get; }

        public JsonRpcServer(JsonRpcServerApi api, JsonRpcServerConfig cfg, CancellationToken cancel_token)
        {
            this.Api = api;
            this.Config = cfg;
        }

        public async Task<JsonRpcResponse> CallMethod(JsonRpcRequest req)
        {
            try
            {
                RpcMethodInfo method = this.Api.GetMethodInfo(req.Method);
                JObject in_obj = (JObject)req.Params;
                try
                {
                    object ret_obj = await this.Api.InvokeMethod(req.Method, in_obj);
                    return new JsonRpcResponseOk()
                    {
                        Id = req.Id,
                        Error = null,
                        Result = ret_obj,
                    };
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    throw ex.InnerException;
                }
            }
            catch (JsonRpcException ex)
            {
                return new JsonRpcResponseError()
                {
                    Id = req.Id,
                    Error = ex.RpcError,
                    Result = null,
                };
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) ex = ex.InnerException;
                return new JsonRpcResponseError()
                {
                    Id = req.Id,
                    Error = new JsonRpcError(-32603, ex.Message, ex.ToString()),
                    Result = null,
                };
            }
        }

        public async Task<string> CallMethods(string in_str)
        {
            bool is_single = false;
            List<JsonRpcRequest> request_list = new List<JsonRpcRequest>();
            try
            {
                if (in_str.StartsWith("{"))
                {
                    is_single = true;
                    JsonRpcRequest r = in_str.JsonToObject<JsonRpcRequest>();
                    request_list.Add(r);
                }
                else
                {
                    JsonRpcRequest[] rr = in_str.JsonToObject<JsonRpcRequest[]>();
                    request_list = new List<JsonRpcRequest>(rr);
                }
            }
            catch
            {
                throw new JsonRpcException(new JsonRpcError(-32700, "Parse error"));
            }

            List<JsonRpcResponse> response_list = new List<JsonRpcResponse>();

            foreach (JsonRpcRequest req in request_list)
            {
                try
                {
                    JsonRpcResponse res = await CallMethod(req);
                    if (req.Id != null) response_list.Add(res);
                }
                catch (Exception ex)
                {
                    JsonRpcException json_ex;
                    if (ex is JsonRpcException) json_ex = ex as JsonRpcException;
                    else json_ex = new JsonRpcException(new JsonRpcError(-32603, ex.Message, ex.ToString()));
                    JsonRpcResponseError res = new JsonRpcResponseError()
                    {
                        Id = req.Id,
                        Error = json_ex.RpcError,
                        Result = null,
                    };
                    if (req.Id != null) response_list.Add(res);
                }
            }

            if (is_single)
            {
                if (response_list.Count >= 1)
                    return response_list[0].ObjectToJson();
                else
                    return "";
            }
            else
                return response_list.ObjectToJson();
        }
    }

    public class JsonHttpRpcServer : JsonRpcServer
    {
        public JsonHttpRpcServer(JsonRpcServerApi api, JsonRpcServerConfig cfg, CancellationToken cancel_token) : base(api, cfg, cancel_token) { }

        public virtual async Task GetRequestHandler(HttpRequest request, HttpResponse response, RouteData route_data)
        {
            await response.WriteAsync("This is a JSON-RPC server.\r\nCurrent time : " + DateTime.Now.ToDtStr(true, DtstrOption.All, true));
        }

        public virtual async Task PostRequestHandler(HttpRequest request, HttpResponse response, RouteData route_data)
        {
            string ret_str = "";
            try
            {
                string in_str = (await request.Body.ReadToEndAsync(this.Config.MaxRequestBodyLen)).GetString_UTF8();
                Dbg.WriteLine("in_str: " + in_str);
                ret_str = await this.CallMethods(in_str);
            }
            catch (Exception ex)
            {
                JsonRpcException json_ex;
                if (ex is JsonRpcException) json_ex = ex as JsonRpcException;
                else json_ex = new JsonRpcException(new JsonRpcError(1234, ex.Message, ex.ToString()));

                ret_str = new JsonRpcResponseError()
                {
                    Error = json_ex.RpcError,
                    Id = null,
                    Result = null,
                }.ObjectToJson();
            }

            Dbg.WriteLine("ret_str: " + ret_str);

            byte[] ret_data = ret_str.GetBytes_UTF8();
            await response.Body.WriteAsync(ret_data, 0, ret_data.Length);
        }

        public void RegisterToHttpServer(IApplicationBuilder app, string template = "rpc")
        {
            RouteBuilder rb = new RouteBuilder(app);

            rb.MapGet(template, GetRequestHandler);
            rb.MapPost(template, PostRequestHandler);

            IRouter router = rb.Build();
            app.UseRouter(router);
        }
    }

    public class JsonRpcServerConfig
    {
        public int MaxRequestBodyLen = 100 * 1024 * 1024;
    }

    public class JsonHttpRpcListener : HttpServerImplementation
    {
        public JsonHttpRpcServer JsonServer { get; }

        public JsonHttpRpcListener(IConfiguration configuration) : base(configuration)
        {
            (JsonRpcServerConfig rpc_cfg, JsonRpcServerApi api) p = ((JsonRpcServerConfig rpc_cfg, JsonRpcServerApi api))this.Param;

            JsonServer = new JsonHttpRpcServer(p.api, p.rpc_cfg, this.CancelToken);
        }

        public static HttpServer<JsonHttpRpcListener> StartServer(HttpServerBuilderConfig http_cfg, JsonRpcServerConfig rpc_server_cfg, JsonRpcServerApi rpc_api)
            => new HttpServer<JsonHttpRpcListener>(http_cfg, (rpc_server_cfg, rpc_api));

        public override void SetupStartupConfig(HttpServerStartupConfig cfg, IApplicationBuilder app, IHostingEnvironment env)
            => this.JsonServer.RegisterToHttpServer(app);
    }

    public abstract class JsonRpcClient
    {
        List<(JsonRpcRequest request, JsonRpcResponse response, Type response_data_type)> call_queue = new List<(JsonRpcRequest request, JsonRpcResponse response, Type response_data_type)>();

        public void CallClear() => call_queue.Clear();

        public JsonRpcResponse<TResponse> CallAdd<TResponse>(string method, object param) where TResponse: class
        {
            var ret = new JsonRpcResponse<TResponse>();

            call_queue.Add((new JsonRpcRequest(method, param, Str.NewGuid()), ret, typeof(TResponse)));

            return ret;
        }

        public async Task CallAll(bool throw_each_error = false)
        {
            if (call_queue.Count == 0) return;

            try
            {
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
                    foreach (var r in ret_list)
                    {
                        r.CheckError();
                    }
            }
            finally
            {
                CallClear();
            }
        }

        public async Task<JsonRpcResponse<TResponse>> CallOne<TResponse>(string method, object param, bool throw_each_error = false) where TResponse : class
        {
            CallClear();
            try
            {
                JsonRpcResponse<TResponse> res = CallAdd<TResponse>(method, param);
                await CallAll(throw_each_error);
                return res;
            }
            finally
            {
                CallClear();
            }
        }

        public abstract Task<string> GetResponse(string req);

        class ProxyInterceptor : IInterceptor
        {
            public JsonRpcClient RpcClient { get; }

            public ProxyInterceptor(JsonRpcClient rpc_client)
            {
                this.RpcClient = rpc_client;
            }

            public void Intercept(IInvocation v)
            {
                JObject o = new JObject();
                var in_params = v.Method.GetParameters();
                if (v.Arguments.Length != in_params.Length) throw new ApplicationException("v.Arguments.Length != in_params.Length");
                for (int i = 0; i < in_params.Length; i++)
                {
                    var p = in_params[i];
                    o.Add(p.Name, JToken.FromObject(v.Arguments[i]));
                }
                Task<JsonRpcResponse<object>> call_ret = RpcClient.CallOne<object>(v.Method.Name, o, true);

                //ret.Wait();

                Dbg.WhereThread(v.Method.Name);
                Task<object> ret = get_response_object_async(call_ret);

                var return_type = v.Method.ReturnType;
                if (return_type.IsGenericType == false) throw new ApplicationException($"The return type of the method '{v.Method.Name}' is not a Task<>.");
                if (return_type.BaseType != typeof(Task)) throw new ApplicationException($"The return type of the method '{v.Method.Name}' is not a Task<>.");

                var generic_args = return_type.GetGenericArguments();
                if (generic_args.Length != 1) throw new ApplicationException($"The return type of the method '{v.Method.Name}' is not a Task<>.");
                var task_return_type = generic_args[0];

                v.ReturnValue = TaskUtil.ConvertTask(ret, typeof(object), task_return_type);
                Dbg.WhereThread(v.Method.Name);
            }

            async Task<object> get_response_object_async(Task<JsonRpcResponse<object>> o)
            {
                Dbg.WhereThread();
                await o;
                Dbg.WhereThread();
                return o.Result.ResultData;
            }
        }

        public virtual TRpcInterface GetRpcInterface<TRpcInterface>() where TRpcInterface : class
        {
            ProxyGenerator g = new ProxyGenerator();
            ProxyInterceptor ic = new ProxyInterceptor(this);

            return g.CreateInterfaceProxyWithoutTarget<TRpcInterface>(ic);
        }
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
