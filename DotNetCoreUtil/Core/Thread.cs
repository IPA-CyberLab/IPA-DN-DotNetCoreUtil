using System;
using System.Threading;
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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Net.Mail;
using System.Net.Mime;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

#pragma warning disable 0618

namespace IPA.DN.CoreUtil
{
    public class GlobalLockHandle : IDisposable
    {
        public GlobalLock GlobalLock { get; }
        Mutant mutant;

        internal GlobalLockHandle(GlobalLock g)
        {
            this.GlobalLock = g;

            mutant = Mutant.Create(this.GlobalLock.Name);
            mutant.Lock();
        }

        void unlock_main()
        {
            if (mutant != null)
            {
                mutant.Unlock();
                mutant = null;
            }
        }

        public void Unlock()
        {
            unlock_main();
        }

        public void Dispose()
        {
            unlock_main();
        }
    }

    public class GlobalLock
    {
        public string Name { get; }

        public GlobalLock(string name)
        {
            this.Name = name;
        }

        public GlobalLockHandle Lock()
        {
            GlobalLockHandle h = new GlobalLockHandle(this);

            return h;
        }
    }

    internal class MutantUnix : Mutant
    {

        string filename;
        int locked_count = 0;
        IntPtr fs;

        public MutantUnix(string name)
        {
            filename = Path.Combine(Env.UnixMutantDir, Mutant.GenerateInternalName(name) + ".lock");
            IO.MakeDirIfNotExists(Env.UnixMutantDir);
        }

        public override void Lock()
        {
            if (locked_count == 0)
            {
                IO.MakeDirIfNotExists(Env.UnixMutantDir);

                Unisys.Permissions perm = Unisys.Permissions.S_IRUSR | Unisys.Permissions.S_IWUSR | Unisys.Permissions.S_IRGRP | Unisys.Permissions.S_IWGRP | Unisys.Permissions.S_IROTH | Unisys.Permissions.S_IWOTH;

                IntPtr f = Unisys.Open(filename, Unisys.OpenFlags.O_CREAT, (int)perm);
                if (f.ToInt64() < 0)
                {
                    throw new IOException("Open failed.");
                }

                if (Unisys.FLock(f, Unisys.LockOperations.LOCK_EX) == -1)
                {
                    throw new IOException("FLock failed.");
                }

                this.fs = f;

                locked_count++;
            }
        }

        public override void Unlock()
        {
            if (locked_count <= 0) throw new ApplicationException("locked_count <= 0");
            if (locked_count == 1)
            {
                Unisys.Close(this.fs);

                this.fs = IntPtr.Zero;
            }
            locked_count--;
        }
    }

    internal class MutantWin32 : Mutant
    {
        Mutex mutex;
        int locked_count = 0;

        public MutantWin32(string name)
        {
            bool f;
            mutex = new Mutex(false, Mutant.GenerateInternalName(name), out f);
        }

        public override void Lock()
        {
            if (locked_count == 0)
            {
                int num_retry = 0;
                LABEL_RETRY:

                try
                {
                    mutex.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                    if (num_retry >= 100) throw;
                    num_retry++;
                    goto LABEL_RETRY;
                }
            }
            locked_count++;
        }

        public override void Unlock()
        {
            if (locked_count <= 0) throw new ApplicationException("locked_count <= 0");
            if (locked_count == 1)
            {
                mutex.ReleaseMutex();
            }
            locked_count--;
        }
    }

    public abstract class Mutant
    {
        public static string GenerateInternalName(string name)
        {
            name = name.Trim().ToUpperInvariant();
            return "dnmutant_" + Str.ByteToStr(Str.HashStr(name)).ToLowerInvariant();
        }

        public abstract void Lock();
        public abstract void Unlock();

        public static Mutant Create(string name)
        {
            if (Env.IsWindows)
            {
                return new MutantWin32(name);
            }
            else
            {
                return new MutantUnix(name);
            }
        }
    }

    class SemaphoneArrayItem
    {
        public Semaphore Semaphone;
        public int MaxCount;
        public int CurrentWaitThreads;

