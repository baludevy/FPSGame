using System.Buffers;
using System.Security.Cryptography;

public static class NetProtocol {
    public const uint magic = 0xC0FFEE42;
    public const int maxPacketSize = 8 * 1024;
    public const int maxStringLength = 1024;
    public const int maxAccumulatedBuffer = 64 * 1024;
    public const int tokenLength = 16;

    public static readonly ArrayPool<byte> pool = ArrayPool<byte>.Shared;

    public static byte[] NewToken() {
        byte[] t = new byte[tokenLength];
        RandomNumberGenerator.Fill(t);
        return t;
    }

    public static bool TokensEqual(byte[] a, byte[] b) {
        if (a == null || b == null || a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    public static void WriteInt32LE(byte[] buffer, int offset, int value) {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    public static void WriteUInt32LE(byte[] buffer, int offset, uint value) {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }
}