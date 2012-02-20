using kcslib;
using System;
using System.Diagnostics;
using System.Net.Sockets;

namespace kwmlib
{
    /// <summary>
    /// Manage the mechanic of sending and receiving ANP messages.
    /// </summary>
    public class AnpTransport
    {
        enum InState
        {
            NoMsg,
            RecvHdr,
            RecvPayload,
            Received
        };

        enum OutState
        {
            NoPacket,
            Sending,
        };

        private InState inState = InState.NoMsg;
        private AnpMsg inMsg;
        private byte[] inBuf;
        private int inPos;
        private OutState outState = OutState.NoPacket;
        private byte[] outBuf;
        private int outPos;
        private Socket sock;

        public bool IsReceiving
        {
            get { return (inState != InState.NoMsg); }
        }
        public bool DoneReceiving
        {
            get { return (inState == InState.Received); }
        }
        public bool IsSending
        {
            get { return (outState != OutState.NoPacket); }
        }
        public void Reset()
        {
            FlushRecv();
            FlushSend();
            sock = null;
        }

        public void FlushRecv() { inState = InState.NoMsg; }
        public void FlushSend() { outState = OutState.NoPacket; }

        public AnpTransport(Socket s)
        {
            sock = s;
        }

        public void BeginRecv()
        {
            inState = InState.RecvHdr;
            inBuf = new byte[AnpMsg.HdrSize];
            inPos = 0;
        }

        public AnpMsg GetRecv()
        {
            Debug.Assert(DoneReceiving);
            AnpMsg m = inMsg;
            FlushRecv();
            return m;
        }

        public void SendMsg(AnpMsg msg)
        {
            outState = OutState.Sending;
            outBuf = msg.ToByteArray(true);
            outPos = 0;
        }

        public void DoXfer()
        {
            bool loop = true;

            while (loop)
            {
                loop = false;

                if (inState == InState.RecvHdr)
                {
                    int r = KSocket.SockRead(sock, inBuf, inPos, inBuf.Length - inPos);

                    if (r > 0)
                    {
                        loop = true;
                        inPos += r;

                        if (inPos == inBuf.Length)
                        {
                            inMsg = new AnpMsg();

                            UInt32 size = 0;
                            AnpMsg.ParseHdr(inBuf, ref inMsg.Major, ref inMsg.Minor, ref inMsg.Type, ref inMsg.ID, ref size);

                            if (size > AnpMsg.MaxSize)
                            {
                                throw new AnpException("ANP message is too large");
                            }

                            if (size > 0)
                            {
                                inState = InState.RecvPayload;
                                inBuf = new byte[size];
                                inPos = 0;
                            }

                            else
                            {
                                inState = InState.Received;
                            }
                        }
                    }
                }

                if (inState == InState.RecvPayload)
                {
                    int r = KSocket.SockRead(sock, inBuf, inPos, inBuf.Length - inPos);

                    if (r > 0)
                    {
                        loop = true;
                        inPos += r;

                        if (inPos == inBuf.Length)
                        {
                            inMsg.Elements = AnpMsg.ParsePayload(inBuf);
                            inMsg.PayloadSize = AnpMsg.ComputePayloadSize(inMsg.Elements);
                            inState = InState.Received;
                        }
                    }
                }

                if (outState == OutState.Sending)
                {
                    int r = KSocket.SockWrite(sock, outBuf, outPos, outBuf.Length - outPos);

                    if (r > 0)
                    {
                        loop = true;
                        outPos += r;

                        if (outPos == outBuf.Length)
                        {
                            outState = OutState.NoPacket;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update the select set specified with the socket of the transport.
        /// </summary>
        public void UpdateSelect(SelectSockets selectSocket)
        {
            Debug.Assert(IsReceiving || DoneReceiving);
            if (IsSending) selectSocket.AddWrite(sock);
            if (IsReceiving && !DoneReceiving) selectSocket.AddRead(sock);
        }
    }
}