        public SemaphoneArrayItem(int max)
        {
            this.Semaphone = new Semaphore(max - 1, max);
            this.MaxCount = max;
        }
    }

    public class SemaphoneArray
    {
        Dictionary<string, SemaphoneArrayItem> array = new Dictionary<string, SemaphoneArrayItem>();

        string normalize_name(string name)
        {
            Str.NormalizeString(ref name);
            name = name.ToUpperInvariant();
            return name;
        }

        public int GetNumSemaphone
        {
            get
            {
                lock (array)
                {
                    return array.Count;
                }
            }
        }

        public void Release(string name)
        {
            name = normalize_name(name);

            SemaphoneArrayItem sem;

            lock (array)
            {
                if (array.ContainsKey(name) == false)
                {
                    throw new ApplicationException("invalid semaphone name: " + name);
                }

                sem = array[name];

                int c = sem.Semaphone.Release();

                if ((c + 1) == sem.MaxCount)
                {
                    if (sem.CurrentWaitThreads == 0)
                    {
                        //sem.Semaphone.Dispose();

                        array.Remove(name);
                    }
                }
            }

        }

        public bool Wait(string name, int max_count, int timeout)
        {
            if (max_count <= 0 || (timeout < 0 && timeout != -1))
            {
                throw new ApplicationException("Invalid args.");
            }

            name = normalize_name(name);

            SemaphoneArrayItem sem = null;

            lock (array)
            {
                if (array.ContainsKey(name) == false)
                {
                    sem = new SemaphoneArrayItem(max_count);

                    array.Add(name, sem);

                    return true;
                }
                else
                {
                    sem = array[name];

                    if (sem.MaxCount != max_count)
                    {
                        throw new ApplicationException("max_count != current database value.");
                    }

                    if (sem.Semaphone.WaitOne(0))
                    {
                        return true;
                    }

                    Interlocked.Increment(ref sem.CurrentWaitThreads);
                }
            }

            bool ret = sem.Semaphone.WaitOne(timeout);

            Interlocked.Decrement(ref sem.CurrentWaitThreads);

            return ret;
        }
    }

    class WorkerQueuePrivate
    {
        object lockObj = new object();

        List<ThreadObj> thread_list;
        ThreadProc thread_proc;
        int num_worker_threads;
        Queue<object> taskQueue = new Queue<object>();
        Exception raised_exception = null;

        void worker_thread(object param)
        {
            while (true)
            {
                object task = null;

                // キューから 1 個取得する
                lock (lockObj)
                {
                    if (taskQueue.Count == 0)
                    {
                        return;
                    }
                    task = taskQueue.Dequeue();
                }

                // タスクを処理する
                try
                {
                    this.thread_proc(task);
                }
                catch (Exception ex)
                {
                    if (raised_exception == null)
                    {
                        raised_exception = ex;
                    }

                    Console.WriteLine(ex.Message);
                }
            }
        }

        public WorkerQueuePrivate(ThreadProc thread_proc, int num_worker_threads, object[] tasks)
        {
            thread_list = new List<ThreadObj>();
            int i;

            this.thread_proc = thread_proc;
            this.num_worker_threads = num_worker_threads;

            foreach (object task in tasks)
            {
                taskQueue.Enqueue(task);
            }

            raised_exception = null;

            for (i = 0; i < num_worker_threads; i++)
            {
                ThreadObj t = new ThreadObj(worker_thread);

                thread_list.Add(t);
            }

            foreach (ThreadObj t in thread_list)
            {
                t.WaitForEnd();
            }

            if (raised_exception != null)
            {
                throw raised_exception;
            }
        }
    }

    public static class Tick64
    {
        static object lock_obj = new object();
        static uint last_value = 0;
        static bool is_first = true;
        static uint num_round = 0;

