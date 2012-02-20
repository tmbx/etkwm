using kcslib;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Diagnostics;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Globalization;
using System.Net;

namespace kwmlib
{
    /// <summary>
    /// Status of a thread channel.
    /// </summary>
    public enum EAnpThreadChannelStatus
    {
        /// <summary>
        /// Initial step.
        /// </summary>
        Initial,

        /// <summary>
        /// Connecting to the server.
        /// </summary>
        Connecting,

        /// <summary>
        /// Sending/receiving the handshake data.
        /// </summary>
        Handshake,

        /// <summary>
        /// The channel is open.
        /// </summary>
        Open,

        /// <summary>
        /// The channel has been closed.
        /// </summary>
        Closed
    }

    /// <summary>
    /// Base worker thread class.
    /// </summary>
    public abstract class EAnpBaseThread : KWorkerThread
    {
        /// <summary>
        /// Length in bytes of the secret written in the information file.
        /// </summary>
        public const int SecretLen = 16;

        /// <summary>
        /// Reference to the broker.
        /// </summary>
        public EAnpBaseBroker Broker = null;

        /// <summary>
        /// Authentication data written in the information file.
        /// </summary>
        public byte[] Secret = null;

        /// <summary>
        /// Port written in the information file.
        /// </summary>
        public int Port = 0;

        public EAnpBaseThread(EAnpBaseBroker broker)
        {
            Broker = broker;
        }

        /// <summary>
        /// Return the channel having the ID specified, if any.
        /// </summary>
        public abstract EAnpThreadChannel GetChannelByID(UInt64 id);

        /// <summary>
        /// Remove the channel from the list of channels managed by the thread.
        /// </summary>
        public abstract void RemoveChannel(UInt64 id);

        /// <summary>
        /// Execute an iteration of the main loop.
        /// </summary>
        protected abstract void RunPass();

        /// <summary>
        /// Called before the main loop is entered.
        /// </summary>
        protected virtual void Initialize() { }

        /// <summary>
        /// Called when the thread has completed.
        /// </summary>
        protected virtual void CleanUp() { }

        protected override void Run()
        {
            Initialize();
            while (true) RunPass();
        }

        protected override void OnCompletion()
        {
            if (Status == WorkerStatus.Failed) KBase.HandleException(FailException, true);
            CleanUp();
            Broker.InternalThreadCompletion();
        }

        /// <summary>
        /// Notify the broker that a channel has been opened.
        /// </summary>
        public void NotifyChannelOpened(UInt64 channelID)
        {
            InvokeUI(new Action<UInt64>(Broker.InternalChannelOpened), new object[] { channelID });
        }

        /// <summary>
        /// Notify the broker that a channel has been closed.
        /// </summary>
        public void NotifyChannelClosed(UInt64 channelID)
        {
            InvokeUI(new Action<UInt64>(Broker.InternalChannelClosed), new object[] { channelID });
        }

        /// <summary>
        /// Notify the broker that some messages have been received for a 
        /// channel.
        /// </summary>
        public void NotifyMsgReceived(UInt64 channelID, List<AnpMsg> recvList)
        {
            InvokeUI(new Action<UInt64, List<AnpMsg>>(Broker.InternalMsgReceived),
                     new object[] { channelID, recvList });
        }

        /// <summary>
        /// Called by the broker to close a channel.
        /// </summary>
        public void RequestCloseChannel(UInt64 channelID)
        {
            EAnpThreadChannel c = GetChannelByID(channelID);
            if (c != null) c.Close();
        }

        /// <summary>
        /// Called by the broker to send a message in a channel.
        /// </summary>
        public void RequestSendMsg(UInt64 channelID, AnpMsg msg)
        {
            EAnpThreadChannel c = GetChannelByID(channelID);
            if (c != null) c.SendQueue.Enqueue(msg);
        }
    }

    /// <summary>
    /// Thread used by the client broker.
    /// </summary>
    public class EAnpClientThread : EAnpBaseThread
    {
        /// <summary>
        /// True if a request has been sent to try to connect.
        /// </summary>
        public bool TryConnectFlag = true;

        /// <summary>
        /// Current channel, if any.
        /// </summary>
        public EAnpClientThreadChannel Channel = null;

        public EAnpClientThread(EAnpClientBroker broker) :
            base(broker)
        { }

        public override EAnpThreadChannel GetChannelByID(UInt64 id)
        {
            if (Channel != null && id == Channel.ChannelID) return Channel;
            return null;
        }

