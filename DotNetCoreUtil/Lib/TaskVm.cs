using System;
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
using System.Drawing;
using System.Runtime.InteropServices;

using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil.Lib
{
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

    public static class AsyncWaiter
    {
        static SortedList<long, List<TaskCompletionSource<int>>> wait_list = new SortedList<long, List<TaskCompletionSource<int>>>();

        static Stopwatch w;

        static Thread background_thread;

        static AutoResetEvent ev = new AutoResetEvent(false);

        static List<Thread> worker_thread_list = new List<Thread>();

        static Queue<TaskCompletionSource<int>> queued_tcs = new Queue<TaskCompletionSource<int>>();

        static AutoResetEvent queued_tcs_signal = new AutoResetEvent(false);

        static AsyncWaiter()
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
                    TaskCompletionSource<int> tcs = null;
                    lock (queued_tcs)
                    {
                        if (queued_tcs.Count != 0)
                        {
                            tcs = queued_tcs.Dequeue();
                        }
                    }

                    if (tcs != null)
                    {
                        tcs.TrySetResult(0);
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

        static void FireWorkerThread(TaskCompletionSource<int> tc)
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
            while (true)
            {
                long now = Tick;
                long next_wait_target = -1;

                List<TaskCompletionSource<int>> tc_list = new List<TaskCompletionSource<int>>();

                lock (wait_list)
                {
                    List<long> past_target_list = new List<long>();

                    foreach (long target in wait_list.Keys)
                    {
                        if (now >= target)
                        {
                            past_target_list.Add(target);
                            next_wait_target = 0;
                        }
                        else
                        {
                            break;
                        }
                    }

                    foreach (long target in past_target_list)
                    {
                        List<TaskCompletionSource<int>> tcl = wait_list[target];

                        wait_list.Remove(target);

                        foreach (TaskCompletionSource<int> tc in tcl)
                        {
                            tc_list.Add(tc);
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

                foreach (TaskCompletionSource<int> tc in tc_list)
                {
                    //tc.TrySetResult(0);
                    //Task.Factory.StartNew(() => tc.TrySetResult(0));
                    FireWorkerThread(tc);
                }

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
                        //next_wait_tick = 100;
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

        public static Task Sleep(long msec)
        {
            if (msec <= 0)
            {
                return Task.CompletedTask;
            }

            long target_time = Tick + msec;

            TaskCompletionSource<int> tc = new TaskCompletionSource<int>();
            List<TaskCompletionSource<int>> o;

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
                    o = new List<TaskCompletionSource<int>>();
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

            return tc.Task;
        }
    }

    public abstract class AsyncEvent
    {
        public abstract Task Wait();
        public abstract void Set();
    }

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
    }

    public class AsyncManualResetEvent : AsyncEvent
    {
        volatile TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

        public override Task Wait()
        {
            return tcs.Task;
        }

        public override void Set()
        {
            tcs.TrySetResult(true);
        }
    }

    public static class TaskUtil
    {
        public static Task Sleep(long msec)
        {
            return AsyncWaiter.Sleep(msec);
        }

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
                cts.Cancel();
            }, null);
        }

        public static CancellationToken CombineCancellationTokens(params CancellationToken[] tokens)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            foreach (CancellationToken t in tokens)
            {
                t.Register(() =>
                {
                    cts.Cancel(false);
                });
            }

            return cts.Token;
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
        static Dictionary<object, Queue<ValueTuple<SendOrPostCallback, object>>> dispatch_queue_list = new Dictionary<object, Queue<(SendOrPostCallback, object)>>();
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
                List<ValueTuple<SendOrPostCallback, object>> actions = new List<(SendOrPostCallback, object)>();

                lock (LockObj)
                {
                    foreach (object ctx in dispatch_queue_list.Keys)
                    {
                        Queue<ValueTuple<SendOrPostCallback, object>> queue = dispatch_queue_list[ctx];

                        while (queue.Count >= 1)
                        {
                            actions.Add(queue.Dequeue());
                        }
                    }
                }

                foreach (ValueTuple<SendOrPostCallback, object> action in actions)
                {
                    try
                    {
                        //Dbg.WriteCurrentThreadId("aborted_call");
                        action.Item1(action.Item2);
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
                    Queue<ValueTuple<SendOrPostCallback, object>> queue = dispatch_queue_list[ctx];

                    while (queue.Count >= 1)
                    {
                        var q = queue.Dequeue();
                        PostAction(dummy_orphants, q.Item1, q.Item2);
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

        Queue<ValueTuple<SendOrPostCallback, object>> dispatch_queue = new Queue<ValueTuple<SendOrPostCallback, object>>();
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

        public static Task<TResult> NewTask(Func<TIn, Task<TResult>> root_action, TIn input_parameter = default(TIn), CancellationToken graceful_cancel = default(CancellationToken), CancellationToken abort_cancel = default(CancellationToken))
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
                    ValueTuple<SendOrPostCallback, object> queued_item;

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
                        queued_item.Item1(queued_item.Item2);
                    }
                    catch (Exception ex)
                    {
                        ex.ToString().Debug();
                    }
                }
            }

            no_more_enqueue = true;

            List<ValueTuple<SendOrPostCallback, object>> remaining_tasks = new List<(SendOrPostCallback, object)>();
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
                    AbortedTaskExecuteThreadPrivate.PostAction(this.sync_ctx, x.Item1, x.Item2);
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

