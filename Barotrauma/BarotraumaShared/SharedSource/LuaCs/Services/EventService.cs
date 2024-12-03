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
using OneOf;
using QuikGraph.Algorithms.Observers;

namespace Barotrauma.LuaCs.Services;

public class EventService : IEventService, IEventAssemblyLoaded, IEventAssemblyContextUnloading
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

    public class LuaCallbackContainer
    {
        public LuaCsFunc Callback;

        public LuaCallbackContainer(LuaCsFunc callback)
        {
            Callback = callback;
        }
    }

    private readonly ILoggerService _loggerService;
    private Dictionary<string, Dictionary<string, LuaCsFunc>> luaEvents = new();
    private Dictionary<Type, List<object>> eventSubscribers = new();

    private Dictionary<TypeStringKey, Type> eventAssemblyTypes = new();

    #region PublicAPI

    #region Compatibility

    [Obsolete("Use ILuaEventService.Add() instead.")]
    void ILuaCsHook.Add(string eventName, string identifier, LuaCsFunc callback, ACsMod mod = null)
    {
        Add(eventName, identifier, callback);
    }
    [Obsolete("Use ILuaEventService.Add() instead.")]
    void ILuaCsHook.Add(string eventName, LuaCsFunc callback, ACsMod mod = null)
    {
        Add(eventName, callback);
    }

    #endregion

    #region DynamicHooks

    public void Add(string eventName, string identifier, LuaCsFunc callback)
    {
        if (eventAssemblyTypes.TryGetValue(eventName, out Type type))
        {
            if (!eventSubscribers.ContainsKey(type))
            {
                eventSubscribers.Add(type, new List<object>());
            }

            // how eventSubscribers[type].Add(observer);
        }
        else
        {
            if (!luaEvents.ContainsKey(eventName))
            {
                luaEvents.Add(eventName, new Dictionary<string, LuaCsFunc>());
            }

            luaEvents[eventName].Add(identifier, callback);
        }
    }

    public void Add(string eventName, LuaCsFunc callback)
    {
        Add(eventName, "", callback);
    }

    public bool Exists(string eventName, string identifier)
    {
        return luaEvents.ContainsKey(eventName) && luaEvents[eventName].ContainsKey(identifier);
    }

    public void Remove(string eventName, string identifier)
    {
        if (!luaEvents.ContainsKey(eventName)) { return; }

        luaEvents[eventName].Remove(identifier);
    }

    public T Call<T>(string eventName, params object[] args)
    {
        if (!luaEvents.ContainsKey(eventName)) { return default; }

        foreach (var luaEvent in luaEvents[eventName])
        {
            try
            {
                return (T)luaEvent.Value(args);
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Error invoking Lua event '{eventName}': {e}");
            }
        }

        return default;
    }

    public object Call(string eventName, params object[] args) => Call<object>(eventName, args);

    #endregion

    #region TypeEventSystem

    public void ClearAllEventSubscribers<T>() where T : IEvent
    {
        eventSubscribers.Remove(typeof(T));
    }

    public void ClearAllSubscribers()
    {
        eventSubscribers = new Dictionary<Type, List<object>>();
    }

    public void SubscribeAll(object observer)
    {
        foreach (var type in observer.GetType().GetInterfaces().Where(inter => inter.IsAssignableTo(typeof(IEvent))))
        {
            if (!eventSubscribers.ContainsKey(type))
            {
                eventSubscribers.Add(type, new List<object>());
            }

            eventSubscribers[type].Add(observer);
        }
    }

    public FluentResults.Result PublishEvent<T>(Action<T> eventInvoker) where T : IEvent
    {
        if (!eventSubscribers.ContainsKey(typeof(T))) { return FluentResults.Result.Ok(); }

        foreach (var subscriber in eventSubscribers[typeof(T)])
        {
            try
            {
                eventInvoker((T)subscriber);
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Error invoking event '{typeof(T).Name}': {e}");
            }
        }

        return FluentResults.Result.Ok();
    }

    #endregion

    #endregion

    #region InternalAPI

    private static FluentResults.Result<Delegate> BuildExecutionDynamicDelegate(Type delegateType, string callbackMethodName, Action<object[]> callback)
    {
        try
        {
            var delMethodInfo = delegateType.GetMethod(callbackMethodName);
            if (delMethodInfo == null)
                return FluentResults.Result.Fail("Delegate " + callbackMethodName + " not found");
            var paramsInfo = delMethodInfo.GetParameters()
                .Select(pInfo => Expression.Parameter(pInfo.ParameterType, pInfo.Name)).ToImmutableArray();
            var inst = callback.Target is null ? null : Expression.Constant(callback.Target);
            var convertedExpression = paramsInfo.Select(pInfo => Expression.Convert(pInfo, typeof(object)));
            var call = Expression.Call(inst, callback.Method,
                Expression.NewArrayInit(typeof(object), convertedExpression));
            var rType = delMethodInfo.ReturnType == typeof(void)
                ? (Expression)call
                : Expression.Convert(call, delMethodInfo.ReturnType);
            var expression = Expression.Lambda(delegateType, rType, paramsInfo).Compile();
            return new FluentResults.Result<Delegate>()
                .WithValue(expression);
        }
        catch (Exception e)
        {
            return FluentResults.Result.Fail(new ExceptionalError(e));
        }
    }

    public void Dispose()
    {
        luaEvents = null;
        eventSubscribers = null;
    }

    public FluentResults.Result Reset()
    {
        luaEvents = new Dictionary<string, Dictionary<string, LuaCsFunc>>();
        ClearAllSubscribers();
        return FluentResults.Result.Ok();
    }

    public void OnAssemblyLoaded(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(IEvent))))
        {
            eventAssemblyTypes.Add(type.Name, type);
        }
    }

    public void OnAssemblyUnloading(WeakReference<IAssemblyLoaderService> loaderService)
    {
        if (loaderService.TryGetTarget(out var svc))
        {
            foreach (Type type in svc.Assemblies.SelectMany(assembly => assembly.GetTypes()))
            {
                if (eventAssemblyTypes.ContainsKey(type))
                {
                    eventAssemblyTypes.Remove(type);
                }
            }
        }
    }

    #endregion

    #region ClassFunctions

    public EventService(ILoggerService loggerService)
    {
        _loggerService = loggerService;
    }
    
    #endregion
}
