using kcslib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.IO;

/* Documentation:
 * 
 * The EAnp broker manages the interactions between the EchoTracker and the
 * KWM. There are two modes: server and client. In server mode, the broker
 * listens for incoming connections. In client mode, the broker tries to
 * connect once to the server.
 *
 * The broker uses channels to represent a connection to a remote host. The
 * broker raises an event when a new channel is opened.
 * 
 * A channel supports five high-level operations:
 * - Send a query to the remote host and wait for the reply (outgoing query).
 * - Send an event to the remote host.
 * - Handle a query received from the remote host (incoming query).
 * - Handle an event received from the remote host.
 * - Close the channel.
 * 
 * The channel raises an event when an incoming query or event is received,
 * and when the channel is closed.
 * 
 * An outgoing query can be cancelled at any time. An event is raised when
 * the query completes, either because the reply was received or the channel
 * was closed.
 * 
 * An incoming query can be replied to when the result is ready. An event is
 * raised if the query is cancelled, either because the remote host cancelled
 * the query or the channel was closed.
 * 
 * The broker is started with the Start() method. The broker is stopped with
 * the TryStop() method. TryStop() returns true when the broker is ready to
 * stop. Call TryStop() once to initiate the shutdown procedure. If TryStop()
 * returns false, an event will be raised when the broker is ready to stop.
 * Wait for that event. Call TryStop() again when the event is received.
 * 
 * Since the implementation is event-based, you have to be careful about the 
 * inherent callback race conditions. If you perform an action on a channel or
 * a query when you receive an event through a callback method, then the broker
 * may fire other events before the original event has been fully processed.
 * The broker is designed to behave gracefully in that situation. However, pay
 * attention to the following pitfalls in your callback methods:
 * - The channel included in a "new channel open" event may have been closed.
 * - The query included in a "new incoming query" event may have been closed.
 * - The channel you try to send a command or event to may have been closed.
 * 
 * In the code, the prefix 'Internal' means that the field or method is part
 * of the private implemention, even though it might be declared public to be
 * accessible outside the class.
 */

namespace kwmlib
{
    /// <summary>
    /// Base class of the EAnp broker.
    /// </summary>
    public abstract class EAnpBaseBroker
    {
        /// <summary>
        /// Fired when the worker thread has been collected. Call TryStop()
        /// when this is fired.
        /// </summary>
        public event EventHandler OnClose;

        /// <summary>
        /// Fired when a channel is opened.
        /// </summary>
        public event EventHandler<EAnpChannelOpenEventArgs> OnChannelOpen;

        /// <summary>
        /// Reference to the worker thread.
        /// </summary>
        protected EAnpBaseThread m_thread = null;

        /// <summary>
        /// Tree of channels indexed by channel ID.
        /// </summary>
        protected SortedDictionary<UInt64, EAnpChannel> m_channelTree = new SortedDictionary<UInt64, EAnpChannel>();

        /// <summary>
        /// ID of the next channel. This field is accessed only from the thread.
        /// This field is declared here because the broker persists longer than
        /// the thread and we don't want to recycle IDs.
        /// </summary>
        public UInt64 InternalNextChannelID = 1;

        /// <summary>
        /// Start the broker. Do not call this unless TryStop() returns true.
        /// </summary>
        public virtual void Start()
        {
            Debug.Assert(m_thread == null);
            if (this is EAnpClientBroker) m_thread = new EAnpClientThread(this as EAnpClientBroker);
            else m_thread = new EAnpServerThread(this as EAnpServerBroker);
            m_thread.Start();
        }

        /// <summary>
        /// This method is called to stop the broker. It returns true when the
        /// broker is ready to stop.
        /// </summary>
        public virtual bool TryStop()
        {
            if (m_thread != null) m_thread.RequestCancellation();
            SortedDictionary<UInt64, EAnpChannel> tree = new SortedDictionary<UInt64, EAnpChannel>(m_channelTree);
            foreach (EAnpChannel c in tree.Values) c.InternalClose(new EAnpExInterrupted());
            return (m_thread == null);
        }
        
        /// <summary>
        /// Called by the worker thread when it has completed.
        /// </summary>
        public void InternalThreadCompletion()
        {
            m_thread = null;
            if (OnClose != null) OnClose(this, null);
        }

