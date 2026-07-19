namespace GenerationArk.Simulation.Diagnostics;

public sealed class StateChecksumWriter
{
    private StableHash64 _hash = new();

    public ulong Value => _hash.Value;

    public void AddByte(byte value) => _hash.AddByte(value);
    public void AddBoolean(bool value) => _hash.AddBoolean(value);
    public void AddInt32(int value) => _hash.AddInt32(value);
    public void AddUInt32(uint value) => _hash.AddUInt32(value);
    public void AddInt64(long value) => _hash.AddInt64(value);
    public void AddUInt64(ulong value) => _hash.AddUInt64(value);
    public void AddString(string value) => _hash.AddString(value);
}
