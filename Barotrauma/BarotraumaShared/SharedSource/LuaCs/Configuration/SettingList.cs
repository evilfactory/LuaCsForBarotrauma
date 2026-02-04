using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;
using OneOf;

namespace Barotrauma.LuaCs.Configuration;

public class SettingList<T> : ISettingList<T> where T : IEquatable<T>
{
    public string InternalName { get; }
    public ContentPackage OwnerPackage { get; }
    public bool Equals(ISettingBase other)
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

    public string GetStringValue()
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

    public event Func<OneOf<string, XElement>, bool> IsNewValueValid;
    public T Value { get; }
    public bool TrySetValue(T value)
    {
        throw new NotImplementedException();
    }

    public bool IsAssignable(T value)
    {
        throw new NotImplementedException();
    }

    event Action<ISettingList<T>> ISettingList<T>.OnValueChanged
    {
        add => throw new NotImplementedException();
        remove => throw new NotImplementedException();
    }

    public IReadOnlyList<T> Options { get; }

    event Action<ISettingEntry<T>> ISettingEntry<T>.OnValueChanged
    {
        add => throw new NotImplementedException();
        remove => throw new NotImplementedException();
    }

    event Action<ISettingBase> ISettingBase.OnValueChanged
    {
        add => throw new NotImplementedException();
        remove => throw new NotImplementedException();
    }

    public OneOf<string, XElement> GetSerializableValue()
    {
        throw new NotImplementedException();
    }

    public Guid InstanceId { get; }
    public NetSync SyncType { get; }
    public ClientPermissions WritePermissions { get; }
    public void ReadNetMessage(IReadMessage message)
    {
        throw new NotImplementedException();
    }

    public void WriteNetMessage(IWriteMessage message)
    {
        throw new NotImplementedException();
    }
}