        /// <summary>
        /// Called by the worker thread when a channel has been opened.
        /// </summary>
        public void InternalChannelOpened(UInt64 channelID)
        {
            if (m_thread == null || m_thread.CancelFlag) return;
            EAnpChannel c = new EAnpChannel(this, channelID);
            m_channelTree[c.InternalChannelID] = c;
            if (OnChannelOpen != null) OnChannelOpen(this, new EAnpChannelOpenEventArgs(c));
        }

        /// <summary>
        /// Called by the worker thread when a channel has been closed.
        /// </summary>
        public void InternalChannelClosed(UInt64 channelID)
        {
            EAnpChannel c = GetChannelByID(channelID);
            if (c != null) c.InternalClose(new EAnpExEAnpConn());
        }

        /// <summary>
        /// Called by the worker thread when some messages have been received
        /// from a channel.
        /// </summary>
        public void InternalMsgReceived(UInt64 channelID, List<AnpMsg> msgList)
        {
            EAnpChannel c = GetChannelByID(channelID);
            if (c != null) c.InternalMsgReceived(msgList);
        }

        /// <summary>
        /// Called by broker channel to unlink the channel.
        /// </summary>
        public void InternalUnlinkChannel(EAnpChannel channel)
        {
            m_channelTree.Remove(channel.InternalChannelID);
        }

        /// <summary>
        /// Called by broker channel to close the thread channel.
        /// </summary>
        public void InternalCloseThreadChannel(EAnpChannel channel)
        {
            if (m_thread == null) return;
            m_thread.InvokeWorker(new Action<UInt64>(m_thread.RequestCloseChannel),
                                  new object[] { channel.InternalChannelID });
        }

        /// <summary>
        /// Called by broker channel to send a message to a thread channel.
        /// </summary>
        public void InternalSendMsg(EAnpChannel channel, AnpMsg msg)
        {
            if (m_thread == null) return;
            m_thread.InvokeWorker(new Action<UInt64, AnpMsg>(m_thread.RequestSendMsg),
                                  new object[] { channel.InternalChannelID, msg });
        }

        /// <summary>
        /// Return the channel having the ID specified, if any.
        /// </summary>
        private EAnpChannel GetChannelByID(UInt64 id)
        {
            if (m_channelTree.ContainsKey(id)) return m_channelTree[id];
            return null;
        }
    }

    /// <summary>
    /// The client broker tries to open a single channel to the server when it
    /// is started. A new channel is opened when the previous one is closed.
    /// </summary>
    public class EAnpClientBroker : EAnpBaseBroker
    {
        private EAnpClientThread m_clientThread { get { return m_thread as EAnpClientThread; } }

        /// <summary>
        /// Reference to the file system watcher used to detect when the KWM
        /// has started up.
        /// </summary>
        private FileSystemWatcher m_watcher = null;

        /// <summary>
        /// Return true if the KWM process is started.
        /// </summary>
        public static bool IsKwmStarted()
        {
            Process currentProcess = Process.GetCurrentProcess();
            String procName = currentProcess.ProcessName;

            Process[] processes = Process.GetProcessesByName("kwm");
            WindowsIdentity userWI = WindowsIdentity.GetCurrent();

            foreach (Process p in processes)
            {
                String stringSID = KSyscalls.GetProcessSid(p);
                bool InOurSession = currentProcess.SessionId == p.SessionId;
                bool IsOurUser = String.Compare(stringSID, userWI.User.Value, true) == 0;
                if (InOurSession && IsOurUser) return true;
            }

            return false;
        }

        public override void Start()
        {
            base.Start();

            // Start the watcher, if possible.
            try
            {
                m_watcher = new FileSystemWatcher(KwmPath.GetKcsLocalDataPath());
                m_watcher.SynchronizingObject = KBase.InvokeUiControl;
                m_watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName;
                m_watcher.IncludeSubdirectories = false;
                m_watcher.Created += HandleOnWatcherCreated;
                m_watcher.EnableRaisingEvents = true;
            }

            catch (Exception ex)
            {
                KLogging.Log("Cannot start KWM FileSystemWatcher: " + ex.Message);
                m_watcher = null;
            }
        }

        public override bool TryStop()
        {
            // Stop the watcher.
            if (m_watcher != null)
            {
                m_watcher.EnableRaisingEvents = false;
                m_watcher = null;
            }

            return base.TryStop();
        }