        public override void RemoveChannel(UInt64 id)
        {
            if (Channel != null && id == Channel.ChannelID) Channel = null;
        }

        protected override void CleanUp()
        {
            if (Channel != null)
            {
                Channel.Close();
                Channel = null;
            }
        }

        /// <summary>
        /// Execute an iteration of the main loop.
        /// </summary>
        protected override void RunPass()
        {
            SelectSockets selectSockets = new SelectSockets();

            // Dispatch before the call to select().
            if (Channel == null) BeforeSelectNoChannel(selectSockets);
            else Channel.BeforeSelect(selectSockets);

            // Block.
            Block(selectSockets);

            // Dispatch after the call to select().
            if (Channel != null) Channel.AfterSelect(selectSockets);
        }

        /// <summary>
        /// Called when there is no channel before the call to select().
        /// </summary>
        private void BeforeSelectNoChannel(SelectSockets selectSockets)
        {
            // Try to connect.
            if (TryConnectFlag)
            {
                TryConnectFlag = false;
                Channel = new EAnpClientThreadChannel(this);
                Channel.BeforeSelect(selectSockets);
            }
        }

        /// <summary>
        /// Return the information required for connecting to the KWM.
        /// </summary>
        public void GetConnectInfo()
        {
            String[] hexSecret = null;
            StringBuilder sb = new StringBuilder();
            String infoPath = KwmPath.GetKwmInfoPath();

            if (!File.Exists(infoPath)) throw new Exception("no KWM information file found");

            StreamReader fsr = null;

            try
            {
                fsr = new StreamReader(new FileStream(infoPath, FileMode.Open, FileAccess.Read,
                                                      FileShare.ReadWrite | FileShare.Delete));
                Port = int.Parse(fsr.ReadLine());
                hexSecret = fsr.ReadLine().Split(new char[1] { ' ' });
            }

            finally
            {
                if (fsr != null) fsr.Close();
            }

            // Convert the secret hexadecimal string to binary.
            if (hexSecret.Length < EAnpBaseThread.SecretLen) throw new Exception("invalid secret string");
            Secret = new byte[EAnpBaseThread.SecretLen];
            for (int i = 0; i < EAnpBaseThread.SecretLen; i++)
            {
                if (hexSecret[i].StartsWith("0x"))
                {
                    Secret[i] = byte.Parse(hexSecret[i].Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    Secret[i] = byte.Parse(hexSecret[i], NumberStyles.HexNumber);
                }
            }
        }

        /// <summary>
        /// Called by the broker to force a connection attempt.
        /// </summary>
        public void RequestConnect()
        {
            TryConnectFlag = true;
        }
    }

    /// <summary>
    /// Thread used by the server broker.
    /// </summary>
    public class EAnpServerThread : EAnpBaseThread
    {
        /// <summary>
        /// Listening socket.
        /// </summary>
        public Socket ListenSock = null;

        /// <summary>
        /// Stream to the information file.
        /// </summary>
        public FileStream InfoStream = null;

        /// <summary>
        /// Tree of channels indexed by channel ID.
        /// </summary>
        public SortedDictionary<UInt64, EAnpServerThreadChannel> ChannelTree = 
            new SortedDictionary<UInt64, EAnpServerThreadChannel>();

        public EAnpServerThread(EAnpServerBroker broker) :
            base(broker)
        { }

        public override EAnpThreadChannel GetChannelByID(UInt64 id)
        {
            if (ChannelTree.ContainsKey(id)) return ChannelTree[id];
            return null;
        }

        public override void RemoveChannel(UInt64 id)
        {
            ChannelTree.Remove(id);
        }

        protected override void Initialize()
        {
            StartListening();
        }

        protected override void CleanUp()
        {
            KSocket.Dispose(ref ListenSock);
            if (InfoStream != null)
            {
                InfoStream.Dispose();
                InfoStream = null;
            }
            SortedDictionary<UInt64, EAnpServerThreadChannel> tree =
                new SortedDictionary<UInt64, EAnpServerThreadChannel>(ChannelTree);
            foreach (EAnpThreadChannel c in tree.Values) c.Close();
            ChannelTree.Clear();
        }

