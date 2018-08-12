using System;
using System.Threading;
using System.Threading.Tasks;
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
                Dbg.WriteCurrentThreadId("CreateCopy");
                return base.CreateCopy();
            }

            public override void OperationCompleted()
            {
                Dbg.WriteCurrentThreadId("OperationCompleted");
                base.OperationCompleted();
            }

            public override void OperationStarted()
            {
                Dbg.WriteCurrentThreadId("OperationStarted");
                base.OperationStarted();
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                Dbg.WriteCurrentThreadId("Post");
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
                Dbg.WriteCurrentThreadId("Send");
                base.Send(d, state);
            }

            public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
            {
                Dbg.WriteCurrentThreadId("Wait");
                return base.Wait(waitHandles, waitAll, millisecondsTimeout);
            }
        }
    }
}

