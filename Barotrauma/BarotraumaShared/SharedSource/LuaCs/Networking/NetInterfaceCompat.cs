using Barotrauma.Networking;
using Microsoft.Xna.Framework;

namespace Barotrauma.LuaCs.Services;

#region Wrapper-IWriteMessage

/// <summary>
/// Literally just exists because Barotrauma.IWriteMessage is internal only. 
/// </summary>
public interface INetWriteMessage
{
    internal IWriteMessage Message { get; }
    internal INetWriteMessage SetMessage(IWriteMessage msg);

    void WriteBoolean(bool val) => Message.WriteBoolean(val);

    void WritePadBits() => Message.WritePadBits();

    void WriteByte(byte val) => Message.WriteByte(val);

    void WriteInt16(short val) => Message.WriteInt16(val);

    void WriteUInt16(ushort val) => Message.WriteUInt16(val);

    void WriteInt32(int val) => Message.WriteInt32(val);

    void WriteUInt32(uint val) => Message.WriteUInt32(val);

    void WriteInt64(long val) => Message.WriteInt64(val);

    void WriteUInt64(ulong val) => Message.WriteUInt64(val);

    void WriteSingle(float val) => Message.WriteSingle(val);

    void WriteDouble(double val) => Message.WriteDouble(val);

    void WriteColorR8G8B8(Color val) => Message.WriteColorR8G8B8(val);

    void WriteColorR8G8B8A8(Color val) => Message.WriteColorR8G8B8A8(val);

    void WriteVariableUInt32(uint val) => Message.WriteVariableUInt32(val);

    void WriteString(string val) => Message.WriteString(val);

    void WriteIdentifier(Identifier val) => Message.WriteIdentifier(val);

    void WriteRangedInteger(int val, int min, int max) => Message.WriteRangedInteger(val, min, max);

    void WriteRangedSingle(float val, float min, float max, int bitCount) =>
        Message.WriteRangedSingle(val, min, max, bitCount);

    void WriteBytes(byte[] val, int startIndex, int length) => Message.WriteBytes(val, startIndex, length);

    byte[] PrepareForSending(bool compressPastThreshold, out bool isCompressed, out int outLength) =>
        Message.PrepareForSending(compressPastThreshold, out isCompressed, out outLength);

    int BitPosition
    {
        get => Message.BitPosition;
        set => Message.BitPosition = value;
    }

    int BytePosition => Message.BytePosition;

    byte[] Buffer => Message.Buffer;

    int LengthBits
    {
        get => Message.LengthBits;
        set => Message.LengthBits = value;
    }

    int LengthBytes => Message.LengthBytes;
}

#endregion

#region Wrapper-IReadMessage

/// <summary>
/// Literally just exists because Barotrauma.IReadMessage is internal only. 
/// </summary>
public interface INetReadMessage
{
    internal IReadMessage Message { get; }
    internal INetReadMessage SetMessage(IReadMessage msg);

    bool ReadBoolean() => Message.ReadBoolean();
    void ReadPadBits() => Message.ReadPadBits();
    byte ReadByte() => Message.ReadByte();
    byte PeekByte() => Message.PeekByte();
    ushort ReadUInt16() => Message.ReadUInt16();
    short ReadInt16() => Message.ReadInt16();
    uint ReadUInt32() => Message.ReadUInt32();
    int ReadInt32() => Message.ReadInt32();
    ulong ReadUInt64() => Message.ReadUInt64();
    long ReadInt64() => Message.ReadInt64();
    float ReadSingle() => Message.ReadSingle();
    double ReadDouble() => Message.ReadDouble();
    uint ReadVariableUInt32() => Message.ReadVariableUInt32();
    string ReadString() => Message.ReadString();
    Identifier ReadIdentifier() => Message.ReadIdentifier();
    Color ReadColorR8G8B8() => Message.ReadColorR8G8B8();
    Color ReadColorR8G8B8A8() => Message.ReadColorR8G8B8A8();
    int ReadRangedInteger(int min, int max) => Message.ReadRangedInteger(min, max);
    float ReadRangedSingle(float min, float max, int bitCount) => Message.ReadRangedSingle(min, max, bitCount);
    byte[] ReadBytes(int numberOfBytes) => Message.ReadBytes(numberOfBytes);
    int BitPosition
    {
        get => Message.BitPosition;
        set => Message.BitPosition = value;
    }
    int BytePosition => Message.BytePosition;
    byte[] Buffer => Message.Buffer;
    int LengthBits
    {
        get => Message.LengthBits;
        set => Message.LengthBits = value;
    }
    int LengthBytes => Message.LengthBytes;
}

#endregion

#region HelperImplementations

public class NetWriteMessage : INetWriteMessage
{
    private IWriteMessage Message { get; set; }

    IWriteMessage INetWriteMessage.Message => Message;

    INetWriteMessage INetWriteMessage.SetMessage(IWriteMessage msg)
    {
        Message = msg;
        return this;
    }
}

internal static class NetHelperExtensions
{
    internal static INetWriteMessage ToNetWriteMessage(this IWriteMessage msg) =>
        ((INetWriteMessage)new NetWriteMessage()).SetMessage(msg);
    internal static INetReadMessage ToNetReadMessage(this IReadMessage msg) =>
        ((INetReadMessage)new NetReadMessage()).SetMessage(msg);
}

public class NetReadMessage : INetReadMessage
{
    private IReadMessage Message { get; set; }
    IReadMessage INetReadMessage.Message => Message;

    INetReadMessage INetReadMessage.SetMessage(IReadMessage msg)
    {
        Message = msg;
        return this;
    }
}

#endregion