        /// <summary>
        /// Send a request to connect immediately to the KWM, if possible.
        /// </summary>
        public void RequestConnect()
        {
            if (m_thread == null) return;
            m_clientThread.InvokeWorker(new KBase.EmptyDelegate(m_clientThread.RequestConnect), new object[] { });
        }

        /// <summary>
        /// Called when a file has been created in the local data directory.
        /// </summary>
        private void HandleOnWatcherCreated(Object sender, FileSystemEventArgs args)
        {
            // The information file has been written.
            if (KwmPath.GetKwmInfoPath() + ".trigger" == args.FullPath)
            {
                RequestConnect();
            }
        }
    }

    /// <summary>
    /// The server broker accepts as many connections as possible.
    /// </summary>
    public class EAnpServerBroker : EAnpBaseBroker
    {
    }

    /// <summary>
    /// Represent a channel opened between the local host and a remote host.
    /// </summary>
    public class EAnpChannel : IComparable 
    {
        /// <summary>
        /// Fired when the channel has been closed.
        /// </summary>
        public event EventHandler OnClose;

        /// <summary>
        /// Fired when an incoming query is received. Make sure the query is
        /// still pending before you register to it.
        /// </summary>
        public event EventHandler<EAnpIncomingQueryEventArgs> OnIncomingQuery;

        /// <summary>
        /// Fired when an EAnp event is received.
        /// </summary>
        public event EventHandler<EAnpIncomingEventEventArgs> OnIncomingEvent;

        /// <summary>
        /// Exception set when the channel is closing. It can be null if the
        /// channel is closing normally, i.e. because you requested so.
        /// </summary>
        public Exception Ex = null;

        /// <summary>
        /// Internal channel ID.
        /// </summary>
        public UInt64 InternalChannelID = 0;

        /// <summary>
        /// Reference to the broker.
        /// </summary>
        private EAnpBaseBroker m_broker = null;

        /// <summary>
        /// True if the channel is open.
        /// </summary>
        private bool m_openFlag = true;

        /// <summary>
        /// ID of the next command sent.
        /// </summary>
        private UInt64 m_nextCmdID = 1;

        /// <summary>
        /// Tree of pending outgoing queries indexed by command ID.
        /// </summary>
        private SortedDictionary<UInt64, EAnpOutgoingQuery> m_outgoingTree =
            new SortedDictionary<UInt64, EAnpOutgoingQuery>();

        /// <summary>
        /// Tree of pending incoming queries indexed by command ID.
        /// </summary>
        private SortedDictionary<UInt64, EAnpIncomingQuery> m_incomingTree =
            new SortedDictionary<UInt64, EAnpIncomingQuery>();

        public EAnpChannel(EAnpBaseBroker broker, UInt64 channelID)
        {
            m_broker = broker;
            InternalChannelID = channelID;
        }

        /// <summary>
        /// Return true if the channel is open.
        /// </summary>
        public bool IsOpen() { return m_openFlag; }

        /// <summary>
        /// Close this channel. 'Ex' provides the reason why the channel is 
        /// closing.
        /// </summary>
        public void Close(Exception ex)
        {
            if (!m_openFlag) return;
            m_broker.InternalCloseThreadChannel(this);
            InternalClose(ex);
        }

        /// <summary>
        /// Send a query to the remote host. The query is returned if the
        /// channel is open, otherwise null is returned.
        /// </summary>
        public EAnpOutgoingQuery SendCmd(AnpMsg cmd)
        {
            if (!m_openFlag) return null;
            cmd.ID = m_nextCmdID++;
            EAnpOutgoingQuery q = new EAnpOutgoingQuery(this, cmd);
            m_outgoingTree[q.CmdID] = q;
            InternalSendMsg(cmd);
            return q;
        }

        /// <summary>
        /// Send an EAnp event to the remote host. Nothing is done if the 
        /// channel is closed.
        /// </summary>
        public void SendEvt(AnpMsg evt)
        {
            InternalSendMsg(evt);
        }

