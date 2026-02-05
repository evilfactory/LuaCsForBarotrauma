using System;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;
using Microsoft.Toolkit.Diagnostics;
using OneOf;

namespace Barotrauma.LuaCs.Configuration;

public class SettingEntry<T> : SettingBase, ISettingBase<T>, INetworkSyncEntity where T : IEquatable<T>, IConvertible
{
    public class Factory : ISettingBase.IFactory<ISettingBase<T>>
    {
        public ISettingBase<T> CreateInstance(IConfigInfo configInfo, Func<OneOf<string, XElement, object>, bool> valueChangePredicate)
        {
            Guard.IsNotNull(configInfo, nameof(configInfo));
            return new SettingEntry<T>(configInfo, valueChangePredicate);
        }
    }
    
    public SettingEntry(IConfigInfo configInfo, 
        Func<OneOf<string, XElement, object>, bool> valueChangePredicate) 
        : base(configInfo)
    {
        if (!( 
                typeof(T).IsEnum || 
                typeof(T).IsPrimitive || 
                typeof(T) == typeof(string)))
        {
            ThrowHelper.ThrowArgumentException($"{nameof(ISettingBase<T>)}: The type of {nameof(T)} is not an allowed type.");
        }
        ValueChangePredicate = valueChangePredicate;
        
        try
        {
            Value = (T)Convert.ChangeType(ConfigInfo.Element.GetAttributeString("Value", null), typeof(T));
        }
        catch (Exception e) when (e is InvalidCastException or ArgumentNullException)
        {
            Value = default(T);
        }
        
        try
        {
            DefaultValue = (T)Convert.ChangeType(ConfigInfo.Element.GetAttributeString("Value", null), typeof(T));
        }
        catch (Exception e) when (e is InvalidCastException or ArgumentNullException)
        {
            DefaultValue = default(T);
        }
    }

    protected Func<OneOf<string, XElement, object>, bool> ValueChangePredicate;
    public T Value { get; protected set; }
    
    public T DefaultValue { get; protected set; }

    public virtual bool TrySetValue(T value)
    {
        if (value is null)
        {
            return false;
        }

        if (ValueChangePredicate != null && !ValueChangePredicate(value))
        {
            return false;
        }
        
        Value = value;
        return true;
    }

    public override Type GetValueType() => typeof(T);

    public override string GetStringValue() => Value.ToString();
    
    public override string GetDefaultStringValue() => DefaultValue.ToString();

    public override bool TrySetValue(OneOf<string, XElement> value)
    {
        bool isFailed = false;
        var typeConvertedValue = value.Match<T>(
            (string val) =>
            {
                try
                {
                    return (T)Convert.ChangeType(val, typeof(T));
                }
                catch (Exception e)
                {
                    // ignored
                    isFailed = true;
                    return default(T);
                }
            },
            (XElement val) =>
            {
                try
                {
                    return (T)Convert.ChangeType(val.GetAttributeString("Value", null), typeof(T));
                }
                catch (Exception e)
                {
                    isFailed = true;
                    return default(T);
                }
            });

        return isFailed || TrySetValue(typeConvertedValue);
    }

    public override OneOf<string, XElement> GetSerializableValue() => Value.ToString();
    
    // -- Networking
    protected IEntityNetworkingService NetworkingService;
    public ulong InstanceId => NetworkingService?.GetNetworkIdForInstance(this) ?? 0ul;
    public void SetNetworkOwner(IEntityNetworkingService networkingService)
    {
        NetworkingService = networkingService;
        if (NetworkingService is null)
        {
            return;
        }
        NetworkingService.RegisterNetVar(this);
    }

