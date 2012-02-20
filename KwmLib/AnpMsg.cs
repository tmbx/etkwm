using kcslib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace kwmlib
{
    /// <summary>
    /// Type of an element in an ANP message.
    /// </summary>
    public enum AnpType : byte
    {
        UInt32 = 1,
        UInt64,
        String,
        Bin
    }

    /// <summary>
    /// Exception thrown when an error occurs in an ANP message.
    /// </summary>
    public class AnpException : Exception
    {
        public AnpException(String str) : base(str) { }
        public AnpException(String str, Exception ex) : base(str, ex) { }

        public AnpException(AnpType actualType, AnpType requestedType)
            : base("AnpType is " + actualType.ToString() + ", not " + requestedType.ToString())
        {
        }
    }

    /// <summary>
    /// Element in an ANP message.
    /// </summary>
    public abstract class AnpElement
    {
        /// <summary>
        /// Return the type of the element.
        /// </summary>
        public abstract AnpType GetElType();

        /// <summary>
        /// Return the size of the element when it is added in an ANP message.
        /// </summary>
        public abstract UInt32 GetSize();

        protected virtual void SetUInt32(UInt32 v) { throw new AnpException(GetElType(), AnpType.UInt32); }
        protected virtual UInt32 GetUInt32() { throw new AnpException(GetElType(), AnpType.UInt32); }
        protected virtual void SetUInt64(UInt64 v) { throw new AnpException(GetElType(), AnpType.UInt64); }
        protected virtual UInt64 GetUInt64() { throw new AnpException(GetElType(), AnpType.UInt64); }
        protected virtual void SetString(String v) { throw new AnpException(GetElType(), AnpType.String); }
        protected virtual String GetString() { throw new AnpException(GetElType(), AnpType.String); }
        protected virtual void SetBin(byte[] v) { throw new AnpException(GetElType(), AnpType.Bin); }
        protected virtual byte[] GetBin() { throw new AnpException(GetElType(), AnpType.Bin); }

        public UInt32 UInt32
        {
            get { return GetUInt32(); }
            set { SetUInt32(value); }
        }

        public UInt64 UInt64
        {
            get { return GetUInt64(); }
            set { SetUInt64(value); }
        }

        public String String
        {
            get { return GetString(); }
            set { SetString(value); }
        }

        public Byte[] Bin
        {
            get { return GetBin(); }
            set { SetBin(value); }
        }
    }

    public class AnpU32El : AnpElement
    {
        UInt32 Value;
        public AnpU32El(UInt32 v) { Value = v; }
        public override AnpType GetElType() { return AnpType.UInt32; }
        public override UInt32 GetSize() { return 5; }
        protected override void SetUInt32(UInt32 v) { Value = v; }
        protected override UInt32 GetUInt32() { return Value; }
    }

    public class AnpU64El : AnpElement
    {
        UInt64 Value;
        public AnpU64El(UInt64 v) { Value = v; }
        public override AnpType GetElType() { return AnpType.UInt64; }
        public override UInt32 GetSize() { return 9; }
        protected override void SetUInt64(UInt64 v) { Value = v; }
        protected override UInt64 GetUInt64() { return Value; }
    }

    public class AnpStrEl : AnpElement
    {
        String Value;
        public AnpStrEl(String v) { if (v == null) v = "";  Value = v; }
        public override AnpType GetElType() { return AnpType.String; }
        public override UInt32 GetSize() { return 5 + (UInt32)Value.Length; }
        protected override void SetString(String v) { Value = v; }
        protected override String GetString() { return Value; }
    }

    public class AnpBinEl : AnpElement
    {
        byte[] Value;
        public AnpBinEl(byte[] v) { if (v == null) v = new byte[0]; Value = v; }
        public override AnpType GetElType() { return AnpType.Bin; }
        public override UInt32 GetSize() { return 5 + (UInt32)Value.Length; }
        protected override void SetBin(byte[] v) { Value = v; }
        protected override byte[] GetBin() { return Value; }
    }

    /// <summary>
    /// Represent an ANP message in the KAnp or EAnp protocols.
    /// </summary>
    public class AnpMsg
    {
        /// <summary>
        /// Size of the header, in bytes.
        /// </summary>
        public const int HdrSize = 24;

        /// <summary>
        /// Maximum size of an ANP message.
        /// </summary>
        public const int MaxSize = 100 * 1024 * 1024;

        /// <summary>
        /// Major protocol version.
        /// </summary>
        public UInt32 Major = 0;

        /// <summary>
        /// Minor protocol version.
        /// </summary>
        public UInt32 Minor = 0;

        /// <summary>
        /// Message type.
        /// </summary>
        public UInt32 Type = 0;

        /// <summary>
        /// Message ID.
        /// </summary>
        public UInt64 ID = 0;

        /// <summary>
        /// Size of the payload (total size of the ANP elements).
        /// </summary>
        public UInt32 PayloadSize = 0;

        /// <summary>
        /// List of ANP elements.
        /// </summary>
        public List<AnpElement> Elements = new List<AnpElement>();

        /// <summary>
        /// Add an element to the message.
        /// </summary>
        public void AddElement(AnpElement e)
        {
            Elements.Add(e);
            PayloadSize += e.GetSize();
        }

        public void AddUInt32(UInt32 v) { AddElement(new AnpU32El(v)); }
        public void AddUInt64(UInt64 v) { AddElement(new AnpU64El(v)); }
        public void AddString(String v){ AddElement(new AnpStrEl(v)); }
        public void AddBin(byte[] v) { AddElement(new AnpBinEl(v)); }

        /// <summary>
        /// Format the message, including the header if requested, as a byte 
        /// array.
        /// </summary>
        public byte[] ToByteArray(bool headerFlag)
        {
            MemoryStream s = new MemoryStream();
            BinaryWriter w = new BinaryWriter(s, Encoding.GetEncoding("iso-8859-1"));

            if (headerFlag)
            {
                w.Write(KSocket.Hton(Major));
                w.Write(KSocket.Hton(Minor));
                w.Write(KSocket.Hton(Type));
                w.Write(KSocket.Hton(ID));
                w.Write(KSocket.Hton(PayloadSize));
            }

            foreach (AnpElement e in Elements)
            {
                AnpType t = e.GetElType();
                w.Write((byte)t);

                switch (t)
                {
                    case AnpType.UInt32:
                        w.Write(KSocket.Hton(e.UInt32));
                        break;
                    case AnpType.UInt64:
                        w.Write(KSocket.Hton(e.UInt64));
                        break;
                    case AnpType.String:
                        w.Write(KSocket.Hton((UInt32)e.String.Length));
                        w.Write(e.String.ToCharArray());
                        break;
                    case AnpType.Bin:
                        w.Write(KSocket.Hton((UInt32)e.Bin.Length));
                        w.Write(e.Bin);
                        break;
                }
            }

            return s.ToArray();
        }

        /// <summary>
        /// Retrieve the message data, including the header if requested, from
        /// the byte array specified.
        /// </summary>
        public void FromByteArray(byte[] byteArray, bool headerFlag)
        {
            byte[] payloadArray = byteArray;

            if (headerFlag)
            {
                UInt32 size = 0;
                ParseHdr(byteArray, ref Major, ref Minor, ref Type, ref ID, ref size);

                // C# doesn't have slices.
                payloadArray = new byte[size];
                for (int i = 0; i < size; i++) payloadArray[i] = byteArray[i + HdrSize];
            }

            Elements = ParsePayload(payloadArray);
            PayloadSize = ComputePayloadSize(Elements);
        }

        /// <summary>
        /// Return the size of the payload specified.
        /// </summary>
        public static UInt32 ComputePayloadSize(List<AnpElement> l)
        {
            UInt32 s = 0;
            foreach (AnpElement e in l) s += e.GetSize();
            return s;
        }

        /// <summary>
        /// Parse the header of an ANP message.
        /// </summary>
        public static void ParseHdr(byte[] hdr, ref UInt32 major, ref UInt32 minor, ref UInt32 type, ref UInt64 id, ref UInt32 size)
        {
            try
            {
                BinaryReader r = new BinaryReader(new MemoryStream(hdr));
                major = KSocket.Ntoh(r.ReadUInt32());
                minor = KSocket.Ntoh(r.ReadUInt32());
                type = KSocket.Ntoh(r.ReadUInt32());
                id = KSocket.Ntoh(r.ReadUInt64());
                size = KSocket.Ntoh(r.ReadUInt32());
            }

            catch (Exception ex)
            {
                throw new AnpException("invalid ANP message header", ex);
            }
        }

        /// <summary>
        /// Parse the payload of an ANP message.
        /// </summary>
        public static List<AnpElement> ParsePayload(byte[] payload)
        {
            try
            {
                List<AnpElement> l = new List<AnpElement>();
                MemoryStream s = new MemoryStream(payload);
                BinaryReader r = new BinaryReader(s, Encoding.GetEncoding("iso-8859-1"));

                while (s.Position != s.Length)
                {
                    AnpType t = (AnpType)r.ReadByte();
                    AnpElement e = null;
                    switch (t)
                    {
                        case AnpType.UInt32:
                            e = new AnpU32El(KSocket.Ntoh(r.ReadUInt32()));
                            break;
                        case AnpType.UInt64:
                            e = new AnpU64El(KSocket.Ntoh(r.ReadUInt64()));
                            break;
                        case AnpType.String:
                            e = new AnpStrEl(new String(r.ReadChars((Int32)KSocket.Ntoh((UInt32)r.ReadUInt32()))));
                            break;
                        case AnpType.Bin:
                            e = new AnpBinEl(r.ReadBytes((Int32)KSocket.Ntoh((UInt32)r.ReadUInt32())));
                            break;
                    }
                    l.Add(e);
                }

                return l;
            }

            catch (Exception ex)
            {
                throw new AnpException("invalid ANP message payload", ex);
            }
        }

        /// <summary>
        /// Clear the elements.
        /// </summary>
        public void ClearPayload()
        {
            Elements.Clear();
            PayloadSize = 0;
        }
    }
}