        protected override void RunPass()
        {
            SelectSockets selectSockets = new SelectSockets();
            SortedDictionary<UInt64, EAnpServerThreadChannel> tree =
                new SortedDictionary<UInt64, EAnpServerThreadChannel>(ChannelTree);

            // Dispatch before the call to select().
            selectSockets.AddRead(ListenSock);
            foreach (EAnpThreadChannel c in tree.Values) c.BeforeSelect(selectSockets);

            // Block.
            Block(selectSockets);

            // Dispatch after the call to select().
            foreach (EAnpThreadChannel c in tree.Values) c.AfterSelect(selectSockets);
            AfterSelectListen(selectSockets);
        }

        /// <summary>
        /// Start listening for connections.
        /// </summary>
        private void StartListening()
        {
            // Start to listen.
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 0);
            ListenSock = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            ListenSock.Bind(endPoint);
            ListenSock.Listen(1);
            Port = ((IPEndPoint)ListenSock.LocalEndPoint).Port;

            // Generate the authentication data.
            Secret = new byte[EAnpBaseThread.SecretLen];
            (new RNGCryptoServiceProvider()).GetBytes(Secret);

            // Now we want to write the information file. We have two 
            // requirements:
            // 1) Delete the file automatically when the process is closed.
            // 2) Write the file atomically to avoid race conditions.
            //
            // It turns out you can specify the DeleteOnClose flag for 
            // requirement 1). Of course, with this flag you cannot write,
            // close, and rename the file atomically because you'd lose the
            // file by closing it.
            //
            // Fear not! Microsoft, always at the leading edge of 
            // technology, thought about the problem. "If you request delete
            // permission at the time you create the file, you can delete or 
            // rename the file with that handle but not with any other 
            // handle".
            //
            // Caveat: there is no function to delete or rename a file from 
            // its handle.
            //
            // So, fuck it and live with the race condition. 
            //
            // Format:
            // - port as string.
            // - secret as hexadecimal string.
            String infoPath = KwmPath.GetKwmInfoPath();
            InfoStream = new FileStream(infoPath, FileMode.Create, FileAccess.Write,
                                        FileShare.ReadWrite | FileShare.Delete, 100, FileOptions.DeleteOnClose);
            
            // Now let's jump through hoops to write the data.
            MemoryStream m = new MemoryStream();
            StreamWriter w = new StreamWriter(m);
            w.WriteLine(Port.ToString());
            w.WriteLine(KUtil.HexStr(Secret));
            w.Close();
            byte[] data = m.ToArray();
            InfoStream.Write(data, 0, data.Length);
            InfoStream.Flush();

            // Create and delete the trigger file.
            using (FileStream t = new FileStream(infoPath + ".trigger", FileMode.Create, FileAccess.Write,
                                                 FileShare.Delete, 1, FileOptions.DeleteOnClose))
            { }
        }

        /// <summary>
        /// Accept a connection after the call to select.
        /// </summary>
        private void AfterSelectListen(SelectSockets selectSockets)
        {
            if (!selectSockets.InRead(ListenSock)) return;
            Socket sock = null;

            try
            {
                sock = ListenSock.Accept();
                sock.Blocking = false;
            }

            catch (Exception)
            {
                return;
            }

            EAnpServerThreadChannel c = new EAnpServerThreadChannel(this, sock);
            ChannelTree[c.ChannelID] = c;
        }
    }

    /// <summary>
    /// Thread-level channel.
    /// </summary>
    public abstract class EAnpThreadChannel
    {
        /// <summary>
        /// Reference to the worker thread.
        /// </summary>
        public EAnpBaseThread Worker = null;

        /// <summary>
        /// Status of the channel.
        /// </summary>
        public EAnpThreadChannelStatus Status = EAnpThreadChannelStatus.Initial;

        /// <summary>
        /// Channel ID.
        /// </summary>
        public UInt64 ChannelID = 0;

        /// <summary>
        /// Channel socket.
        /// </summary>
        public Socket Sock = null;

        /// <summary>
        /// ANP transport object.
        /// </summary>
        public AnpTransport Transport = null;

        /// <summary>
        /// Number of bytes of the secret that have been sent/received.
        /// </summary>
        public int SecretPos = 0;

        /// <summary>
        /// Queue of ANP messages to send.
        /// </summary>
        public Queue<AnpMsg> SendQueue = new Queue<AnpMsg>();

        /// <summary>
        /// Reference to the thread secret.
        /// </summary>
        public byte[] Secret { get { return Worker.Secret; } }

