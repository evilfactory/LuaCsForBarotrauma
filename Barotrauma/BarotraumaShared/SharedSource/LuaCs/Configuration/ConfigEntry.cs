using System;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;
using OneOf;

namespace Barotrauma.LuaCs.Configuration;

public class ConfigEntry<T> : IConfigEntry<T>, INetworkSyncEntity where T : IEquatable<T>
{
    
    private readonly Action<ConfigEntry<T>, INetReadMessage> _readMessageHandler;
    private readonly Action<ConfigEntry<T>, INetWriteMessage> _writeMessageHandler;
    private IEntityNetworkingService _networkingService;
    
    public ConfigEntry(IConfigInfo configInfo, Action<ConfigEntry<T>, INetReadMessage> readMessageHandler, 
        Action<ConfigEntry<T>, INetWriteMessage> writeMessageHandler, IEntityNetworkingService networkingService)
    {
        _readMessageHandler = readMessageHandler;
        _writeMessageHandler = writeMessageHandler;
        _networkingService = networkingService;
    }
        
    public string InternalName { get; init; }
    public ContentPackage OwnerPackage { get; init; }
    
    public bool Equals(IConfigBase other)
    {
        if (ReferenceEquals(this, other))
            return true;
        
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Type GetValueType()
    {
        throw new NotImplementedException();
    }

    public string GetValue()
    {
        throw new NotImplementedException();
    }

    public bool TrySetValue(OneOf<string, XElement> value)
    {
        throw new NotImplementedException();
    }

    public bool IsAssignable(OneOf<string, XElement> value)
    {
        throw new NotImplementedException();
    }

    private event Action<IConfigEntry<T>> _onValueChanged;
    public event Action<IConfigEntry<T>> OnValueChanged
    {
        add => _onValueChanged += value;
        remove => _onValueChanged -= value;
    }

    event Action<IConfigBase> IConfigBase.OnValueChanged
    {
        add => _onValueChanged += value;
        remove => _onValueChanged -= value;
    }

    public Guid InstanceId => throw new NotImplementedException();

    public NetSync SyncType => throw new NotImplementedException();

    public ClientPermissions WritePermissions => throw new NotImplementedException();

    public void ReadNetMessage(INetReadMessage message)
    {
        throw new NotImplementedException();
    }

    public void WriteNetMessage(INetWriteMessage message)
    {
        throw new NotImplementedException();
    }

    public T Value => throw new NotImplementedException();

    public bool TrySetValue(T value)
    {
        throw new NotImplementedException();
    }

    public bool IsAssignable(T value)
    {
        throw new NotImplementedException();
    }
}
