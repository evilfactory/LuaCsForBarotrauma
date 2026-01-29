using System;

namespace Barotrauma
{
    public struct LuaSByte
    {
        public readonly sbyte Value;

        public LuaSByte(double v)
        {
            Value = (sbyte)v;
        }

        public LuaSByte(string v, int radix = 10)
        {
            Value = Convert.ToSByte(v, radix);
        }

        public static implicit operator sbyte(LuaSByte luaValue) => luaValue.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public struct LuaByte
    {
        public readonly byte Value;

        public LuaByte(double v)
        {
            Value = (byte)v;
        }

        public LuaByte(string v, int radix = 10)
        {
            Value = Convert.ToByte(v, radix);
        }

        public static implicit operator byte(LuaByte luaValue) => luaValue.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public struct LuaInt16
    {
        public readonly short Value;

        public LuaInt16(double v)
        {
            Value = (short)v;
        }

        public LuaInt16(string v, int radix = 10)
        {
            Value = Convert.ToInt16(v, radix);
        }

        public static implicit operator short(LuaInt16 luaValue) => luaValue.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public struct LuaUInt16
    {
        public readonly ushort Value;

        public LuaUInt16(double v)
        {
            Value = (ushort)v;
        }

        public LuaUInt16(string v, int radix = 10)
        {
            Value = Convert.ToUInt16(v, radix);
        }

        public static implicit operator ushort(LuaUInt16 luaValue) => luaValue.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public struct LuaInt32
    {
        public readonly int Value;

        public LuaInt32(double v)
        {
            Value = (int)v;
        }

        public LuaInt32(string v, int radix = 10)
        {
            Value = Convert.ToInt32(v, radix);
        }

        public static implicit operator int(LuaInt32 luaValue) => luaValue.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public struct LuaUInt32
    {
        public readonly uint Value;

        public LuaUInt32(double v)
        {
            Value = (uint)v;
        }

        public LuaUInt32(string v, int radix = 10)
        {
            Value = Convert.ToUInt32(v, radix);
        }

        public static implicit operator uint(LuaUInt32 luaValue) => luaValue.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public struct LuaInt64
    {
        public readonly long Value;

        public LuaInt64(double v)
        {
            Value = (long)v;
        }

        public LuaInt64(double lo, double hi)
        {
            Value = Convert.ToUInt32(lo) | (long)Convert.ToInt32(hi) << 32;
        }

        public LuaInt64(string v, int radix = 10)
        {
            Value = Convert.ToInt64(v, radix);
        }

        public static implicit operator long(LuaInt64 luaValue) => luaValue.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public struct LuaUInt64
    {
        public readonly ulong Value;

        public LuaUInt64(double v)
        {
            Value = (ulong)v;
        }

        public LuaUInt64(double lo, double hi)
        {
            Value = Convert.ToUInt32(lo) | (ulong)Convert.ToUInt32(hi) << 32;
        }

        public LuaUInt64(string v, int radix = 10)
        {
            Value = Convert.ToUInt64(v, radix);
        }

        public static implicit operator ulong(LuaUInt64 luaValue) => luaValue.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public struct LuaSingle
    {
        public readonly float Value;

        public LuaSingle(double v)
        {
            Value = (float)v;
        }

        public LuaSingle(string v)
        {
            Value = float.Parse(v);
        }

        public static implicit operator float(LuaSingle luaValue) => luaValue.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public struct LuaDouble
    {
        public readonly double Value;

        public LuaDouble(double v)
        {
            Value = v;
        }

        public LuaDouble(string v)
        {
            Value = double.Parse(v);
        }

        public static implicit operator double(LuaDouble luaValue) => luaValue.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