        public EAnpThreadChannel(EAnpBaseThread thread)
        {
            Worker = thread;
            ChannelID = Worker.Broker.InternalNextChannelID++;
        }

        /// <summary>
        /// This method should be called when an error occurs with a channel.
        /// </summary>
        public virtual void HandleError(Exception ex)
        {
            if (Status == EAnpThreadChannelStatus.Closed) return;
            KLogging.Log(ex.Message);
            if (Status == EAnpThreadChannelStatus.Open) Worker.NotifyChannelClosed(ChannelID);
            Close();
        }

        /// <summary>
        /// Clean up the resources used by this channel and unlink it from the
        /// thread.
        /// </summary>
        public void Close()
        {
            Status = EAnpThreadChannelStatus.Closed;
            KSocket.Dispose(ref Sock);
            Worker.RemoveChannel(ChannelID);
        }

        /// <summary>
        /// Called before the call to select().
        /// </summary>
        public void BeforeSelect(SelectSockets selectSockets)
        {
            try
            {
                if (Status == EAnpThreadChannelStatus.Initial) BeforeSelectInitial(selectSockets);
                else if (Status == EAnpThreadChannelStatus.Connecting) BeforeSelectConnecting(selectSockets);
                else if (Status == EAnpThreadChannelStatus.Handshake) BeforeSelectHandshake(selectSockets);
                else if (Status == EAnpThreadChannelStatus.Open) BeforeSelectOpen(selectSockets);
            }

            catch (Exception ex)
            {
                HandleError(ex);
                selectSockets.LowerTimeout(0);
            }
        }

        /// <summary>
        /// Called after the call to select().
        /// </summary>
        public void AfterSelect(SelectSockets selectSockets)
        {
            try
            {
                if (Status == EAnpThreadChannelStatus.Handshake) AfterSelectHandshake(selectSockets);
                else if (Status == EAnpThreadChannelStatus.Open) AfterSelectOpen(selectSockets);
            }

            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        // Stubs.
        public virtual void BeforeSelectInitial(SelectSockets selectSockets) { }
        public virtual void BeforeSelectConnecting(SelectSockets selectSockets) { }
        public virtual void BeforeSelectHandshake(SelectSockets selectSockets) { }
        public virtual void AfterSelectHandshake(SelectSockets selectSockets) { }

        /// <summary>
        /// Called when the channel is open before the call to select().
        /// </summary>
        public void BeforeSelectOpen(SelectSockets selectSockets)
        {
            PrepareNextMsgTransfer();
            Transport.UpdateSelect(selectSockets);
        }

        /// <summary>
        /// Called when the channel is open after the call to select().
        /// </summary>
        public void AfterSelectOpen(SelectSockets selectSockets)
        {
            // Perform transfers only if the socket is ready.
            if (!selectSockets.InReadOrWrite(Sock)) return;

            // Build the received message list.
            List<AnpMsg> recvList = new List<AnpMsg>();

            while (true)
            {
                // Prepare a transfer.
                PrepareNextMsgTransfer();

                // Remember if we are sending a message.
                bool sendingFlag = Transport.IsSending;

                // Do transfers.
                Transport.DoXfer();

                // Stop if no message has been received and no message has been sent.
                if (!Transport.DoneReceiving && (!sendingFlag || Transport.IsSending)) break;

                // Add the message received.
                if (Transport.DoneReceiving) recvList.Add(Transport.GetRecv());
            }

            // Dispatch the received messages.
            if (recvList.Count > 0) Worker.NotifyMsgReceived(ChannelID, recvList);
        }

        /// <summary>
        /// Send the next message queued in SendQueue if required. Begin 
        /// receiving a message if required.
        /// </summary>
        public void PrepareNextMsgTransfer()
        {
            if (!Transport.IsSending && SendQueue.Count != 0) Transport.SendMsg(SendQueue.Dequeue());
            if (!Transport.IsReceiving) Transport.BeginRecv();
        }

        /// <summary>
        /// This method should be called when the channel has been opened.
        /// </summary>
        public void HandleChannelOpened()
        {
            Transport = new AnpTransport(Sock);
            Status = EAnpThreadChannelStatus.Open;
            Worker.NotifyChannelOpened(ChannelID);
        }
    }

    /// <summary>
    /// Client thread channel.
    /// </summary>
    public class EAnpClientThreadChannel : EAnpThreadChannel
    {
        public EAnpClientThread ClientThread { get { return Worker as EAnpClientThread; } }

