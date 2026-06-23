using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Sent from server to client.</summary>
public enum ServerPackets 
{
    welcome = 1,
    syncTick,
    spawnPlayer,
    gameUpdate,
    lagCompVisual
}

/// <summary>Sent from client to server.</summary>
public enum ClientPackets 
{
    welcomeReceived = 1,
    syncTick,
    playerInput
}

public class Packet : IDisposable 
{
    private List<byte> buffer;
    private byte[] readableBuffer;
    private int readPos;
    private bool disposed;

    /// <summary>Creates a new empty packet (without an ID).</summary>
    public Packet() 
    {
        buffer = new List<byte>();
        readPos = 0;
    }

    /// <summary>Creates a new packet with a given ID. Used for sending.</summary>
    public Packet(int id) : this()
    {
        Write(id);
    }

    /// <summary>Creates a packet from which data can be read. Used for receiving.</summary>
    public Packet(byte[] data) : this()
    {
        SetBytes(data);
    }

    #region Meta Functions

    /// <summary>Sets the packet's content and prepares it to be read.</summary>
    public void SetBytes(byte[] data) 
    {
        Write(data);
        readableBuffer = buffer.ToArray();
    }

    /// <summary>Inserts the length of the packet's content at the start of the buffer.</summary>
    public void WriteLength() 
    {
        buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));
    }

    /// <summary>Inserts the given int at the start of the buffer.</summary>
    public void InsertInt(int value) 
    {
        buffer.InsertRange(0, BitConverter.GetBytes(value));
    }

    public byte[] ToArray() 
    {
        readableBuffer = buffer.ToArray();
        return readableBuffer;
    }

    public int Length() => buffer.Count;
    public int UnreadLength() => Length() - readPos;

    /// <summary>Resets the packet instance to allow it to be reused.</summary>
    public void Reset(bool shouldReset = true) 
    {
        if (shouldReset) 
        {
            buffer.Clear();
            readableBuffer = null;
            readPos = 0;
        }
        else 
        {
            readPos -= 4; // "Unread" the last read int
        }
    }

    #endregion

    #region Write Data

    public void Write(byte value) => buffer.Add(value);
    public void Write(byte[] value) => buffer.AddRange(value);
    public void Write(short value) => buffer.AddRange(BitConverter.GetBytes(value));
    public void Write(ushort value) => buffer.AddRange(BitConverter.GetBytes(value));
    public void Write(sbyte value) => buffer.Add((byte)value);
    public void Write(int value) => buffer.AddRange(BitConverter.GetBytes(value));
    public void Write(uint value) => buffer.AddRange(BitConverter.GetBytes(value));
    public void Write(long value) => buffer.AddRange(BitConverter.GetBytes(value));
    public void Write(double value) => buffer.AddRange(BitConverter.GetBytes(value));
    public void Write(float value) => buffer.AddRange(BitConverter.GetBytes(value));
    public void Write(bool value) => buffer.AddRange(BitConverter.GetBytes(value));

    public void Write(string value) 
    {
        Write(value.Length); 
        buffer.AddRange(Encoding.ASCII.GetBytes(value));
    }

    public void Write(Vector3 value) 
    {
        Write(value.x);
        Write(value.y);
        Write(value.z);
    }

    public void Write(Quaternion value) 
    {
        Write(value.x);
        Write(value.y);
        Write(value.z);
        Write(value.w);
    }

    #endregion

    #region Read Data

    private void AssertCanRead(int length, string typeName)
    {
        if (readPos + length > buffer.Count)
        {
            throw new Exception($"Could not read value of type '{typeName}'! Buffer underflow.");
        }
    }

    public byte ReadByte(bool moveReadPos = true) 
    {
        AssertCanRead(1, "byte");
        byte value = readableBuffer[readPos];
        if (moveReadPos) readPos += 1;
        return value;
    }

    public sbyte ReadSByte(bool moveReadPos = true) 
    {
        AssertCanRead(1, "sbyte");
        sbyte value = (sbyte)readableBuffer[readPos];
        if (moveReadPos) readPos += 1;
        return value;
    }

    public byte[] ReadBytes(int length, bool moveReadPos = true) 
    {
        AssertCanRead(length, "byte[]");
        byte[] value = buffer.GetRange(readPos, length).ToArray();
        if (moveReadPos) readPos += length;
        return value;
    }

    public short ReadShort(bool moveReadPos = true) 
    {
        AssertCanRead(2, "short");
        short value = BitConverter.ToInt16(readableBuffer, readPos);
        if (moveReadPos) readPos += 2;
        return value;
    }

    public ushort ReadUShort(bool moveReadPos = true) 
    {
        AssertCanRead(2, "ushort");
        ushort value = BitConverter.ToUInt16(readableBuffer, readPos);
        if (moveReadPos) readPos += 2;
        return value;
    }

    public int ReadInt(bool moveReadPos = true) 
    {
        AssertCanRead(4, "int");
        int value = BitConverter.ToInt32(readableBuffer, readPos);
        if (moveReadPos) readPos += 4;
        return value;
    }

    public uint ReadUInt(bool moveReadPos = true) 
    {
        AssertCanRead(4, "uint");
        uint value = BitConverter.ToUInt32(readableBuffer, readPos);
        if (moveReadPos) readPos += 4;
        return value;
    }

    public long ReadLong(bool moveReadPos = true) 
    {
        AssertCanRead(8, "long");
        long value = BitConverter.ToInt64(readableBuffer, readPos);
        if (moveReadPos) readPos += 8;
        return value;
    }

    public double ReadDouble(bool moveReadPos = true) 
    {
        AssertCanRead(8, "double");
        double value = BitConverter.ToDouble(readableBuffer, readPos);
        if (moveReadPos) readPos += 8;
        return value;
    }

    public float ReadFloat(bool moveReadPos = true) 
    {
        AssertCanRead(4, "float");
        float value = BitConverter.ToSingle(readableBuffer, readPos);
        if (moveReadPos) readPos += 4;
        return value;
    }

    public bool ReadBool(bool moveReadPos = true) 
    {
        AssertCanRead(1, "bool");
        bool value = BitConverter.ToBoolean(readableBuffer, readPos);
        if (moveReadPos) readPos += 1;
        return value;
    }

    public string ReadString(bool moveReadPos = true) 
    {
        int length = ReadInt(); 
        AssertCanRead(length, "string");
        
        string value = Encoding.ASCII.GetString(readableBuffer, readPos, length);
        if (moveReadPos && value.Length > 0) 
        {
            readPos += length;
        }
        return value;
    }

    public Vector3 ReadVector3(bool moveReadPos = true) 
    {
        return new Vector3(ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos));
    }

    public Quaternion ReadQuaternion(bool moveReadPos = true) 
    {
        return new Quaternion(ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos));
    }

    #endregion

    #region Disposal

    protected virtual void Dispose(bool disposing) 
    {
        if (!disposed) 
        {
            if (disposing) 
            {
                buffer = null;
                readableBuffer = null;
                readPos = 0;
            }
            disposed = true;
        }
    }

    public void Dispose() 
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}