        public static long Value
        {
            get
            {
                unchecked
                {
                    lock (lock_obj)
                    {
                        uint current_value = (uint)(System.Environment.TickCount + 3864700935);

                        if (is_first)
                        {
                            last_value = current_value;
                            is_first = false;
                        }

                        if (last_value > current_value)
                        {
                            // カウンタが 1 周した
                            num_round++;
                        }

                        last_value = current_value;

                        ulong ret = 4294967296UL * (ulong)num_round + current_value;

                        return (long)ret;
                    }
                }
            }
        }

        public static uint ValueUInt32
        {
            get
            {
                unchecked
                {
                    return (uint)((ulong)Value);
                }
            }
        }
    }

    public class Event
    {
        EventWaitHandle h;
        public const int Infinite = Timeout.Infinite;

        public Event()
        {
            init(false);
        }

        public Event(bool manualReset)
        {
            init(manualReset);
        }

        void init(bool manualReset)
        {
            h = new EventWaitHandle(false, (manualReset ? EventResetMode.ManualReset : EventResetMode.AutoReset));
        }

        public void Set()
        {
            h.Set();
        }

        public bool Wait()
        {
            return Wait(Infinite);
        }
        public bool Wait(int millisecs)
        {
            return h.WaitOne(millisecs, false);
        }

        public delegate bool WaitWithPollDelegate();

        public bool WaitWithPoll(int wait_millisecs, int poll_interval, WaitWithPollDelegate proc)
        {
            long end_tick = Time.Tick64 + (long)wait_millisecs;

            if (wait_millisecs == Infinite)
            {
                end_tick = long.MaxValue;
            }

            while (true)
            {
                long now = Time.Tick64;
                if (wait_millisecs != Infinite)
                {
                    if (now >= end_tick)
                    {
                        return false;
                    }
                }

                long next_wait = (end_tick - now);
                next_wait = Math.Min(next_wait, (long)poll_interval);
                next_wait = Math.Max(next_wait, 1);

                if (proc != null)
                {
                    if (proc())
                    {
                        return true;
                    }
                }

                if (Wait((int)next_wait))
                {
                    return true;
                }
            }
        }

        static EventWaitHandle[] toArray(Event[] events)
        {
            List<EventWaitHandle> list = new List<EventWaitHandle>();

            foreach (Event e in events)
            {
                list.Add(e.h);
            }

            return list.ToArray();
        }

        public static bool WaitAll(Event[] events)
        {
            return WaitAll(events, Infinite);
        }
        public static bool WaitAll(Event[] events, int millisecs)
        {
            if (events.Length <= 64)
            {
                return waitAllInner(events, millisecs);
            }
            else
            {
                return waitAllMulti(events, millisecs);
            }
        }

