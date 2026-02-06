using Barotrauma.Extensions;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Compatibility;
using FluentResults;
using FluentResults.LuaCs;
using HarmonyLib;
using Microsoft.Toolkit.Diagnostics;
using MoonSharp.Interpreter;
using OneOf;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Barotrauma.LuaCs;

public partial class EventService : IEventService
{
    private readonly record struct TypeStringKey : IEqualityComparer<TypeStringKey>, IEquatable<TypeStringKey>
    {
        public Type Type { get; init; }
        public string TypeName { get; init; }
        public readonly int HashCode;

        public TypeStringKey(Type type)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            TypeName = type.Name;
            HashCode = TypeName.GetHashCode();
        }

        public TypeStringKey(string typeName)
        {
            Type = null;
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            HashCode = TypeName.GetHashCode();
        }

        public bool Equals(TypeStringKey x, TypeStringKey y)
        {
            if (x.Type is not null && y.Type is not null)
                return x.Type == y.Type;
            return x.TypeName == y.TypeName;
        }

        public int GetHashCode(TypeStringKey obj)
        {
            return obj.HashCode;
        }

        public static implicit operator TypeStringKey(Type type) => new(type);
        public static implicit operator TypeStringKey(string typeName) => new(typeName);
    }

    private readonly ILoggerService _loggerService;
    private readonly ILuaPatcher _luaPatcher;
    private readonly AsyncReaderWriterLock _operationsLock = new();
    private readonly ConcurrentDictionary<TypeStringKey, ConcurrentDictionary<OneOf<IEvent, string>, IEvent>> _subscribers = new();
    private readonly ConcurrentDictionary<TypeStringKey, (TypeStringKey Event, Func<LuaCsFunc, IEvent> RunnerFactory)> _luaAliasEventFactory = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, LuaCsFunc>> _luaLegacyEventsSubscribers = new();

    #region LifeCycle

    public void Dispose()
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
        {
            return;
        }
        
        _luaLegacyEventsSubscribers.Clear();
        _luaAliasEventFactory.Clear();
        _subscribers.Clear();
        _luaPatcher.Dispose();
    }

    private int _isDisposed;

    public EventService(ILoggerService loggerService, ILuaPatcher luaPatcher)
    {
        _loggerService = loggerService;
        _luaPatcher = luaPatcher;
    }

    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }
    public FluentResults.Result Reset()
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        _luaLegacyEventsSubscribers.Clear();
        _luaAliasEventFactory.Clear();
        _subscribers.Clear();
        _luaPatcher.Reset();
        return FluentResults.Result.Ok();
    }

    #endregion

    #region LuaEventSystem

    public void Add(string eventName, string identifier, LuaCsFunc callback)
    {
        Guard.IsNotNullOrWhiteSpace(eventName, nameof(eventName));
        Guard.IsNotNullOrWhiteSpace(identifier, nameof(identifier));
        Guard.IsNotNull(callback, nameof(callback));
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        if (_luaAliasEventFactory.TryGetValue(eventName, out var eventFunc))
        {
            var eventSubs = _subscribers.GetOrAdd(eventFunc.Event, key => new ConcurrentDictionary<OneOf<IEvent, string>, IEvent>());
            eventSubs[identifier] = eventFunc.RunnerFactory(callback);
        }
        else
        {
            var eventSubs = _luaLegacyEventsSubscribers.GetOrAdd(eventName, key => new ConcurrentDictionary<string, LuaCsFunc>());
            eventSubs[identifier] = callback;
        }
    }

    public void Add(string eventName, LuaCsFunc callback)
    {
        // random ident, we hope for no conflicts :barodev:.
        Add(eventName, Random.Shared.NextInt64().ToString() ,callback); 
    }
    
    public object Call(string eventName, params object[] args)
    {
        return Call<object>(eventName, args);
    }

    public T Call<T>(string eventName, params object[] args)
    {
        Guard.IsNotNullOrWhiteSpace(eventName, nameof(eventName));
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        if (!_luaLegacyEventsSubscribers.TryGetValue(eventName, out var eventSubscribers)
            || eventSubscribers.IsEmpty)
        {
            return default;
        }

        T returnValue = default;

        foreach (var subscriber in eventSubscribers)
        {
            try
            {
                object result = subscriber.Value.Invoke(args);
                if (result is DynValue luaResult)
                {
                    if (luaResult.Type == DataType.Tuple)
                    {
                        bool replaceNil = luaResult.Tuple.Length > 1 && luaResult.Tuple[1].CastToBool();

                        if (!luaResult.Tuple[0].IsNil() || replaceNil)
                        {
                            returnValue = luaResult.ToObject<T>();
                        }
                    }
                    else if (!luaResult.IsNil())
                    {
                        returnValue = luaResult.ToObject<T>();
                    }
                }
                else
                {
                    returnValue = (T)result;
                }
            }
            catch (Exception e)
            {
                _loggerService.LogError(e.Message);
#if DEBUG
                throw;
#endif
            }
        }

        return returnValue;
    }

    public void Subscribe<T>(string identifier, IDictionary<string, LuaCsFunc> callbacks) where T : IEvent<T>
    {
        Guard.IsNotNullOrWhiteSpace(identifier, nameof(identifier));
        Guard.IsNotNull(callbacks, nameof(callbacks));
        Guard.IsNotEmpty(callbacks, nameof(callbacks));
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        var eventSubs = _subscribers.GetOrAdd(typeof(T), key => new ConcurrentDictionary<OneOf<IEvent, string>, IEvent>());
        eventSubs[identifier] = T.GetLuaRunner(callbacks);
    }

    public void Remove(string eventName, string identifier)
    {
        Guard.IsNotNullOrWhiteSpace(eventName, nameof(eventName));
        Guard.IsNotNullOrWhiteSpace(identifier, nameof(identifier));
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();

        if (!_subscribers.TryGetValue(eventName, out var evtSubscribers))
        {
            return;
        }
        
        evtSubscribers.TryRemove(identifier, out _);
    }

    public void PublishLuaEvent<T>(LuaCsFunc subscriberRunner) where T : IEvent<T>
    {
        this.PublishEvent<T>(sub => subscriberRunner(sub));
    }

    public FluentResults.Result RegisterLuaEventAlias<T>(string luaEventName, string targetMethod) where T : IEvent<T>
    {
        Guard.IsNotNullOrWhiteSpace(luaEventName, nameof(luaEventName));
        Guard.IsNotNullOrWhiteSpace(targetMethod, nameof(targetMethod));
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        
        if (_luaAliasEventFactory.ContainsKey(luaEventName))
        {
#if DEBUG
            ThrowHelper.ThrowInvalidOperationException($"{nameof(RegisterLuaEventAlias)}: An alias already exists for the event of {luaEventName}.");
#endif   
            return FluentResults.Result.Fail($"{nameof(RegisterLuaEventAlias)}: An alias already exists for the event of {luaEventName}.");
        }
        
        var eventRunnerFactory = (LuaCsFunc function) => (IEvent)T.GetLuaRunner(new Dictionary<string, LuaCsFunc>
        {
            { targetMethod, function }
        });

        _luaAliasEventFactory[luaEventName] = (Event: typeof(T), RunnerFactory: eventRunnerFactory);
        // create the group
        _subscribers.GetOrAdd(typeof(T), key => new ConcurrentDictionary<OneOf<IEvent, string>, IEvent>());
        return FluentResults.Result.Ok();
    }

    #endregion

    public FluentResults.Result Subscribe<T>(T subscriber) where T : class, IEvent<T>
    {
        Guard.IsNotNull(subscriber, nameof(subscriber));
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        var eventSubs =
            _subscribers.GetOrAdd(typeof(T), (type) => new ConcurrentDictionary<OneOf<IEvent, string>, IEvent>());

        if (eventSubs.ContainsKey(subscriber))
        {
            ThrowHelper.ThrowInvalidOperationException($"{nameof(Subscribe)}: The instance is already registered!");
        }

        return eventSubs.TryAdd(subscriber, subscriber)
            ? FluentResults.Result.Ok()
            : FluentResults.Result.Fail($"{nameof(Subscribe)}: Failed to add subscriber.");
    }

    public void Unsubscribe<T>(T subscriber) where T : class, IEvent
    {
        Guard.IsNotNull(subscriber, nameof(subscriber));
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        if (!_subscribers.TryGetValue(typeof(T), out var evtSubscribers))
        {
            return;
        }
        
        evtSubscribers.TryRemove(subscriber, out _);
    }

    public void ClearAllEventSubscribers<T>() where T : IEvent
    {
        using  var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        _subscribers.TryRemove(typeof(T), out _);
    }

    public void ClearAllSubscribers()
    {
        using  var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);
        _subscribers.Clear();
    }

    public FluentResults.Result PublishEvent<T>(Action<T> action) where T : IEvent<T>
    {
        Guard.IsNotNull(action, nameof(action));
        using  var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        IService.CheckDisposed(this);

        if (!_subscribers.TryGetValue(typeof(T), out var subs) || subs.IsEmpty)
        {
            return FluentResults.Result.Ok();
        }

        var results = new FluentResults.Result();

        foreach (var sub in subs)
        {
            try
            {
                action.Invoke((T)sub.Value);
            }
            catch (Exception e)
            {
                results.WithError(new ExceptionalError(e));
                _loggerService.LogError(e.Message);
#if DEBUG
                throw;
#endif
                continue;
            }
        }

        return results;
    }

    #region LuaPatcherAdapter
    public string Patch(string identifier, string className, string methodName, string[] parameterTypes, LuaCsPatchFunc patch, LuaCsHook.HookMethodType hookType = LuaCsHook.HookMethodType.Before)
    {
        return _luaPatcher.Patch(identifier, className, methodName, parameterTypes, patch, hookType);
    }

    public string Patch(string identifier, string className, string methodName, LuaCsPatchFunc patch, LuaCsHook.HookMethodType hookType = LuaCsHook.HookMethodType.Before)
    {
        return _luaPatcher.Patch(identifier, className, methodName, patch, hookType);
    }

    public string Patch(string className, string methodName, string[] parameterTypes, LuaCsPatchFunc patch, LuaCsHook.HookMethodType hookType = LuaCsHook.HookMethodType.Before)
    {
        return _luaPatcher.Patch(className, methodName, parameterTypes, patch, hookType);
    }

    public string Patch(string className, string methodName, LuaCsPatchFunc patch, LuaCsHook.HookMethodType hookType = LuaCsHook.HookMethodType.Before)
    {
        return _luaPatcher.Patch(className, methodName, patch, hookType);
    }

    public bool RemovePatch(string identifier, string className, string methodName, string[] parameterTypes, LuaCsHook.HookMethodType hookType)
    {
        return _luaPatcher.RemovePatch(className, methodName, methodName, parameterTypes, hookType);
    }

    public bool RemovePatch(string identifier, string className, string methodName, LuaCsHook.HookMethodType hookType)
    {
        return _luaPatcher.RemovePatch(className, methodName, methodName, hookType);
    }

    public void HookMethod(string identifier, MethodBase method, LuaCsPatch patch, LuaCsHook.HookMethodType hookType = LuaCsHook.HookMethodType.Before, IAssemblyPlugin owner = null)
    {
        _luaPatcher.HookMethod(identifier, method, patch, hookType, owner);
    }
    #endregion
}