        /// <summary>
        /// Called by the broker when the thread channel has been closed or by
        /// the user-level Close() operation.
        /// </summary>
        public void InternalClose(Exception ex)
        {
            if (!m_openFlag) return;
            m_openFlag = false;
            Ex = ex;
            m_broker.InternalUnlinkChannel(this);

            SortedDictionary<UInt64, EAnpOutgoingQuery> oTree = new SortedDictionary<UInt64, EAnpOutgoingQuery>(m_outgoingTree);
            foreach (EAnpOutgoingQuery q in oTree.Values) q.InternalOnChannelClose(ex);

            SortedDictionary<UInt64, EAnpIncomingQuery> iTree = new SortedDictionary<UInt64, EAnpIncomingQuery>(m_incomingTree);
            foreach (EAnpIncomingQuery q in iTree.Values) q.InternalOnCancellation();

            if (OnClose != null) OnClose(this, null);
        }

        /// <summary>
        /// Send a message to the remote host.
        /// </summary>
        public void InternalSendMsg(AnpMsg msg)
        {
            if (!m_openFlag) return;
            m_broker.InternalSendMsg(this, msg);
        }

        /// <summary>
        /// Called by the broker when some messages are received.
        /// </summary>
        public void InternalMsgReceived(List<AnpMsg> msgList)
        {
            foreach (AnpMsg m in msgList) HandleMsgReceived(m);
        }

        /// <summary>
        /// Helper method.
        /// </summary>
        private void HandleMsgReceived(AnpMsg m)
        {
            if (!m_openFlag) return;

            if (EAnpProto.IsCmd(m.Type))
            {
                if (m.Type == (uint)EAnpCmd.CancelCmd)
                {
                    EAnpIncomingQuery q = GetIncomingByID(m.ID);
                    if (q != null) q.InternalOnCancellation();
                }

                else
                {
                    EAnpIncomingQuery q = new EAnpIncomingQuery(this, m);
                    m_incomingTree[q.CmdID] = q;
                    if (OnIncomingQuery != null) OnIncomingQuery(this, new EAnpIncomingQueryEventArgs(q));
                }
            }

            else if (EAnpProto.IsRes(m.Type))
            {
                EAnpOutgoingQuery q = GetOutgoingByID(m.ID);
                if (q != null) q.InternalOnReply(m);
            }

            else if (EAnpProto.IsEvt(m.Type))
            {
                if (OnIncomingEvent != null) OnIncomingEvent(this, new EAnpIncomingEventEventArgs(this, m));
            }
        }

        /// <summary>
        /// Remove an outgoing query from the outgoing query tree.
        /// </summary>
        public void InternalRemoveOutgoing(EAnpOutgoingQuery q)
        {
            m_outgoingTree.Remove(q.CmdID);
        }

        /// <summary>
        /// Remove an incoming query from the incoming query tree.
        /// </summary>
        public void InternalRemoveIncoming(EAnpIncomingQuery q)
        {
            m_incomingTree.Remove(q.CmdID);
        }

        /// <summary>
        /// Return the incoming query having the ID specified, if any.
        /// </summary>
        private EAnpIncomingQuery GetIncomingByID(UInt64 id)
        {
            if (m_incomingTree.ContainsKey(id)) return m_incomingTree[id];
            return null;
        }

        /// <summary>
        /// Return the outgoing query having the ID specified, if any.
        /// </summary>
        private EAnpOutgoingQuery GetOutgoingByID(UInt64 id)
        {
            if (m_outgoingTree.ContainsKey(id)) return m_outgoingTree[id];
            return null;
        }

        public int CompareTo(Object obj)
        {
            EAnpChannel c = (EAnpChannel)obj;
            return InternalChannelID.CompareTo(c.InternalChannelID);
        }
    }

    /// <summary>
    /// Base EAnp query class.
    /// </summary>
    public class EAnpBaseQuery
    {
        /// <summary>
        /// Channel associated to this query.
        /// </summary>
        public EAnpChannel Channel = null;

        /// <summary>
        /// EAnp command of this query.
        /// </summary>
        public AnpMsg Cmd = null;

        /// <summary>
        /// EAnp result of this query.
        /// </summary>
        public AnpMsg Res = null;

        /// <summary>
        /// True if this query is still pending.
        /// </summary>
        protected bool m_pendingFlag = true;

        /// <summary>
        /// Message ID of the command.
        /// </summary>
        public UInt64 CmdID { get { return Cmd.ID; } }