        static bool waitAllMulti(Event[] events, int millisecs)
        {
            int numBlocks = (events.Length + 63) / 64;
            List<Event>[] list = new List<Event>[numBlocks];
            int i;
            for (i = 0; i < numBlocks; i++)
            {
                list[i] = new List<Event>();
            }
            for (i = 0; i < events.Length; i++)
            {
                list[i / 64].Add(events[i]);
            }

            double start = Time.NowDouble;
            double giveup = start + (double)millisecs / 1000.0;
            foreach (List<Event> o in list)
            {
                double now = Time.NowDouble;
                if (now <= giveup || millisecs < 0)
                {
                    int waitmsecs;
                    if (millisecs >= 0)
                    {
                        waitmsecs = (int)((giveup - now) * 1000.0);
                    }
                    else
                    {
                        waitmsecs = Timeout.Infinite;
                    }

                    bool ret = waitAllInner(o.ToArray(), waitmsecs);
                    if (ret == false)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        static bool waitAllInner(Event[] events, int millisecs)
        {
            if (events.Length == 1)
            {
                return events[0].Wait(millisecs);
            }
            return EventWaitHandle.WaitAll(toArray(events), millisecs, false);
        }

        public static bool WaitAny(Event[] events)
        {
            return WaitAny(events, Infinite);
        }
        public static bool WaitAny(Event[] events, int millisecs)
        {
            if (events.Length == 1)
            {
                return events[0].Wait(millisecs);
            }
            return ((WaitHandle.WaitTimeout == EventWaitHandle.WaitAny(toArray(events), millisecs, false)) ? false : true);
        }

        public IntPtr Handle
        {
            get
            {
                return h.Handle;
            }
        }
    }

    public class ThreadData
    {
        static LocalDataStoreSlot slot = Thread.AllocateDataSlot();

        public readonly SortedDictionary<string, object> DataList = new SortedDictionary<string, object>();

        public static ThreadData CurrentThreadData
        {
            get
            {
                return GetCurrentThreadData();
            }
        }

        public static ThreadData GetCurrentThreadData()
        {
            ThreadData t;

            try
            {
                t = (ThreadData)Thread.GetData(slot);
            }
            catch
            {
                t = null;
            }

            if (t == null)
            {
                t = new ThreadData();

                Thread.SetData(slot, t);
            }

            return t;
        }
    }

    public delegate void ThreadProc(object userObject);

    public class ThreadObj
    {
        static int defaultStackSize = 100000;

        static LocalDataStoreSlot currentObjSlot = Thread.AllocateDataSlot();

        public const int Infinite = Timeout.Infinite;

        bool stopped = false;

        public bool IsFinished
        {
            get
            {
                return this.stopped;
            }
        }

        ThreadProc proc;
        Thread thread;
        EventWaitHandle waitInit;
        EventWaitHandle waitEnd;
        EventWaitHandle waitInitForUser;
        public Thread Thread
        {
            get { return thread; }
        }
        object userObject;

        public ThreadObj(ThreadProc threadProc)
        {
            init(threadProc, null, 0);
        }

        public ThreadObj(ThreadProc threadProc, int stacksize)
        {
            init(threadProc, null, stacksize);
        }

        public ThreadObj(ThreadProc threadProc, object userObject)
        {
            init(threadProc, userObject, 0);
        }

        public ThreadObj(ThreadProc threadProc, object userObject, int stacksize)
        {
            init(threadProc, userObject, stacksize);
        }

        void init(ThreadProc threadProc, object userObject, int stacksize)
        {
            if (stacksize == 0)
            {
                stacksize = defaultStackSize;
            }

            this.proc = threadProc;
            this.userObject = userObject;
            waitInit = new EventWaitHandle(false, EventResetMode.AutoReset);
            waitEnd = new EventWaitHandle(false, EventResetMode.ManualReset);
            waitInitForUser = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.thread = new Thread(new ParameterizedThreadStart(commonThreadProc), stacksize);
            this.thread.Start(this);
            waitInit.WaitOne();
        }

        public static int DefaultStackSize
        {
            get
            {
                return defaultStackSize;
            }

            set
            {
                defaultStackSize = value;
            }
        }

        void commonThreadProc(object obj)
        {
            Thread.SetData(currentObjSlot, this);

            waitInit.Set();

            try
            {
                this.proc(this.userObject);
            }
            finally
            {
                stopped = true;
                waitEnd.Set();
            }
        }

        public static ThreadObj Current => GetCurrentThreadObj();
        public static int CurrentThreadId => Thread.CurrentThread.ManagedThreadId;

        public static ThreadObj GetCurrentThreadObj()
        {
            return (ThreadObj)Thread.GetData(currentObjSlot);
        }

        public static void NoticeInited()
        {
            GetCurrentThreadObj().waitInitForUser.Set();
        }

        public void WaitForInit()
        {
            waitInitForUser.WaitOne();
        }

        public void WaitForEnd(int timeout)
        {
            waitEnd.WaitOne(timeout, false);
        }
        public void WaitForEnd()
        {
            waitEnd.WaitOne();
        }

        public static void Sleep(int millisec)
        {
            if (millisec == 0x7fffffff)
            {
                millisec = ThreadObj.Infinite;
            }

            Thread.Sleep(millisec);
        }

        public static void Yield()
        {
            Thread.Sleep(0);
        }

        public static void ProcessWorkQueue(ThreadProc thread_proc, int num_worker_threads, object[] tasks)
        {
            WorkerQueuePrivate q = new WorkerQueuePrivate(thread_proc, num_worker_threads, tasks);
        }
    }
}
