using kcslib;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace kcslib
{
    /// <summary>
    /// Base public class of the messages exchanged between the worker and UI 
    /// threads.
    /// </summary>
    public abstract class KThreadMsg
    {
        /// <summary>
        /// This method is called when the message is received.
        /// </summary>
        public abstract void Run();
    }

    /// <summary>
    /// Message used to invoke a method in the context of the worker or the UI.
    /// </summary>
    public class KThreadInvokeMsg : KThreadMsg
    {
        /// <summary>
        /// Method invoked.
        /// </summary>
        private Delegate m_method = null;

        /// <summary>
        /// Method arguments.
        /// </summary>
        private Object[] m_args = null;

        public KThreadInvokeMsg(Delegate method, Object[] args)
        {
            m_method = method;
            m_args = args;
        }

        public override void Run()
        {
            m_method.DynamicInvoke(m_args);
        }
    }

    /// <summary>
    /// This exception is thrown when the worker thread gets cancelled.
    /// </summary>
    public class KWorkerCancellationException : Exception
    {
    }

    /// <summary>
    /// Represent a worker thread.
    /// </summary>
    public abstract class KWorkerThread
    {
        protected enum WorkerStatus
        {
            /// <summary>
            /// The thread has not been started.
            /// </summary>
            None,

            /// <summary>
            /// The thread is running.
            /// </summary>
            Running,

            /// <summary>
            /// The thread honored a cancellation request and completed its
            /// execution before its work was finished.
            /// </summary>
            Cancelled,

            /// <summary>
            /// The thread failed to complete its work because an error occurred.
            /// </summary>
            Failed,

            /// <summary>
            /// The thread completed its works successfully.
            /// </summary>
            Success
        }

        /// <summary>
        /// Message queue.
        /// </summary>
        private Queue<KThreadMsg> MsgQueue;

        /// <summary>
        /// Mutex to protect the queue.
        /// </summary>
        private Object MsgMutex = new Object();

        /// <summary>
        /// Socket pair used to wake up the worker thread.
        /// </summary>
        private Socket[] SocketPair;

        /// <summary>
        /// Wrapper around the threading interface of C#.
        /// </summary>
        private Thread InternalThread;

        /// <summary>
        /// True if cancellation has been requested by the UI thread. This flag
        /// is set asynchronously by the UI thread. It can be queried directly
        /// to detect if the thread has been cancelled.
        /// </summary>
        public volatile bool CancelFlag;

        /// <summary>
        /// True if the Block() method is currently being invoked by the worker
        /// thread. 
        /// </summary>
        private bool BlockedFlag;

        /// <summary>
        /// Status of the worker thread. Do *NOT* access this field from the UI
        /// thread while this thread is or might be running. This field is purely
        /// internal to this thread.
        /// </summary>
        protected WorkerStatus Status = WorkerStatus.None;

        /// <summary>
        /// If the status is "failed", the exception that caused the error, if there
        /// is one. This is set by InternalRun only.
        /// </summary>
        protected Exception FailException;

        /// <summary>
        /// Start the thread. Note: this method can be called again when
        /// the thread has called its OnCompletion() handler.
        /// </summary>
        public void Start()
        {
            Debug.Assert(Status != WorkerStatus.Running);
            Debug.Assert(InternalThread == null);

            // Initialize the socket pair once.
            if (SocketPair == null) SocketPair = KSocket.SocketPair();

            // Initialize the variables used once per invocation.
            MsgQueue = new Queue<KThreadMsg>();
            CancelFlag = false;
            BlockedFlag = false;
            Status = WorkerStatus.Running;
            FailException = null;

            InternalThread = new Thread(InternalRun);
            InternalThread.Start();
        }

        /// <summary>
        /// Post a message to the worker thread.
        /// </summary>
        public void PostToWorker(KThreadMsg m)
        {
            lock (MsgMutex)
            {
                MsgQueue.Enqueue(m);
                WakeUp();
            }
        }

        /// <summary>
        /// Invoke a method of the worker thread.
        /// </summary>
        public void InvokeWorker(Delegate method, Object[] args)
        {
            PostToWorker(new KThreadInvokeMsg(method, args));
        }

        /// <summary>
        /// Request the thread to be cancelled.
        /// </summary>
        public void RequestCancellation()
        {
            if (CancelFlag) return;
            CancelFlag = true;
            WakeUp();
        }

        /// <summary>
        /// This method is called to execute the main loop of the thread.
        /// </summary>
        protected abstract void Run();

        /// <summary>
        /// This method is called in the context of the UI thread when the
        /// thread has completed its execution, regardless of the outcome.
        /// </summary>
        protected virtual void OnCompletion() {}

        /// <summary>
        /// This method can be called by the worker thread from time to time
        /// to check for cancellation. A cancellation exception is thrown if
        /// the thread has been cancelled. This method is very fast.
        /// </summary>
        protected virtual void CheckCancellation()
        {
            if (CancelFlag) throw new KWorkerCancellationException();
        }

        /// <summary>
        /// Post a message to the UI thread from the context of the worker thread.
        /// </summary>
        protected void PostToUI(KThreadMsg m)
        {
            KBase.ExecInUI(new KBase.EmptyDelegate(m.Run));
        }

        /// <summary>
        /// Invoke a method of the UI thread.
        /// </summary>
        protected void InvokeUI(Delegate method, Object[] args)
        {
            PostToUI(new KThreadInvokeMsg(method, args));
        }

        /// <summary>
        /// Wait for one of the sockets specified to become ready, for a message
        /// to arrive or for the thread to be cancelled. Be careful not to call
        /// Block() while handling a message, since this method does not handle
        /// recursivity. Do not call this method after it has thrown an exception.
        /// </summary>
        protected void Block(SelectSockets set)
        {
            Debug.Assert(!BlockedFlag);
            BlockedFlag = true;
            set.AddRead(SocketPair[1]);
            set.Select();
            FlushWakeUp();
            CheckCancellation();
            CheckMessages();
            BlockedFlag = false;
        }

        /// <summary>
        /// Wake up the thread.
        /// </summary>
        private void WakeUp()
        {
            byte[] b = { 1 };
            KSocket.SockWrite(SocketPair[0], b, 0, 1);
        }

        /// <summary>
        /// Flush the queued wake-up requests.
        /// </summary>
        private void FlushWakeUp()
        {
            byte[] b = new byte[100];
            while (KSocket.SockRead(SocketPair[1], b, 0, 100) != -1) { }
        }

        /// <summary>
        /// Process the buffered messages.
        /// </summary>
        private void CheckMessages()
        {
            Queue<KThreadMsg> q = null;

            // Get the messages.
            lock (MsgMutex)
            {
                if (MsgQueue.Count > 0)
                {
                    q = MsgQueue;
                    MsgQueue = new Queue<KThreadMsg>();
                }
            }

            // Handle the buffered messages. No processing is done
            // if the thread has completed its work.
            if (q != null)
            {
                foreach (KThreadMsg m in q)
                {
                    if (Status != WorkerStatus.Running) break;
                    m.Run();
                }
            }
        }

        /// <summary>
        /// This method handles the life-cycle management of the thread, as some
        /// marketing people would put it.
        /// </summary>
        private void InternalRun()
        {
            Debug.Assert(Status == WorkerStatus.Running);

            try
            {
                Run();
            }

            catch (KWorkerCancellationException)
            {
                Status = WorkerStatus.Cancelled;
            }

            catch (Exception e)
            {
                Status = WorkerStatus.Failed;
                FailException = e;
            }

            if (Status == WorkerStatus.Running) Status = WorkerStatus.Success;
            KBase.ExecInUI(new KBase.EmptyDelegate(InternalOnCompletion));
        }

        /// <summary>
        /// This method is called when the thread has completed.
        /// </summary>
        private void InternalOnCompletion()
        {
            Debug.Assert(Status == WorkerStatus.Cancelled ||
                         Status == WorkerStatus.Failed ||
                         Status == WorkerStatus.Success);
            Debug.Assert(InternalThread != null);

            // Join with the internal thread.
            InternalThread.Join();
            InternalThread = null;

            // Call the user-visible OnCompletion() handler.
            OnCompletion();
        }
    }
}
