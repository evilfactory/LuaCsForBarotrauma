using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services.Compatibility;
using Barotrauma.LuaCs.Services.Safe;
using FluentResults;

namespace Barotrauma.LuaCs.Services;

public class EventService : IEventService
{
    private readonly record struct TypeStringKey : IEqualityComparer<TypeStringKey>, IEquatable<TypeStringKey>
    {
        public Type Type { get; init; }
        public string TypeName { get; init; }
        public readonly int HashCode;

        public TypeStringKey(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));
            Type = type;
            TypeName = type.Name;
            HashCode = TypeName.GetHashCode();
        }

        public TypeStringKey(string typeName)
        {
            if (typeName is null)
                throw new ArgumentNullException(nameof(typeName));
            Type = null;
            TypeName = typeName;
            HashCode = TypeName.GetHashCode();
        }

        public bool Equals(TypeStringKey x, TypeStringKey y)
        {
            return x.Type == y.Type || x.TypeName == y.TypeName;
        }

        public int GetHashCode(TypeStringKey obj)
        {
            return obj.HashCode;
        }

        public static implicit operator TypeStringKey(Type type) => new(type);
        public static implicit operator TypeStringKey(string typeName) => new(typeName);
    }

    #region Compatibility

    [Obsolete("ACsMod is deprecated. Use ILuaEventService.Add() instead.")]
    void ILuaCsHook.Add(string eventName, string identifier, LuaCsFunc callback, ACsMod mod = null)
    {
        Add(eventName, identifier, callback);
    }
    [Obsolete("ACsMod is deprecated. Use ILuaEventService.Add() instead.")]
    void ILuaCsHook.Add(string eventName, LuaCsFunc callback, ACsMod mod = null)
    {
        Add(eventName, callback);
    }

    public bool Exists(string eventName, string identifier)
    {
        throw new NotImplementedException();
    }

    public T Call<T>(string eventName, params object[] args)
    {
        throw new NotImplementedException();
    }

    public object Call(string eventName, params object[] args)
    {
        throw new NotImplementedException();
    }

    #endregion

    public void Add(string eventName, string identifier, LuaCsFunc callback)
    {
        throw new NotImplementedException();
    }

    public void Add(string eventName, LuaCsFunc callback)
    {
        throw new NotImplementedException();
    }

    public void Remove(string eventName, string identifier)
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result PublishLuaEvent(string interfaceName, LuaCsFunc runner)
    {
        throw new NotImplementedException();
    }

    public void ClearAllEventSubscribers<T>() where T : IEvent<T>
    {
        throw new NotImplementedException();
    }

    public void ClearAllSubscribers()
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result PublishEvent<T>(Action<T> action) where T : IEvent<T>
    {
        throw new NotImplementedException();
    }
    
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result Reset()
    {
        throw new NotImplementedException();
    }
}
