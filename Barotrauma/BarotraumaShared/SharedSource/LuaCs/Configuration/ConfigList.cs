using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;
using OneOf;

namespace Barotrauma.LuaCs.Configuration;

public class ConfigList<T> : IConfigList<T> where T : IEquatable<T>
{
    private readonly Action<ConfigList<T>, INetReadMessage> _readMessageHandler;
    private readonly Action<ConfigList<T>, INetWriteMessage> _writeMessageHandler;

    public ConfigList(IConfigInfo configInfo, Action<ConfigList<T>, INetReadMessage> readMessageHandler, 
        Action<ConfigList<T>, INetWriteMessage> writeMessageHandler)
    {
        _readMessageHandler = readMessageHandler;
        _writeMessageHandler = writeMessageHandler;
    }

    public string InternalName => throw new NotImplementedException();

    public ContentPackage OwnerPackage => throw new NotImplementedException();

    public bool Equals(IConfigBase other)
    {
        throw new NotImplementedException();
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

    private event Action<IConfigList<T>> _onValueChanged;
    
    public event Action<IConfigList<T>> OnValueChanged
    {
        add => _onValueChanged += value;
        remove => _onValueChanged -= value;
    }
    
    event Action<IConfigEntry<T>> IConfigEntry<T>.OnValueChanged
    {
        add => _onValueChanged += value;
        remove => _onValueChanged -= value;
    }

    event Action<IConfigBase> IConfigBase.OnValueChanged
    {
        add => _onValueChanged += value;
        remove => _onValueChanged -= value;
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

    public OneOf<string, XElement> GetSerializableValue()
    {
        throw new NotImplementedException();
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

    public IReadOnlyList<T> Options => throw new NotImplementedException();
}
