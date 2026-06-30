using System;
using System.Text;
using UnityEngine;

public enum ServerPackets {
    welcome = 1,
    udpConfirmed,
    spawnPlayer,
    gameUpdate,
}

public enum ClientPackets {
    welcomeReceived = 1,
    playerInput
}

public class Packet : IDisposable {
    private byte[] _buffer;
    private int _readPos;
    private int _writePos;
    private bool _disposed;
    private readonly bool _isLeased;

    public Packet() {
        _buffer = NetProtocol.pool.Rent(NetProtocol.maxPacketSize);
        _readPos = 0;
        _writePos = 0;
        _isLeased = true;
    }

    public Packet(byte id) : this() {
        Write(id);
    }

    public Packet(byte[] data) {
        _buffer = data;
        _readPos = 0;
        _writePos = data.Length;
        _isLeased = false;
    }

    public Packet(byte[] rentedData, int length) {
        _buffer = rentedData;
        _readPos = 0;
        _writePos = length;
        _isLeased = false;
    }

    #region Meta Functions

    public void SetBytes(byte[] data) {
        if (_buffer == null || _buffer.Length < data.Length) {
            if (_isLeased && _buffer != null) NetProtocol.pool.Return(_buffer);
            _buffer = NetProtocol.pool.Rent(data.Length);
        }
        Buffer.BlockCopy(data, 0, _buffer, 0, data.Length);
        _readPos = 0;
        _writePos = data.Length;
    }

    public void WriteLength() {
        int length = _writePos;
        if (_buffer.Length < _writePos + 4) Grow(4);
        Buffer.BlockCopy(_buffer, 0, _buffer, 4, length);
        NetProtocol.WriteInt32LE(_buffer, 0, length);
        _writePos += 4;
    }

    public void InsertInt(int value) {
        if (_buffer.Length < _writePos + 4) Grow(4);
        Buffer.BlockCopy(_buffer, 0, _buffer, 4, _writePos);
        NetProtocol.WriteInt32LE(_buffer, 0, value);
        _writePos += 4;
    }

    public byte[] ToArray() {
        byte[] result = new byte[_writePos];
        Buffer.BlockCopy(_buffer, 0, result, 0, _writePos);
        return result;
    }

    public int Length() => _writePos;
    public int UnreadLength() => _writePos - _readPos;

    public void Reset(bool shouldReset = true) {
        if (shouldReset) {
            _readPos = 0;
            _writePos = 0;
        }
        else {
            _readPos = Math.Max(0, _readPos - 4);
        }
    }