        /// <summary>
        /// Return true if this query is still pending.
        /// </summary>
        public bool IsPending()
        {
            return m_pendingFlag;
        }

        public EAnpBaseQuery(EAnpChannel channel, AnpMsg cmd)
        {
            Channel = channel;
            Cmd = cmd;
        }
    }

    /// <summary>
    /// Query sent to the remote host.
    /// </summary>
    public class EAnpOutgoingQuery : EAnpBaseQuery
    {
        /// <summary>
        /// Fired when the reply is received or the query fails. Not fired
        /// when the query was cancelled with Cancel().
        /// </summary>
        public event EventHandler OnCompletion;

        /// <summary>
        /// This field is non-null if the query failed. It is null if the query
        /// was cancelled.
        /// </summary>
        public Exception Ex = null;

        public EAnpOutgoingQuery(EAnpChannel channel, AnpMsg cmd)
            : base(channel, cmd)
        {
        }

        /// <summary>
        /// Cancel this query.
        /// </summary>
        public void Cancel()
        {
            if (!m_pendingFlag) return;
            
            AnpMsg m = new AnpMsg();
            m.Type = (uint)EAnpCmd.CancelCmd;
            m.ID = CmdID;
            Channel.InternalSendMsg(m);

            Finish();
        }

        /// <summary>
        /// Called by the channel when it is closing.
        /// </summary>
        public void InternalOnChannelClose(Exception ex)
        {
            if (!m_pendingFlag) return;
            Ex = ex;
            Finish();
            if (OnCompletion != null) OnCompletion(this, null);
        }

        /// <summary>
        /// Called when the reply is received.
        /// </summary>
        public void InternalOnReply(AnpMsg res)
        {
            if (!m_pendingFlag) return;
            Res = res;
            Finish();
            if (OnCompletion != null) OnCompletion(this, null);
        }

        /// <summary>
        /// Set the pending flag to false and unlink this query.
        /// </summary>
        private void Finish()
        {
            m_pendingFlag = false;
            Channel.InternalRemoveOutgoing(this);
        }
    }

    /// <summary>
    /// Query received from the remote host.
    /// </summary>
    public class EAnpIncomingQuery : EAnpBaseQuery
    {
        /// <summary>
        /// Fired when the query is cancelled because the channel is closing or
        /// the remote host cancelled the query.
        /// </summary>
        public event EventHandler OnCancellation;

        public EAnpIncomingQuery(EAnpChannel channel, AnpMsg cmd)
            : base(channel, cmd)
        {
        }

        /// <summary>
        /// Send the reply to the remote host.
        /// </summary>
        public void Reply(AnpMsg res)
        {
            if (!m_pendingFlag) return;
            Res = res;
            Res.ID = CmdID;
            Channel.InternalSendMsg(res);
            Finish();
        }

        /// <summary>
        /// Called by the channel when it is closing or a cancellation command
        /// is received.
        /// </summary>
        public void InternalOnCancellation()
        {
            if (!m_pendingFlag) return;
            Finish();
            if (OnCancellation != null) OnCancellation(this, null);
        }

        /// <summary>
        /// Set the pending flag to false and unlink this query.
        /// </summary>
        private void Finish()
        {
            m_pendingFlag = false;
            Channel.InternalRemoveIncoming(this);
        }
    }

    /// <summary>
    /// Event fired when a new channel is opened.
    /// </summary>
    public class EAnpChannelOpenEventArgs : EventArgs
    {
        public EAnpChannel Channel;

        public EAnpChannelOpenEventArgs(EAnpChannel channel)
        {
            Channel = channel;
        }
    }

    /// <summary>
    /// Event fired when an incoming query is received.
    /// </summary>
    public class EAnpIncomingQueryEventArgs : EventArgs
    {
        public EAnpIncomingQuery Query;

        public EAnpIncomingQueryEventArgs(EAnpIncomingQuery query)
        {
            Query = query;
        }
    }

    /// <summary>
    /// Event fired when an event is received.
    /// </summary>
    public class EAnpIncomingEventEventArgs : EventArgs
    {
        public EAnpChannel Channel;
        public AnpMsg Msg;

        public EAnpIncomingEventEventArgs(EAnpChannel channel, AnpMsg msg)
        {
            Channel = channel;
            Msg = msg;
        }
    }
}