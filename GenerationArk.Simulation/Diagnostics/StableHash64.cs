using System;
using System.Text;

namespace GenerationArk.Simulation.Diagnostics;

public struct StableHash64
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    private ulong _value;

    public StableHash64()
    {
        _value = OffsetBasis;
    }

    public readonly ulong Value => _value;

    public void AddByte(byte value)
    {
        _value ^= value;
        _value *= Prime;
    }

    public void AddBoolean(bool value) => AddByte(value ? (byte)1 : (byte)0);

    public void AddInt32(int value) => AddUInt32(unchecked((uint)value));

    public void AddUInt32(uint value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            AddByte((byte)(value >> shift));
        }
    }

    public void AddInt64(long value) => AddUInt64(unchecked((ulong)value));

    public void AddUInt64(ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            AddByte((byte)(value >> shift));
        }
    }

    public void AddString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        AddUInt64((ulong)bytes.Length);
        foreach (byte item in bytes)
        {
            AddByte(item);
        }
    }
}