    private void Grow(int additionalLength) {
        int targetSize = Math.Max(_buffer.Length * 2, _buffer.Length + additionalLength);
        byte[] newBuffer = NetProtocol.pool.Rent(targetSize);
        Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _writePos);
        if (_isLeased) NetProtocol.pool.Return(_buffer);
        _buffer = newBuffer;
    }

    #endregion

    #region Write Data

    private void EnsureWriteSpace(int length) {
        if (_writePos + length > _buffer.Length) Grow(length);
    }

    public void Write(byte value) {
        EnsureWriteSpace(1);
        _buffer[_writePos++] = value;
    }

    public void Write(byte[] value) {
        EnsureWriteSpace(value.Length);
        Buffer.BlockCopy(value, 0, _buffer, _writePos, value.Length);
        _writePos += value.Length;
    }

    public void Write(short value) {
        EnsureWriteSpace(2);
        _buffer[_writePos] = (byte)value;
        _buffer[_writePos + 1] = (byte)(value >> 8);
        _writePos += 2;
    }

    public void Write(ushort value) {
        EnsureWriteSpace(2);
        _buffer[_writePos] = (byte)value;
        _buffer[_writePos + 1] = (byte)(value >> 8);
        _writePos += 2;
    }

    public void Write(sbyte value) => Write((byte)value);

    public void Write(int value) {
        EnsureWriteSpace(4);
        NetProtocol.WriteInt32LE(_buffer, _writePos, value);
        _writePos += 4;
    }

    public void Write(uint value) {
        EnsureWriteSpace(4);
        NetProtocol.WriteUInt32LE(_buffer, _writePos, value);
        _writePos += 4;
    }

    public void Write(long value) {
        EnsureWriteSpace(8);
        ulong val = (ulong)value;
        for (int i = 0; i < 8; i++) _buffer[_writePos + i] = (byte)(val >> (i * 8));
        _writePos += 8;
    }

    public void Write(float value) => Write(BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
    public void Write(double value) => Write(BitConverter.DoubleToInt64Bits(value));
    public void Write(bool value) => Write((byte)(value ? 1 : 0));

    public void Write(string value) {
        if (string.IsNullOrEmpty(value)) {
            Write(0);
            return;
        }
        int byteCount = Encoding.ASCII.GetByteCount(value);
        if (byteCount > NetProtocol.maxStringLength) throw new ArgumentException("String exceeds limits.");
        Write(byteCount);
        EnsureWriteSpace(byteCount);
        Encoding.ASCII.GetBytes(value, 0, value.Length, _buffer, _writePos);
        _writePos += byteCount;
    }

    public void Write(Vector3 value) {
        Write(value.x);
        Write(value.y);
        Write(value.z);
    }

    public void Write(Quaternion value) {
        Write(value.x);
        Write(value.y);
        Write(value.z);
        Write(value.w);
    }

    #endregion

    #region Read Data

    private void AssertCanRead(int length, string typeName) {
        if (_readPos + length > _writePos) {
            throw new InvalidOperationException($"Underflow context reading '{typeName}'.");
        }
    }

    public byte ReadByte(bool moveReadPos = true) {
        AssertCanRead(1, "byte");
        byte value = _buffer[_readPos];
        if (moveReadPos) _readPos += 1;
        return value;
    }

    public sbyte ReadSByte(bool moveReadPos = true) => (sbyte)ReadByte(moveReadPos);

    public byte[] ReadBytes(int length, bool moveReadPos = true) {
        AssertCanRead(length, "byte[]");
        byte[] value = new byte[length];
        Buffer.BlockCopy(_buffer, _readPos, value, 0, length);
        if (moveReadPos) _readPos += length;
        return value;
    }

    public short ReadShort(bool moveReadPos = true) {
        AssertCanRead(2, "short");
        short value = (short)(_buffer[_readPos] | (_buffer[_readPos + 1] << 8));
        if (moveReadPos) _readPos += 2;
        return value;
    }

    public ushort ReadUShort(bool moveReadPos = true) {
        AssertCanRead(2, "ushort");
        ushort value = (ushort)(_buffer[_readPos] | (_buffer[_readPos + 1] << 8));
        if (moveReadPos) _readPos += 2;
        return value;
    }

    public int ReadInt(bool moveReadPos = true) {
        AssertCanRead(4, "int");
        int value = _buffer[_readPos] | (_buffer[_readPos + 1] << 8) | (_buffer[_readPos + 2] << 16) | (_buffer[_readPos + 3] << 24);
        if (moveReadPos) _readPos += 4;
        return value;
    }

    public uint ReadUInt(bool moveReadPos = true) {
        AssertCanRead(4, "uint");
        uint value = (uint)(_buffer[_readPos] | (_buffer[_readPos + 1] << 8) | (_buffer[_readPos + 2] << 16) | (_buffer[_readPos + 3] << 24));
        if (moveReadPos) _readPos += 4;
        return value;
    }

    public long ReadLong(bool moveReadPos = true) {
        AssertCanRead(8, "long");
        ulong value = 0;
        for (int i = 0; i < 8; i++) value |= (ulong)_buffer[_readPos + i] << (i * 8);
        if (moveReadPos) _readPos += 8;
        return (long)value;
    }

    public float ReadFloat(bool moveReadPos = true) {
        int val = ReadInt(moveReadPos);
        return BitConverter.ToSingle(BitConverter.GetBytes(val), 0);
    }

    public double ReadDouble(bool moveReadPos = true) {
        long val = ReadLong(moveReadPos);
        return BitConverter.Int64BitsToDouble(val);
    }

    public bool ReadBool(bool moveReadPos = true) => ReadByte(moveReadPos) != 0;

    public string ReadString(bool moveReadPos = true) {
        int originPos = _readPos;
        int length = ReadInt(true);
        if (length <= 0) return string.Empty;
        if (length > NetProtocol.maxStringLength) throw new InvalidOperationException("Malformed string length.");
        
        AssertCanRead(length, "string");
        string value = Encoding.ASCII.GetString(_buffer, _readPos, length);
        
        if (moveReadPos) _readPos += length;
        else _readPos = originPos;
        
        return value;
    }

    public Vector3 ReadVector3(bool moveReadPos = true) {
        return new Vector3(ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos));
    }

    public Quaternion ReadQuaternion(bool moveReadPos = true) {
        return new Quaternion(ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos));
    }

    #endregion

    #region Disposal

    protected virtual void Dispose(bool disposing) {
        if (!_disposed) {
            if (disposing && _isLeased && _buffer != null) {
                NetProtocol.pool.Return(_buffer);
            }
            _buffer = null;
            _disposed = true;
        }
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}