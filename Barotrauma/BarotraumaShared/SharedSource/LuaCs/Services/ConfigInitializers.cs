using System;
using System.Numerics;
using Barotrauma.LuaCs.Configuration;
using Barotrauma.LuaCs.Data;
using FluentResults;
using Microsoft.Xna.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;
using Vector4 = Microsoft.Xna.Framework.Vector4;

namespace Barotrauma.LuaCs.Services;

public class ConfigInitializers : IService
{
    // parameterless .ctor
    public ConfigInitializers()
    {
    }

    public void Dispose()
    {
        // stateless service
        return;
    }

    // stateless service
    public bool IsDisposed => false;

    private Result<IConfigEntry<T>> CreateConfigEntry<T>(IConfigInfo configInfo,
        Action<ConfigEntry<T>, INetReadMessage> readHandler, 
        Action<ConfigEntry<T>, INetWriteMessage> writeHandler)
        where T : IEquatable<T>
    {
        try
        {
            var ice = new ConfigEntry<T>(configInfo, readHandler, writeHandler);
            return FluentResults.Result.Ok<IConfigEntry<T>>(ice);
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail($"Error while initializing config var: {configInfo?.OwnerPackage} - {configInfo?.InternalName}")
                .WithError(new ExceptionalError(e));
        }
    }
    
    private Result<IConfigList<T>> CreateConfigList<T>(IConfigInfo configInfo, 
        Action<IConfigList<T>, INetReadMessage> readHandler, Action<IConfigList<T>, INetWriteMessage> writeHandler) 
        where T : IEquatable<T>
    {
        try
        {
            var icl = new ConfigList<T>(configInfo, readHandler, writeHandler);
            return FluentResults.Result.Ok<IConfigList<T>>(icl);
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail($"Error while initializing config var: {configInfo?.OwnerPackage} - {configInfo?.InternalName}")
                .WithError(new ExceptionalError(e));
        }
    }

    public void RegisterTypeInitializers(IConfigService configService)
    {
        if (configService == null) 
            throw new ArgumentNullException($"{nameof(RegisterTypeInitializers)}: {nameof(IConfigService)} is null.");
        
        configService.RegisterTypeInitializer<bool, IConfigEntry<bool>>(this.CreateConfigBool);
        configService.RegisterTypeInitializer<sbyte, IConfigEntry<sbyte>>(this.CreateConfigSbyte);
        configService.RegisterTypeInitializer<byte, IConfigEntry<byte>>(this.CreateConfigByte);
        configService.RegisterTypeInitializer<short, IConfigEntry<short>>(this.CreateConfigShort);
        configService.RegisterTypeInitializer<ushort, IConfigEntry<ushort>>(this.CreateConfigUShort);
        configService.RegisterTypeInitializer<int, IConfigEntry<int>>(this.CreateConfigInt32);
        configService.RegisterTypeInitializer<uint, IConfigEntry<uint>>(this.CreateConfigUInt32);
        configService.RegisterTypeInitializer<long, IConfigEntry<long>>(this.CreateConfigInt64);
        configService.RegisterTypeInitializer<ulong, IConfigEntry<ulong>>(this.CreateConfigUInt64);
        configService.RegisterTypeInitializer<float, IConfigEntry<float>>(this.CreateConfigFloat32);
        configService.RegisterTypeInitializer<double, IConfigEntry<double>>(this.CreateConfigFloat64);
        configService.RegisterTypeInitializer<decimal, IConfigEntry<decimal>>(this.CreateConfigFloat128);
        configService.RegisterTypeInitializer<char, IConfigEntry<char>>(this.CreateConfigChar);
        configService.RegisterTypeInitializer<string, IConfigEntry<string>>(this.CreateConfigString);
        configService.RegisterTypeInitializer<Color, IConfigEntry<Color>>(this.CreateConfigColor);
        configService.RegisterTypeInitializer<Vector2, IConfigEntry<Vector2>>(this.CreateConfigVector2);
        configService.RegisterTypeInitializer<Vector3, IConfigEntry<Vector3>>(this.CreateConfigVector3);
        configService.RegisterTypeInitializer<Vector4, IConfigEntry<Vector4>>(this.CreateConfigVector4);
    }
        
    
    #region InitializerWrappers_NetworkInjected

    private void AssignValueConditional<T>(T val, IConfigEntry<T> inst) where T : IEquatable<T>
    {
#if SERVER
        if (inst.SyncType is NetSync.None or NetSync.ServerAuthority)
            throw new InvalidOperationException($"[Server] Tried to assign a net value to a type that does not support sync: {inst.SyncType}. Name: {inst.InternalName}, Package: {inst.OwnerPackage.Name}");
        inst.TrySetValue(val);
#else
        if (inst.SyncType is NetSync.None or NetSync.ClientOneWay)
            throw new InvalidOperationException($"[Client] Tried to assign a net value to a type that does not support sync: {inst.SyncType}. Name: {inst.InternalName}, Package: {inst.OwnerPackage.Name}");
        inst.TrySetValue(val);
#endif
    }

