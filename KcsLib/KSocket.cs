using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;

namespace kcslib
{
    /// <summary>
    /// Socket handling methods.
    /// </summary>
    public static class KSocket
    {
        public static UInt32 Hton(UInt32 v)
        {
            return (UInt32)IPAddress.HostToNetworkOrder((Int32)v);
        }

        public static UInt32 Ntoh(UInt32 v)
        {
            return (UInt32)IPAddress.NetworkToHostOrder((Int32)v);
        }

        public static UInt64 Hton(UInt64 v)
        {
            return (UInt64)IPAddress.HostToNetworkOrder((Int64)v);
        }

        public static UInt64 Ntoh(UInt64 v)
        {
            return (UInt64)IPAddress.NetworkToHostOrder((Int64)v);
        }

        /// <summary>
        /// Create 2 sockets connected to each other, for communication between threads.
        /// </summary>
        public static Socket[] SocketPair()
        {
            Socket Listener;
            Socket[] Pair = new Socket[2];

            // Start the listener side.
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 0);
            Listener = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Listener.Bind(endPoint);
            Listener.Listen(1);
            IAsyncResult ServerResult = Listener.BeginAccept(null, null);

            // Connect the client to the server.
            endPoint = new IPEndPoint(IPAddress.Loopback, ((IPEndPoint)Listener.LocalEndPoint).Port);
            Pair[0] = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            IAsyncResult ClientResult = Pair[0].BeginConnect(endPoint, null, null);

            // Get the server side socket.
            Pair[1] = Listener.EndAccept(ServerResult);
            Pair[0].EndConnect(ClientResult);

            Listener.Close();

            Pair[0].Blocking = false;
            Pair[1].Blocking = false;

            return Pair;
        }

        /// <summary>
        /// Read data from the socket specfied. Return -1 if the socket would
        /// block, otherwise return the number of bytes read.
        /// </summary>
        public static int SockRead(Socket sock, byte[] buf, int pos, int count)
        {
            if (count == 0) return 0;

            SocketError error;
            int r = sock.Receive(buf, pos, count, SocketFlags.None, out error);
            if (error == SocketError.WouldBlock) return -1;
            else if (error != SocketError.Success) throw new SocketException((int)error);
            else if (r == 0) throw new Exception("lost connection");
            return r;
        }

        /// <summary>
        /// Write data to the socket specfied. Return -1 if the socket would
        /// block, otherwise return the number of bytes written.
        /// </summary>
        public static int SockWrite(Socket sock, byte[] buf, int pos, int count)
        {
            if (count == 0) return 0;

            SocketError error;
            int r = sock.Send(buf, pos, count, SocketFlags.None, out error);
            if (error == SocketError.WouldBlock) return -1;
            else if (error != SocketError.Success) throw new SocketException((int)error);
            if (r == 0) throw new Exception("lost connection");
            return r;
        }

        /// <summary>
        /// Close a socket if it is open and set its reference to null.
        /// </summary>
        public static void Dispose(ref Socket sock)
        {
            if (sock != null)
            {
                try
                {
                    sock.Close();
                    sock = null;
                }

                catch (Exception)
                {
                }
            }
        }
    }

    /// By supplying -2 instead of -1 as the timeout value to Socket.Select,
    /// it will result in the timeout for the underlying winsock select call 
    /// to be more than 1 hour. Windows crap workaround.
    /// http://msdn.microsoft.com/en-us/library/system.net.sockets.socket.select.aspx

    public class SelectSockets
    {
        public ArrayList ReadSockets = new ArrayList();
        public ArrayList WriteSockets = new ArrayList();
        public ArrayList ErrorSockets = new ArrayList();

        /// <summary>
        /// Timeout, in microseconds. -2 means infinity.
        /// </summary>
        public int Timeout = -2;

        private void Add(ArrayList l, Socket sock)
        {
            if (!ErrorSockets.Contains(sock))
            {
                l.Add(sock);
                ErrorSockets.Add(sock);
            }
            else if (!l.Contains(sock))
            {
                l.Add(sock);
            }
        }

        public void AddRead(Socket sock)
        {
            Add(ReadSockets, sock);
        }

        public void AddWrite(Socket sock)
        {
            Add(WriteSockets, sock);
        }

        public void AddRW(Socket sock)
        {
            AddRead(sock);
            AddWrite(sock);
        }

        public bool InRead(Socket sock)
        {
            return ReadSockets.Contains(sock) || ErrorSockets.Contains(sock);
        }

        public bool InWrite(Socket sock)
        {
            return WriteSockets.Contains(sock) || ErrorSockets.Contains(sock);
        }

        public bool InReadOrWrite(Socket sock)
        {
            return InRead(sock) || InWrite(sock);
        }

        /// <summary>
        /// Lower the value of the timeout to the value specified in 
        /// milliseconds, if needed.
        /// </summary>
        public void LowerTimeout(int value)
        {
            if (value == -2) return;
            else if (Timeout == -2) Timeout = value * 1000;
            else Timeout = Math.Min(Timeout, value * 1000);
        }

        public void Select()
        {
            Socket.Select(
                ReadSockets.Count > 0 ? ReadSockets : null,
                WriteSockets.Count > 0 ? WriteSockets : null,
                ErrorSockets.Count > 0 ? ErrorSockets : null,
                Timeout);
        }
    }
}