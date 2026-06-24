using System;
using System.Runtime.CompilerServices;

public static class FloatCompressor
{
    private const float ToShort = 32767f;
    private const float ToUShort = 65535f;
    private const float FromShort = 1f / 32767f;
    private const float FromUShort = 1f / 65535f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short FloatToShort(float value)
    {
        value = value < -1f ? -1f : value > 1f ? 1f : value;
        return (short)(value * ToShort);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ShortToFloat(short value)
    {
        return value * FromShort;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort FloatToUShort(float value)
    {
        value = value < 0f ? 0f : value > 1f ? 1f : value;
        return (ushort)(value * ToUShort);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float UShortToFloat(ushort value)
    {
        return value * FromUShort;
    }
}