    private Result<IConfigEntry<bool>> CreateConfigBool(IConfigInfo configInfo)
    {
        return CreateConfigEntry<bool>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadBoolean(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteBoolean(inst.Value);       
        });
    }

    private Result<IConfigEntry<sbyte>> CreateConfigSbyte(IConfigInfo configInfo)
    {
        return CreateConfigEntry<sbyte>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional((sbyte)readMsg.ReadInt16(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteInt16((short)inst.Value);       
        });
    }

    private Result<IConfigEntry<byte>> CreateConfigByte(IConfigInfo configInfo)
    {
        return CreateConfigEntry<byte>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional((byte)readMsg.ReadUInt16(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteUInt16((byte)inst.Value);       
        });
    }

    private Result<IConfigEntry<short>> CreateConfigShort(IConfigInfo configInfo)
    {
        return CreateConfigEntry<short>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadInt16(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteInt16(inst.Value);       
        });
    }

    private Result<IConfigEntry<ushort>> CreateConfigUShort(IConfigInfo configInfo)
    {
        return CreateConfigEntry<ushort>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadUInt16(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteUInt16(inst.Value);       
        });
    }

    private Result<IConfigEntry<int>> CreateConfigInt32(IConfigInfo configInfo)
    {
        return CreateConfigEntry<int>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadInt32(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteInt32(inst.Value);       
        });
    }

    private Result<IConfigEntry<uint>> CreateConfigUInt32(IConfigInfo configInfo)
    {
        return CreateConfigEntry<uint>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadUInt32(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteUInt32(inst.Value);       
        });
    }

    private Result<IConfigEntry<long>> CreateConfigInt64(IConfigInfo configInfo)
    {
        return CreateConfigEntry<long>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadInt64(), inst);
            
        }, (inst, writeMsg) =>
        {
            writeMsg.WriteInt64(inst.Value);       
        });
    }

    private Result<IConfigEntry<ulong>> CreateConfigUInt64(IConfigInfo configInfo)
    {
        return CreateConfigEntry<ulong>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadUInt64(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteUInt64(inst.Value);       
        });
    }
    
    private Result<IConfigEntry<float>> CreateConfigFloat32(IConfigInfo configInfo)
    {
        return CreateConfigEntry<float>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadSingle(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteSingle(inst.Value);       
        });
    }
    
    private Result<IConfigEntry<double>> CreateConfigFloat64(IConfigInfo configInfo)
    {
        return CreateConfigEntry<double>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadDouble(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteDouble(inst.Value);       
        });
    }
    
    private Result<IConfigEntry<decimal>> CreateConfigFloat128(IConfigInfo configInfo)
    {
        return CreateConfigEntry<decimal>(configInfo, (inst, readMsg) =>
        {
            var decimalArr = new int[4];
            decimalArr[0] = readMsg.ReadInt32();
            decimalArr[1] = readMsg.ReadInt32();
            decimalArr[2] = readMsg.ReadInt32();
            decimalArr[3] = readMsg.ReadInt32();
            AssignValueConditional(new decimal(decimalArr), inst);

        }, (inst, writeMsg) =>
        {
            var decimalArr = Decimal.GetBits(inst.Value);
            writeMsg.WriteInt32(decimalArr[0]);       
            writeMsg.WriteInt32(decimalArr[1]);       
            writeMsg.WriteInt32(decimalArr[2]);       
            writeMsg.WriteInt32(decimalArr[3]);       
        });
    }
    
    private Result<IConfigEntry<char>> CreateConfigChar(IConfigInfo configInfo)
    {
        return CreateConfigEntry<char>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional((char)readMsg.ReadUInt16(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteUInt16(inst.Value);       
        });
    }
    
    private Result<IConfigEntry<string>> CreateConfigString(IConfigInfo configInfo)
    {
        return CreateConfigEntry<string>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadString(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteString(inst.Value);       
        });
    }
    
    private Result<IConfigEntry<Color>> CreateConfigColor(IConfigInfo configInfo)
    {
        return CreateConfigEntry<Color>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(readMsg.ReadColorR8G8B8A8(), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteColorR8G8B8A8(inst.Value);       
        });
    }

    private Result<IConfigEntry<Vector2>> CreateConfigVector2(IConfigInfo configInfo)
    {
        return CreateConfigEntry<Vector2>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(new Vector2(readMsg.ReadSingle(), readMsg.ReadSingle()), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteSingle(inst.Value.X);       
            writeMsg.WriteSingle(inst.Value.Y);       
        });
    }

    private Result<IConfigEntry<Vector3>> CreateConfigVector3(IConfigInfo configInfo)
    {
        return CreateConfigEntry<Vector3>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(new Vector3(readMsg.ReadSingle(), readMsg.ReadSingle(), readMsg.ReadSingle()), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteSingle(inst.Value.X);       
            writeMsg.WriteSingle(inst.Value.Y);       
            writeMsg.WriteSingle(inst.Value.Z);       
        });
    }

    private Result<IConfigEntry<Vector4>> CreateConfigVector4(IConfigInfo configInfo)
    {
        return CreateConfigEntry<Vector4>(configInfo, (inst, readMsg) =>
        {
            AssignValueConditional(new Vector4(
                readMsg.ReadSingle(), 
                readMsg.ReadSingle(),
                readMsg.ReadSingle(), 
                readMsg.ReadSingle()), inst);

        }, (inst, writeMsg) =>
        {
            writeMsg.WriteSingle(inst.Value.X);       
            writeMsg.WriteSingle(inst.Value.Y);       
            writeMsg.WriteSingle(inst.Value.Z);       
            writeMsg.WriteSingle(inst.Value.W);       
        });
    }
    

    #endregion
}
