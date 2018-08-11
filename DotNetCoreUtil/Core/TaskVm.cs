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
    public class TaskVm
    {
        readonly ThreadObj thread;
        public ThreadObj ThreadObj => this.thread;

        Func<Task<object>> root_function;
        Task root_task;

        Queue<Tuple<SendOrPostCallback, object>> dispatch_queue = new Queue<Tuple<SendOrPostCallback, object>>();
        AutoResetEvent dispatch_queue_event = new AutoResetEvent(false);

        bool halt = false;

        TaskVmSynchronizationContext sync_ctx;

        void thread_proc(object param)
        {
            sync_ctx = new TaskVmSynchronizationContext(this);
            SynchronizationContext.SetSynchronizationContext(sync_ctx);

            Dbg.WriteCurrentThreadId("before task_proc()");

            this.root_task = task_proc();

            ThreadObj.NoticeInited();

            dispatcher_loop();
        }

        async Task task_proc()
        {
            Dbg.WriteCurrentThreadId("task_proc: before await");

            await this.root_function();

            Dbg.WriteCurrentThreadId("task_proc: after await");
            halt = true;
            this.dispatch_queue_event.Set();
        }

        void dispatcher_loop()
        {
            while (halt == false)
            {
                this.dispatch_queue_event.WaitOne();

                while (true)
                {
                    Tuple<SendOrPostCallback, object> queued_item = null;

                    lock (this.dispatch_queue)
                    {
                        if (this.dispatch_queue.Count >= 1)
                        {
                            queued_item = this.dispatch_queue.Dequeue();
                        }
                    }

                    if (queued_item == null)
                    {
                        break;
                    }

                    queued_item.Item1(queued_item.Item2);
                }
            }
        }

        public TaskVm(Func<Task<object>> root_action, object param)
        {
            this.root_function = root_action;
            this.thread = new ThreadObj(this.thread_proc);
            this.thread.WaitForInit();
        }

        public class TaskVmSynchronizationContext : SynchronizationContext
        {
            public readonly TaskVm Vm;

            public TaskVmSynchronizationContext(TaskVm vm)
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
                    Vm.dispatch_queue.Enqueue(new Tuple<SendOrPostCallback, object>(d, state));
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

