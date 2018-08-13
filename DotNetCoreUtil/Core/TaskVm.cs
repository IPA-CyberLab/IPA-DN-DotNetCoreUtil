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

namespace IPA.DN.CoreUtil
{
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
    }

    public class TaskVmAbortException : Exception
    {
        public TaskVmAbortException(string message) : base(message) { }
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

        bool halt = false;

        CancellationToken cancel_token;

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

        TaskVmSynchronizationContext sync_ctx;

        public TaskVm(Func<TIn, Task<TResult>> root_action, TIn input_parameter = default(TIn), CancellationToken cancel = default(CancellationToken))
        {
            this.InputParameter = input_parameter;
            this.root_function = root_action;
            this.cancel_token = cancel;
            this.cancel_token.Register(() =>
            {
                this.Abort();
            });

            this.thread = new ThreadObj(this.thread_proc);
            this.thread.WaitForInit();
        }

        public static Task<TResult> NewTask(Func<TIn, Task<TResult>> root_action, TIn input_parameter = default(TIn), CancellationToken cancel = default(CancellationToken))
        {
            TaskVm<TResult, TIn> vm = new TaskVm<TResult, TIn>(root_action, input_parameter, cancel);

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

            Dbg.WriteCurrentThreadId("before task_proc()");

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

                    this.halt = true;
                    this.dispatch_queue_event.Set();
                    this.CompletedEvent.Set();
                }
            }
        }

        async Task task_proc()
        {
            Dbg.WriteCurrentThreadId("task_proc: before yield");

            await Task.Yield();
            
            Dbg.WriteCurrentThreadId("task_proc: before await");

            TResult ret = default(TResult);

            try
            {
                ret = await this.root_function(this.InputParameter);

                Dbg.WriteCurrentThreadId("task_proc: after await");
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

            while (halt == false)
            {
                this.dispatch_queue_event.WaitOne();

                if (this.abort_flag)
                {
                    set_result(new TaskVmAbortException("aborted."));
                    break;
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
                        break;
                    }

                    try
                    {
                        queued_item.Item1(queued_item.Item2);
                    }
                    catch (Exception ex)
                    {
                        set_result(ex);
                        break;
                    }
                }
            }
        }

        public class TaskVmSynchronizationContext : SynchronizationContext
        {
            public readonly TaskVm<TResult, TIn> Vm;

            public TaskVmSynchronizationContext(TaskVm<TResult, TIn> vm)
            {
                this.Vm = vm;
            }

            public override SynchronizationContext CreateCopy()
            {
                // Dbg.WriteCurrentThreadId("CreateCopy");
                return base.CreateCopy();
            }

            public override void OperationCompleted()
            {
                // Dbg.WriteCurrentThreadId("OperationCompleted");
                base.OperationCompleted();
            }

            public override void OperationStarted()
            {
                // Dbg.WriteCurrentThreadId("OperationStarted");
                base.OperationStarted();
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                //Dbg.WriteCurrentThreadId("Post");
                //base.Post(d, state);
                //d(state);
                lock (Vm.dispatch_queue)
                {
                    Vm.dispatch_queue.Enqueue((d, state));
                }

                Vm.dispatch_queue_event.Set();
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                //Dbg.WriteCurrentThreadId("Send");
                base.Send(d, state);
            }

            public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
            {
                // Dbg.WriteCurrentThreadId("Wait");
                return base.Wait(waitHandles, waitAll, millisecondsTimeout);
            }
        }
    }
}

