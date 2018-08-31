﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Text;
using System.ComponentModel;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Drawing;
using System.Runtime.InteropServices;

using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil.Basic
{
    internal class TaskVarObject
    {
        Dictionary<string, object> data = new Dictionary<string, object>();

        public object Get(Type type) => Get(type.AssemblyQualifiedName);
        public void Set(Type type, object obj) => Set(type.AssemblyQualifiedName, obj);

        public object Get(string key)
        {
            lock (data)
            {
                if (data.ContainsKey(key))
                    return data[key];
                else
                    return null;
            }
        }
        public void Set(string key, object obj)
        {
            lock (data)
            {
                if (obj != null)
                {
                    if (data.ContainsKey(key) == false)
                        data.Add(key, obj);
                    else
                        data[key] = obj;
                }
                else
                {
                    if (data.ContainsKey(key))
                        data.Remove(key);
                }
            }
        }
    }

    public static class TaskVar<T>
    {
        public static T Value { get => TaskVar.Get<T>(); set => TaskVar.Set<T>(value); }
    }

    public static class TaskVar
    {
        internal static AsyncLocal<TaskVarObject> async_local_obj = new AsyncLocal<TaskVarObject>();

        public static T Get<T>()
        {
            var v = async_local_obj.Value;
            if (v == null) return default(T);

            T ret = (T)v.Get(typeof(T));
            return ret;
        }
        public static void Set<T>(T obj)
        {
            if (async_local_obj.Value == null) async_local_obj.Value = new TaskVarObject();
            async_local_obj.Value.Set(typeof(T), obj);
        }

        public static object Get(string name) => async_local_obj.Value.Get(name);
        public static void Set(string name, object obj) => async_local_obj.Value.Set(name, obj);
    }

    public static class BackgroundWorker
    {
        static volatile int num_busy_worker_threads = 0;
        static volatile int num_worker_threads = 0;

        static Queue<Tuple<Action<object>, object>> queue = new Queue<Tuple<Action<object>, object>>();

        static AutoResetEvent signal = new AutoResetEvent(false);

        static void worker_thread_proc()
        {
            while (true)
            {
                Interlocked.Increment(ref num_busy_worker_threads);
                while (true)
                {
                    Tuple<Action<object>, object> work = null;
                    lock (queue)
                    {
                        if (queue.Count != 0)
                        {
                            work = queue.Dequeue();
                        }
                    }

                    if (work != null)
                    {
                        try
                        {
                            work.Item1(work.Item2);
                        }
                        catch (Exception ex)
                        {
                            Dbg.WriteLine(ex.ToString());
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                Interlocked.Decrement(ref num_busy_worker_threads);

                signal.WaitOne();
            }
        }

        public static void Run(Action<object> action, object arg)
        {
            if (num_busy_worker_threads == num_worker_threads)
            {
                Interlocked.Increment(ref num_worker_threads);
                Thread t = new Thread(worker_thread_proc);
                t.IsBackground = true;
                t.Start();
            }

            lock (queue)
            {
                queue.Enqueue(new Tuple<Action<object>, object>(action, arg));
            }

            signal.Set();
        }

    }

    public static class AsyncPreciseDelay
    {
        static SortedList<long, List<AsyncManualResetEvent>> wait_list = new SortedList<long, List<AsyncManualResetEvent>>();

        static Stopwatch w;

        static Thread background_thread;

        static AutoResetEvent ev = new AutoResetEvent(false);

        static List<Thread> worker_thread_list = new List<Thread>();

        static Queue<AsyncManualResetEvent> queued_tcs = new Queue<AsyncManualResetEvent>();

        static AutoResetEvent queued_tcs_signal = new AutoResetEvent(false);

        static AsyncPreciseDelay()
        {
            w = new Stopwatch();
            w.Start();

            background_thread = new Thread(background_thread_proc);
            background_thread.IsBackground = true;
            background_thread.Start();
        }

        static volatile int num_busy_worker_threads = 0;
        static volatile int num_worker_threads = 0;

        static void worker_thread_proc()
        {
            while (true)
            {
                Interlocked.Increment(ref num_busy_worker_threads);
                while (true)
                {
                    AsyncManualResetEvent tcs = null;
                    lock (queued_tcs)
                    {
                        if (queued_tcs.Count != 0)
                        {
                            tcs = queued_tcs.Dequeue();
                        }
                    }

                    if (tcs != null)
                    {
                        tcs.Set();
                    }
                    else
                    {
                        break;
                    }
                }
                Interlocked.Decrement(ref num_busy_worker_threads);

                queued_tcs_signal.WaitOne();
            }
        }

        static void FireWorkerThread(AsyncManualResetEvent tc)
        {
            if (num_busy_worker_threads == num_worker_threads)
            {
                Interlocked.Increment(ref num_worker_threads);
                Thread t = new Thread(worker_thread_proc);
                t.IsBackground = true;
                t.Start();
                //Console.WriteLine($"num_worker_threads = {num_worker_threads}");
            }

            lock (queued_tcs)
            {
                queued_tcs.Enqueue(tc);
            }
            queued_tcs_signal.Set();
        }

        static void background_thread_proc()
        {
            //Benchmark b1 = new Benchmark("num_fired");
            //Benchmark b2 = new Benchmark("num_loop");
            //Benchmark b3 = new Benchmark("num_removed");
            while (true)
            {
                long now = Tick;
                long next_wait_target = -1;

                List<AsyncManualResetEvent> fire_event_list = new List<AsyncManualResetEvent>();

                lock (wait_list)
                {
                    List<long> past_target_list = new List<long>();
                    List<long> future_target_list = new List<long>();

                    foreach (long target in wait_list.Keys)
                    {
                        if (now >= target)
                        {
                            past_target_list.Add(target);
                            next_wait_target = 0;
                        }
                        else
                        {
                            future_target_list.Add(target);
                        }
                    }

                    foreach (long target in past_target_list)
                    {
                        List<AsyncManualResetEvent> event_list = wait_list[target];

                        wait_list.Remove(target);

                        foreach (AsyncManualResetEvent e in event_list)
                        {
                            if (e.IsAbandoned == false)
                                fire_event_list.Add(e);
                            else
                            {
                                //b3.IncrementMe++;
                            }
                        }
                    }

                    foreach (long target in future_target_list)
                    {
                        List<AsyncManualResetEvent> event_list = wait_list[target];

                        List<AsyncManualResetEvent> remove_list = new List<AsyncManualResetEvent>();

                        foreach (AsyncManualResetEvent e in event_list)
                        {
                            if (e.IsAbandoned)
                            {
                                remove_list.Add(e);
                                //b3.IncrementMe++;
                                //Dbg.Where();
                            }
                        }

                        foreach (AsyncManualResetEvent e in remove_list)
                        {
                            event_list.Remove(e);
                        }

                        if (event_list.Count == 0)
                        {
                            wait_list.Remove(target);
                        }
                    }

                    if (next_wait_target == -1)
                    {
                        if (wait_list.Count >= 1)
                        {
                            next_wait_target = wait_list.Keys[0];
                        }
                    }
                }

                int n = 0;
                foreach (AsyncManualResetEvent tc in fire_event_list)
                {
                    //tc.TrySetResult(0);
                    //Task.Factory.StartNew(() => tc.TrySetResult(0));
                    FireWorkerThread(tc);
                    n++;
                    //b1.IncrementMe++;
                }
                //n.Print();
                //b2.IncrementMe++;

                now = Tick;
                long next_wait_tick = (Math.Max(next_wait_target - now, 0));
                if (next_wait_target == -1)
                {
                    next_wait_tick = -1;
                }
                if (next_wait_tick >= 1 || next_wait_tick == -1)
                {
                    if (next_wait_tick == -1 || next_wait_tick >= 100)
                    {
                        next_wait_tick = 100;
                    }
                    ev.WaitOne((int)next_wait_tick);
                }
            }
        }

        public static long Tick
        {
            get
            {
                lock (w)
                {
                    return w.ElapsedMilliseconds + 1L;
                }
            }
        }

        public static Task PreciseDelay(int msec)
        {
            if (msec == Timeout.Infinite)
            {
                return Task.Delay(Timeout.Infinite);
            }
            if (msec <= 0)
            {
                return Task.CompletedTask;
            }

            long target_time = Tick + (long)msec;

            AsyncManualResetEvent tc = new AsyncManualResetEvent();
            Task ret = tc.WaitAsync();

            List<AsyncManualResetEvent> o;

            bool set_event = false;

            lock (wait_list)
            {
                long first_target_before = -1;
                long first_target_after = -1;

                if (wait_list.Count >= 1)
                {
                    first_target_before = wait_list.Keys[0];
                }

                if (wait_list.ContainsKey(target_time) == false)
                {
                    o = new List<AsyncManualResetEvent>();
                    wait_list.Add(target_time, o);
                }
                else
                {
                    o = wait_list[target_time];
                }

                o.Add(tc);

                first_target_after = wait_list.Keys[0];

                if (first_target_before != first_target_after)
                {
                    set_event = true;
                }
            }

            if (set_event)
            {
                ev.Set();
            }

            return ret;
        }
    }
    /*
    public class AsyncAutoResetEvent : AsyncEvent
    {
        volatile Queue<TaskCompletionSource<object>> waiters = new Queue<TaskCompletionSource<object>>();
        volatile bool set;

        public override Task Wait()
        {
            lock (waiters)
            {
                var tcs = new TaskCompletionSource<object>();
                if (waiters.Count > 0 || !set)
                {
                    waiters.Enqueue(tcs);
                }
                else
                {
                    //tcs.SetCanceled();
                    tcs.SetResult(null);
                    set = false;
                }
                return tcs.Task;
            }
        }

        public override void Set()
        {
            TaskCompletionSource<object> toSet = null;
            lock (waiters)
            {
                if (waiters.Count > 0) toSet = waiters.Dequeue();
                else set = true;
            }
            if (toSet != null)
            {
                toSet.SetResult(null);
                //toSet.SetCanceled();
            }
        }
    }*/

    public class AsyncAutoResetEvent
    {
        object lockobj = new object();
        Queue<AsyncManualResetEvent> event_queue = new Queue<AsyncManualResetEvent>();
        bool is_set = false;

        public Task WaitOneAsync()
        {
            lock (lockobj)
            {
                if (is_set)
                {
                    is_set = false;
                    return Task.CompletedTask;
                }

                AsyncManualResetEvent e = new AsyncManualResetEvent();

                Task ret = e.WaitAsync();

                event_queue.Enqueue(e);

                return ret;
            }
        }

        public void Set()
        {
            AsyncManualResetEvent ev = null;
            lock (lockobj)
            {
                while (event_queue.Count >= 1)
                {
                    AsyncManualResetEvent e = event_queue.Dequeue();
                    if (e.IsAbandoned == false)
                    {
                        ev = e;
                    }
                }

                if (ev == null)
                {
                    is_set = true;
                }
            }

            if (ev != null)
            {
                ev.Set();
            }
        }
    }

    public class AsyncManualResetEvent
    {
        object lockobj = new object();
        volatile TaskCompletionSource<bool> tcs;
        bool is_set = false;
        WeakReference<Task> weak_task = null;

        public AsyncManualResetEvent()
        {
            init();
        }

        void init()
        {
            this.tcs = new TaskCompletionSource<bool>();
            weak_task = null;
        }

        public bool IsSet
        {
            get
            {
                lock (lockobj)
                {
                    return this.is_set;
                }
            }
        }

        public bool IsAbandoned
        {
            get
            {
                Task ret = null;
                if (weak_task == null || weak_task.TryGetTarget(out ret) == false)
                {
                    return true;
                }
                return false;
            }
        }

        public Task WaitAsync()
        {
            lock (lockobj)
            {
                if (is_set)
                {
                    return Task.CompletedTask;
                }
                else
                {
                    Task ret = null;
                    if (weak_task == null || weak_task.TryGetTarget(out ret) == false)
                    {
                        ret = TaskUtil.CreateWeakTaskFromTask(tcs.Task);
                        weak_task = new WeakReference<Task>(ret);
                    }
                    return ret;
                }
            }
        }

        public void Set()
        {
            lock (lockobj)
            {
                if (is_set == false)
                {
                    is_set = true;
                    tcs.TrySetResult(true);
                }
            }
        }

        public void Reset()
        {
            lock (lockobj)
            {
                if (is_set)
                {
                    is_set = false;
                    init();
                }
            }
        }
    }

    public static class TaskUtil
    {
        public static void Test()
        {
            Dbg.WhereThread();
            //Task<string> t = (Task<string>)ConvertTask(f1(), typeof(object), typeof(string));
            Task<object> t = (Task<object>)ConvertTask(f2(), typeof(string), typeof(object));
            Dbg.WhereThread();
            t.Result.Print();
            Dbg.WhereThread();
        }

        static async Task<object> f1()
        {
            Dbg.WhereThread();
            await Task.Delay(100);
            Dbg.WhereThread();
            return "Hello";
        }

        static async Task<string> f2()
        {
            Dbg.WhereThread();
            await Task.Delay(100);
            Dbg.WhereThread();
            return "Hello";
        }

        public static object ConvertTask(object src_task_object, Type old_task_type, Type new_task_type)
        {
            Type src_task_def = typeof(Task<>).MakeGenericType(old_task_type);

            var cont_with_methods = src_task_def.GetMethods();
            MethodInfo cont_with = null;
            int num = 0;
            foreach (var m in cont_with_methods)
            {
                if (m.Name == "ContinueWith" && m.ContainsGenericParameters)
                {
                    var pinfos = m.GetParameters();
                    if (pinfos.Length == 1)
                    {
                        var pinfo = pinfos[0];
                        var ptype = pinfo.ParameterType;
                        var generic_args = ptype.GenericTypeArguments;
                        if (generic_args.Length == 2)
                        {
                            if (generic_args[0].IsGenericType)
                            {
                                if (generic_args[1].IsGenericParameter)
                                {
                                    if (generic_args[0].BaseType == typeof(Task))
                                    {
                                        cont_with = m;
                                        num++;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (num != 1) throw new ApplicationException("ConvertTask: num != 1");

            object ret = null;

            var cont_with_generic = cont_with.MakeGenericMethod(new_task_type);

            var convert_task_proc_method = typeof(TaskUtil).GetMethod(nameof(convert_task_proc), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(new_task_type);

            var func_type = typeof(Func<,>).MakeGenericType(typeof(Task<>).MakeGenericType(old_task_type), new_task_type);

            Delegate delegate_instance = convert_task_proc_method.CreateDelegate(func_type);

            ret = cont_with_generic.Invoke(src_task_object, new object[] { delegate_instance });

            return ret;
        }

        static TNewResult convert_task_proc<TNewResult>(object t)
        {
            Type old_task_type = t.GetType();
            object result_old = old_task_type.GetProperty("Result").GetValue(t);
            TNewResult result_new = Json.ConvertObject<TNewResult>(result_old);
            return result_new;
        }
        
        class weak_task_param
        {
            public WeakReference<TaskCompletionSource<bool>> tcs_weak;
            public bool is_completed = false;
            public object LockObj = new object();
        }

        static void weak_task_proc(Task t, object param)
        {
            try
            {
                weak_task_param p = (weak_task_param)param;

                //Dbg.Where();

                lock (p.LockObj)
                {
                    if (p.tcs_weak.TryGetTarget(out TaskCompletionSource<bool> tcs))
                    {
                        if (p.is_completed == false)
                        {
                            p.is_completed = true;
                            tcs.TrySetResult(true);
                        }
                    }
                    else
                    {
                        Dbg.Where();
                    }
                }
            }
            catch (Exception ex)
            {
                ex.ToString().Debug();
            }
        }

        public static Task CreateWeakTaskFromTask(Task t)
        {
            Ref<object> avoid_gc_ref = new Ref<object>();
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(avoid_gc_ref);
            avoid_gc_ref.Set(tcs);
            weak_task_param p = new weak_task_param()
            {
                tcs_weak = new WeakReference<TaskCompletionSource<bool>>(tcs),
            };
            t.ContinueWith(weak_task_proc, p, TaskContinuationOptions.ExecuteSynchronously);
            Task ret = tcs.Task;
            return ret;
        }

        public static Task PreciseDelay(int msec)
        {
            return AsyncPreciseDelay.PreciseDelay(msec);
        }

        /*public static Task<bool> Sleep(int msec, CancellationToken cancel)
        {
        }*/

        public static Task WhenCanceledOrTimeouted(CancellationToken cancel, int timeout)
        {
            if (timeout == 0)
            {
                return Task.CompletedTask;
            }

            return Task.WhenAny(WhenCanceled(cancel), Task.Delay(timeout));
        }

        public static Task WhenCanceled(CancellationToken cancel)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            cancel.Register((s) => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);

            return tcs.Task;
        }

        public static TaskVm<TResult, TIn> GetCurrentTaskVm<TResult, TIn>()
        {
            TaskVm<TResult, TIn>.TaskVmSynchronizationContext ctx = (TaskVm<TResult, TIn>.TaskVmSynchronizationContext)SynchronizationContext.Current;
            
            return ctx.Vm;
        }

        public static async Task CancelAsync(CancellationTokenSource cts, bool throwOnFirstException = false)
        {
            await Task.Run(() => cts.Cancel(throwOnFirstException));
        }

        public static async Task TryCancelAsync(CancellationTokenSource cts)
        {
            await Task.Run(() => TryCancel(cts));
        }

        public static void TryCancel(CancellationTokenSource cts)
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
            }
        }

        public static void TryCancelNoBlock(CancellationTokenSource cts)
        {
            BackgroundWorker.Run(arg =>
            {
                cts.TryCancel();
            }, null);
        }

        // いずれかの CancellationToken がキャンセルされたときにキャンセルされる CancellationToken を作成する
        public static CancellationToken CombineCancellationTokens(bool no_wait, params CancellationToken[] tokens)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            ChainCancellationTokensToCancellationTokenSource(cts, no_wait, tokens);
            return cts.Token;
        }

        // いずれかの CancellationToken がキャンセルされたときに CancellationTokenSource をキャンセルするように設定する
        public static void ChainCancellationTokensToCancellationTokenSource(CancellationTokenSource cts, bool no_wait, params CancellationToken[] tokens)
        {
            foreach (CancellationToken t in tokens)
            {
                t.Register(() =>
                {
                    if (no_wait == false)
                        cts.TryCancel();
                    else
                        cts.TryCancelNoBlock();
                });
            }
        }

        public static CancellationToken CurrentTaskVmGracefulCancel => (CancellationToken)ThreadData.CurrentThreadData.DataList["taskvm_current_graceful_cancel"];
    }

    public class TaskVmAbortException : Exception
    {
        public TaskVmAbortException(string message) : base(message) { }
    }

    static class AbortedTaskExecuteThreadPrivate
    {
        static object LockObj = new object();
        static Dictionary<object, Queue<(SendOrPostCallback callback, object args)>> dispatch_queue_list = new Dictionary<object, Queue<(SendOrPostCallback, object)>>();
        static object dummy_orphants = new object();
        static AutoResetEvent ev = new AutoResetEvent(true);

        static AbortedTaskExecuteThreadPrivate()
        {
            Thread t = new Thread(thread_proc);
            t.IsBackground = true;
            t.Start();
        }

        static void thread_proc(object param)
        {
            SynchronizationContext.SetSynchronizationContext(new AbortedTaskExecuteThreadSynchronizationContext());

            while (true)
            {
                var actions = new List<(SendOrPostCallback callback, object args)>();

                lock (LockObj)
                {
                    foreach (object ctx in dispatch_queue_list.Keys)
                    {
                        var queue = dispatch_queue_list[ctx];

                        while (queue.Count >= 1)
                        {
                            actions.Add(queue.Dequeue());
                        }
                    }
                }

                foreach (var action in actions)
                {
                    try
                    {
                        //Dbg.WriteCurrentThreadId("aborted_call");
                        action.callback(action.args);
                    }
                    catch (Exception ex)
                    {
                        ex.ToString().Debug();
                    }
                }

                ev.WaitOne();
            }
        }

        public static void PostAction(object ctx, SendOrPostCallback callback, object arg)
        {
            lock (LockObj)
            {
                if (dispatch_queue_list.ContainsKey(ctx) == false)
                {
                    dispatch_queue_list.Add(ctx, new Queue<(SendOrPostCallback, object)>());
                }

                dispatch_queue_list[ctx].Enqueue((callback, arg));
            }

            ev.Set();
        }

        public static void RemoveContext(object ctx)
        {
            lock (LockObj)
            {
                if (dispatch_queue_list.ContainsKey(ctx))
                {
                    var queue = dispatch_queue_list[ctx];

                    while (queue.Count >= 1)
                    {
                        var q = queue.Dequeue();
                        PostAction(dummy_orphants, q.callback, q.args);
                    }

                    dispatch_queue_list.Remove(ctx);
                }
            }

            ev.Set();
        }

        class AbortedTaskExecuteThreadSynchronizationContext : SynchronizationContext
        {
            volatile int num_operations = 0;
            volatile int num_operations_total = 0;

            public bool IsAllOperationsCompleted => (num_operations_total >= 1 && num_operations == 0);

            public override void Post(SendOrPostCallback d, object state)
            {
                //Dbg.WriteCurrentThreadId("aborted_call_post");
                AbortedTaskExecuteThreadPrivate.PostAction(AbortedTaskExecuteThreadPrivate.dummy_orphants, d, state);
            }
        }
    }

    public class TaskVm<TResult, TIn>
    {
        readonly ThreadObj thread;
        public ThreadObj ThreadObj => this.thread;

        Func<TIn, Task<TResult>> root_function;
        Task root_task;

        Queue<(SendOrPostCallback callback, object args)> dispatch_queue = new Queue<(SendOrPostCallback callback, object args)>();
        AutoResetEvent dispatch_queue_event = new AutoResetEvent(false);

        public TIn InputParameter { get; }

        CancellationToken GracefulCancel { get; }
        CancellationToken AbortCancel { get; }

        object ResultLock = new object();
        public Exception Error { get; private set; } = null;
        public TResult Result => this.GetResult(out _);
        TResult result = default(TResult);
        public bool IsCompleted { get; private set; } = false;
        public bool IsAborted { get; private set; } = false;
        public bool HasError => this.Error != null;
        public bool Ok => !HasError;

        public ManualResetEventSlim CompletedEvent { get; } = new ManualResetEventSlim();

        bool abort_flag = false;
        bool no_more_enqueue = false;

        TaskVmSynchronizationContext sync_ctx;

        public TaskVm(Func<TIn, Task<TResult>> root_action, TIn input_parameter = default(TIn), CancellationToken graceful_cancel = default(CancellationToken), CancellationToken abort_cancel = default(CancellationToken))
        {
            this.InputParameter = input_parameter;
            this.root_function = root_action;
            this.GracefulCancel = graceful_cancel;
            this.AbortCancel = abort_cancel;

            this.AbortCancel.Register(() =>
            {
                Abort(true);
            });

            this.thread = new ThreadObj(this.thread_proc);
            this.thread.WaitForInit();
        }

        public static Task<TResult> NewTaskVm(Func<TIn, Task<TResult>> root_action, TIn input_parameter = default(TIn), CancellationToken graceful_cancel = default(CancellationToken), CancellationToken abort_cancel = default(CancellationToken))
        {
            TaskVm<TResult, TIn> vm = new TaskVm<TResult, TIn>(root_action, input_parameter, graceful_cancel, abort_cancel);

            return Task<TResult>.Run(new Func<TResult>(vm.get_result_simple));
        }

        TResult get_result_simple()
        {
            return this.GetResult();
        }

        public bool Abort(bool no_wait = false)
        {
            this.abort_flag = true;

            this.dispatch_queue_event.Set();

            if (no_wait)
            {
                return this.IsAborted;
            }

            this.thread.WaitForEnd();

            return this.IsAborted;
        }

        public bool Wait(bool ignore_error = false, int timeout = Timeout.Infinite, CancellationToken cancel = default(CancellationToken))
        {
            TResult ret = GetResult(out bool timeouted, ignore_error, timeout, cancel);

            return !timeouted;
        }

        public TResult GetResult(bool ignore_error = false, int timeout = Timeout.Infinite, CancellationToken cancel = default(CancellationToken)) => GetResult(out _, ignore_error, timeout, cancel);
        public TResult GetResult(out bool timeouted, bool ignore_error = false, int timeout = Timeout.Infinite, CancellationToken cancel = default(CancellationToken))
        {
            CompletedEvent.Wait(timeout, cancel);

            if (this.IsCompleted == false)
            {
                timeouted = true;
                return default(TResult);
            }

            timeouted = false;

            if (this.Error != null)
            {
                if (ignore_error == false)
                {
                    throw this.Error;
                }
                else
                {
                    return default(TResult);
                }
            }

            return this.result;
        }

        void thread_proc(object param)
        {
            sync_ctx = new TaskVmSynchronizationContext(this);
            SynchronizationContext.SetSynchronizationContext(sync_ctx);

            ThreadData.CurrentThreadData.DataList["taskvm_current_graceful_cancel"] = this.GracefulCancel;

            //Dbg.WriteCurrentThreadId("before task_proc()");

            this.root_task = task_proc();

            dispatcher_loop();
        }

        void set_result(Exception ex = null, TResult result = default(TResult))
        {
            lock (this.ResultLock)
            {
                if (this.IsCompleted == false)
                {
                    this.IsCompleted = true;

                    if (ex == null)
                    {
                        this.result = result;
                    }
                    else
                    {
                        this.Error = ex;

                        if (ex is TaskVmAbortException)
                        {
                            this.IsAborted = true;
                        }
                    }

                    this.dispatch_queue_event.Set();
                    this.CompletedEvent.Set();
                }
            }
        }

        async Task task_proc()
        {
            //Dbg.WriteCurrentThreadId("task_proc: before yield");

            await Task.Yield();
            
            //Dbg.WriteCurrentThreadId("task_proc: before await");

            TResult ret = default(TResult);

            try
            {
                ret = await this.root_function(this.InputParameter);

                //Dbg.WriteCurrentThreadId("task_proc: after await");
                set_result(null, ret);
            }
            catch (Exception ex)
            {
                set_result(ex);
            }
        }

        void dispatcher_loop()
        {
            int num_executed_tasks = 0;

            while (this.IsCompleted == false)
            {
                this.dispatch_queue_event.WaitOne();

                if (this.abort_flag)
                {
                    set_result(new TaskVmAbortException("aborted."));
                }

                while (true)
                {
                    (SendOrPostCallback callback, object args) queued_item;

                    lock (this.dispatch_queue)
                    {
                        if (this.dispatch_queue.Count == 0)
                        {
                            break;
                        }
                        queued_item = this.dispatch_queue.Dequeue();
                    }

                    if (num_executed_tasks == 0)
                    {
                        ThreadObj.NoticeInited();
                    }
                    num_executed_tasks++;

                    if (this.abort_flag)
                    {
                        set_result(new TaskVmAbortException("aborted."));
                    }

                    try
                    {
                        queued_item.callback(queued_item.args);
                    }
                    catch (Exception ex)
                    {
                        ex.ToString().Debug();
                    }
                }
            }

            no_more_enqueue = true;

            List<(SendOrPostCallback callback, object args)> remaining_tasks = new List<(SendOrPostCallback callback, object args)>();
            lock (this.dispatch_queue)
            {
                while (true)
                {
                    if (this.dispatch_queue.Count == 0)
                    {
                        break;
                    }
                    remaining_tasks.Add(this.dispatch_queue.Dequeue());
                }
                foreach (var x in remaining_tasks)
                {
                    AbortedTaskExecuteThreadPrivate.PostAction(this.sync_ctx, x.callback, x.args);
                }
            }
        }

        public class TaskVmSynchronizationContext : SynchronizationContext
        {
            public readonly TaskVm<TResult, TIn> Vm;
            volatile int num_operations = 0;
            volatile int num_operations_total = 0;


            public bool IsAllOperationsCompleted => (num_operations_total >= 1 && num_operations == 0);

            public TaskVmSynchronizationContext(TaskVm<TResult, TIn> vm)
            {
                this.Vm = vm;
            }

            public override SynchronizationContext CreateCopy()
            {
                //Dbg.WriteCurrentThreadId("CreateCopy");
                return base.CreateCopy();
            }

            public override void OperationCompleted()
            {
                base.OperationCompleted();

                Interlocked.Decrement(ref num_operations);
                //Dbg.WriteCurrentThreadId("OperationCompleted. num_operations = " + num_operations);
                Vm.dispatch_queue_event.Set();
            }

            public override void OperationStarted()
            {
                base.OperationStarted();

                Interlocked.Increment(ref num_operations);
                Interlocked.Increment(ref num_operations_total);
                //Dbg.WriteCurrentThreadId("OperationStarted. num_operations = " + num_operations);
                Vm.dispatch_queue_event.Set();
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                //Dbg.WriteCurrentThreadId("Post: " + this.Vm.halt);
                //base.Post(d, state);
                //d(state);

                bool ok = false;

                lock (Vm.dispatch_queue)
                {
                    if (Vm.no_more_enqueue == false)
                    {
                        Vm.dispatch_queue.Enqueue((d, state));
                        ok = true;
                    }
                }

                if (ok)
                {
                    Vm.dispatch_queue_event.Set();
                }
                else
                {
                    AbortedTaskExecuteThreadPrivate.PostAction(this, d, state);
                }
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                //Dbg.WriteCurrentThreadId("Send");
                base.Send(d, state);
            }

            public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
            {
                //Dbg.WriteCurrentThreadId("Wait");
                return base.Wait(waitHandles, waitAll, millisecondsTimeout);
            }
        }
    }
}