        public EAnpClientThreadChannel(EAnpClientThread thread)
            : base(thread)
        {
        }

        /// <summary>
        /// Try to connect to the KWM.
        /// </summary>
        public override void BeforeSelectInitial(SelectSockets selectSockets)
        {
            // Get the connection info.
            ClientThread.GetConnectInfo();

            // Try to connect.
            Sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Sock.Blocking = false;

            try
            {
                Sock.Connect(new IPEndPoint(IPAddress.Loopback, ClientThread.Port));
            }

            catch (SocketException ex)
            {
                // This exception will always happen after we try async
                // connect. Awesome API there.
                if (ex.SocketErrorCode != SocketError.WouldBlock) throw;
            }

            // We're now connecting.
            Status = EAnpThreadChannelStatus.Connecting;
            selectSockets.LowerTimeout(0);
        }

        /// <summary>
        /// Check if we are connected.
        /// </summary>
        public override void BeforeSelectConnecting(SelectSockets selectSockets)
        {
            // The socket is connected.
            if (Sock.Poll(0, SelectMode.SelectWrite))
            {
                Status = EAnpThreadChannelStatus.Handshake;
                selectSockets.LowerTimeout(0);
            }

            // The connection has failed.
            else if (Sock.Poll(0, SelectMode.SelectError))
            {
                throw new Exception("the connection to the KWM could not be established");
            }

            // Add the socket in the write set and wait forever.
            else
            {
                selectSockets.AddWrite(Sock);
            }
        }

        /// <summary>
        /// Prepare to send handshake data.
        /// </summary>
        public override void BeforeSelectHandshake(SelectSockets selectSockets)
        {
            selectSockets.AddWrite(Sock);
        }

        /// <summary>
        /// Send some handshake data.
        /// </summary>
        public override void AfterSelectHandshake(SelectSockets selectSockets)
        {
            // Determine the number of bytes to send.
            int nbLeft = Secret.Length - SecretPos;
            Debug.Assert(nbLeft != 0);

            // Send the remaining data.
            int r = KSocket.SockWrite(Sock, Secret, SecretPos, nbLeft);

            // We have written some data.
            if (r != -1) SecretPos += r;

            // We're not done yet.
            if (Secret.Length != SecretPos) return;

            // The channel is now open.
            HandleChannelOpened();
        }
    }

    /// <summary>
    /// Server thread channel.
    /// </summary>
    public class EAnpServerThreadChannel : EAnpThreadChannel
    {
        /// <summary>
        /// Number of milliseconds allowed for the handshake to complete.
        /// </summary>
        private const int m_maxHandshakeTime = 5000;

        /// <summary>
        /// Date at which the handshake started.
        /// </summary>
        private DateTime m_handshakeStartTime = DateTime.Now;

        /// <summary>
        /// Secret received from the server.
        /// </summary>
        private byte[] ClientSecret = new byte[EAnpBaseThread.SecretLen];

        public EAnpServerThreadChannel(EAnpServerThread thread, Socket sock)
            : base(thread)
        {
            Sock = sock;
            Transport = new AnpTransport(Sock);
            Status = EAnpThreadChannelStatus.Handshake;
        }

        public override void BeforeSelectHandshake(SelectSockets selectSockets)
        {
            double timeout = GetHandshakeTimeout();
            if (timeout <= 0) throw new Exception("timeout waiting for client authentication data");
            selectSockets.AddRead(Sock);
            selectSockets.LowerTimeout((int)timeout);
        }

        public override void AfterSelectHandshake(SelectSockets selectSockets)
        {
            // Determine the number of bytes to send.
            int nbLeft = Secret.Length - SecretPos;
            Debug.Assert(nbLeft != 0);

            // Read the remaining data.
            int r = KSocket.SockRead(Sock, ClientSecret, SecretPos, nbLeft);

            // We have read some data.
            if (r != -1) SecretPos += r;

            // We're not done yet.
            if (Secret.Length != SecretPos) return;

            // Verify the secret.
            if (!KUtil.ByteArrayEqual(Secret, ClientSecret))
                throw new Exception("invalid authentication data received");

            // The channel is now open.
            HandleChannelOpened();
        }

        /// <summary>
        /// Return the current handshake timeout.
        /// </summary>
        private double GetHandshakeTimeout()
        {
            return m_maxHandshakeTime - (DateTime.Now - m_handshakeStartTime).TotalMilliseconds;
        }
    }
}