    public NetSync SyncType => ConfigInfo.NetSync;
    // needs to be added IConfigInfo
    public ClientPermissions WritePermissions => throw new NotImplementedException();
    public void ReadNetMessage(IReadMessage message)
    {
        if (SyncType == NetSync.None || NetworkingService is null)
        {
            return;
        }
        
        try
        {
            if (typeof(T).IsEnum)
            {
                TrySetValue((T)(object)message.ReadInt32());
            }
            
            // No...there's no better way to do this...
            var typeCode = Type.GetTypeCode(typeof(T));
            switch (typeCode)
            {
                 case TypeCode.Boolean:
                     TrySetValue((T)Convert.ChangeType(message.ReadBoolean(), typeCode));
                     return;
                 case TypeCode.Byte:
                     TrySetValue((T)Convert.ChangeType(message.ReadByte(), typeCode));
                     return;
                 // SByte not supported by interface
                 case TypeCode.SByte:
                     TrySetValue((T)Convert.ChangeType(message.ReadInt16(), typeCode));
                     return;
                 case TypeCode.Int16:
                     TrySetValue((T)Convert.ChangeType(message.ReadInt16(), typeCode));
                     return;
                 case TypeCode.Char:
                 case TypeCode.UInt16:
                     TrySetValue((T)Convert.ChangeType(message.ReadUInt16(), typeCode));
                     return;
                 case TypeCode.Int32:
                     TrySetValue((T)Convert.ChangeType(message.ReadInt32(), typeCode));
                     return;
                 case TypeCode.UInt32:
                     TrySetValue((T)Convert.ChangeType(message.ReadUInt32(), typeCode));
                     return;
                 case TypeCode.Int64:
                     TrySetValue((T)Convert.ChangeType(message.ReadInt64(), typeCode));
                     return;
                 case TypeCode.UInt64:
                     TrySetValue((T)Convert.ChangeType(message.ReadUInt64(), typeCode));
                     return;
                 case TypeCode.Single:
                     TrySetValue((T)Convert.ChangeType(message.ReadSingle(), typeCode));
                     return;
                 case TypeCode.Double:
                     TrySetValue((T)Convert.ChangeType(message.ReadDouble(), typeCode));
                     return;
                 case TypeCode.String:
                     TrySetValue((T)Convert.ChangeType(message.ReadString(), typeCode));
                     return;
                 case TypeCode.Decimal: 
                 default:
                     ThrowHelper.ThrowNotSupportedException($"{nameof(SettingEntry<T>)}: The type {typeof(T).Name} is not supported.");
                     break;
            }
        }
        catch (Exception e)
        {
            // Suppress unless we're testing.
#if DEBUG
            throw;
#endif
        }
    }

    public void WriteNetMessage(IWriteMessage message)
    {
        if (SyncType == NetSync.None || NetworkingService is null)
        {
            return;
        }
        
        try
        {
            if (typeof(T).IsEnum)
            {
                message.WriteInt32((int)((IConvertible)Value));
            }
            
            // No...there's no better way to do this...
            var typeCode = Type.GetTypeCode(typeof(T));
            switch (typeCode)
            {
                 case TypeCode.Boolean:
                     message.WriteBoolean((bool)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.Byte:
                     message.WriteByte((byte)Convert.ChangeType(Value, typeCode)!);
                     return;
                 // SByte not supported by interface
                 case TypeCode.SByte:
                     message.WriteInt16((short)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.Int16:
                     message.WriteInt16((short)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.Char:
                 case TypeCode.UInt16:
                     message.WriteUInt16((ushort)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.Int32:
                     message.WriteInt32((int)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.UInt32:
                     message.WriteUInt32((uint)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.Int64:
                     message.WriteInt64((long)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.UInt64:
                     message.WriteUInt64((ulong)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.Single:
                     message.WriteSingle((float)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.Double:
                     message.WriteDouble((double)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.String:
                     message.WriteString((string)Convert.ChangeType(Value, typeCode)!);
                     return;
                 case TypeCode.Decimal: 
                 default:
                     ThrowHelper.ThrowNotSupportedException($"{nameof(SettingEntry<T>)}: The type {typeof(T).Name} is not supported.");
                     break;
            }
        }
        catch (Exception e)
        {
            // Suppress unless we're testing.
#if DEBUG
            throw;
#endif
        }
    }
}
