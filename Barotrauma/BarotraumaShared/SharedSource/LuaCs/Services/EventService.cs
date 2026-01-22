using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs.Services.Compatibility;
using FluentResults;
using FluentResults.LuaCs;
using OneOf;

namespace Barotrauma.LuaCs.Services;

public class EventService : IEventService, IEventAssemblyContextUnloading
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
    /// <summary>
    /// <para>Contains subscriber delegates by event and identifier.</para>
    /// Structure:<br/>
    /// - Key: Type or String, TypeName == String Equality.<br/>
    /// - Value: Dictionary<br/>
    /// ---- Key: Either string identifier or subscriber instance pointer<br/>
    /// ---- Value: Subscriber delegate<br/>
    /// </summary>
    private readonly Dictionary<TypeStringKey, Dictionary<OneOf<string, IEvent>, IEvent>> _subscriptions = new();
    private readonly Dictionary<string, string> _eventTypeNameAliases = new();
    private readonly Lazy<IPluginManagementService> _pluginManagementService;
    private readonly Dictionary<TypeStringKey, Action<string, IDictionary<string, LuaCsFunc>>> _luaSubscriptionFactories = new();
    /// <summary>
    /// A collection of factories to produce subscribers from a single lua function handle. For legacy Add() API.
    /// </summary>
    private readonly Dictionary<TypeStringKey, Action<string, LuaCsFunc>> _luaLegacySubscriptionFactories = new();
    /// <summary>
    /// A collection of lua event subscribers from Add() that had neither a valid event name nor an event alias pointing to one.
    /// Only actionable via Call().
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, LuaCsFunc>> _luaOrphanSubscribers = new();

    public EventService(Lazy<IPluginManagementService> pluginManagementService)
    {
        _pluginManagementService = pluginManagementService ?? throw new ArgumentNullException(nameof(pluginManagementService));
        this.Subscribe<IEventAssemblyContextUnloading>(this);
    }

    public bool IsDisposed { get; private set; } = false;
    
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
        ((IService)this).CheckDisposed();
        if (_subscriptions.ContainsKey(eventName) && _subscriptions[eventName].ContainsKey(identifier))
            return true;
        if (_luaOrphanSubscribers.ContainsKey(eventName))
            return true;
        return false;
    }
    
    [Obsolete("Part of the legacy events API, only works for Lua-only custom events.")]
    public T Call<T>(string eventName, params object[] args)
    {
        ((IService)this).CheckDisposed();
        if (!_luaOrphanSubscribers.TryGetValue(eventName, out var dict))
            return default;
        T returnValue = default;
        foreach (var sub in dict.Values)
        {
            try
            {
                var r = sub(args);
                if (r != default)
                    returnValue = (T)r;
            }
            catch
            {
                continue;
            }
        }
        return returnValue;
    }
    
    [Obsolete("Part of the legacy events API, only works for Lua-only custom events.")]
    public object Call(string eventName, params object[] args) => Call<object>(eventName, args);

    #endregion

    public void Add(string eventName, string identifier, LuaCsFunc callback)
    {
        var eventKey = eventName;
        if (_eventTypeNameAliases.TryGetValue(eventName, out var aliasType))
            eventKey = aliasType;
        if (_luaLegacySubscriptionFactories.TryGetValue(eventKey, out var factory))
        {
            factory(identifier, callback);
            return;
        }
        _luaOrphanSubscribers.TryGetOrSet(eventName, () => new Dictionary<string, LuaCsFunc>())
            .Add(identifier.IsNullOrWhiteSpace() ? string.Empty : identifier, callback);
    }

    public void Add(string eventName, LuaCsFunc callback)
    {
        Add(eventName, string.Empty, callback);
    }

    public void Remove(string eventName, string identifier)
    {
        if (_luaOrphanSubscribers.TryGetValue(eventName, out var dict))
            dict.Remove(identifier);
        if (_subscriptions.TryGetValue(eventName, out var dict2))
            dict2.Remove(identifier);
    }

    public void PublishLuaEvent(string interfaceName, LuaCsFunc runner)
    {
        ((IService)this).CheckDisposed();
        if (interfaceName.IsNullOrWhiteSpace())
            return;
        if (!_subscriptions.TryGetValue(interfaceName, out var dict))
            return;
        
        var type = _subscriptions
            .Select(x => x.Key)
            .FirstOrNull(x => x.Type?.Name == interfaceName)?.Type;
        
        var errors = new Queue<IError>();
        foreach (var eventSub in dict.Values)
        {
            try
            {
                runner(type is null ? eventSub : Convert.ChangeType(eventSub, type));   // cast if possible
            }
            catch
            {
                continue;
            }
        }
    }
    
    public FluentResults.Result RegisterSafeEvent<T>() where T : IEvent<T>
    {
        ((IService)this).CheckDisposed();
        var type = typeof(T);
        if (_luaSubscriptionFactories.ContainsKey(type))
            return FluentResults.Result.Ok().WithReason(new Success($"The event {type.Name} is already registered."));
        try
        {
            _luaSubscriptionFactories.Add(type, (ident, funcDict) =>
            {
                var runner = T.GetLuaRunner(funcDict);
                var dict = _subscriptions.TryGetOrSet(type, () => new Dictionary<OneOf<string, IEvent>, IEvent>());
                if (!ident.IsNullOrWhiteSpace())
                    dict[ident] = runner;
                else
                    dict[runner] = runner;
            });
            return FluentResults.Result.Ok();
        }
        catch (NullReferenceException e)
        {
            return FluentResults.Result.Fail(new Error($"The lua runner for {type.Name} is not registered.")
                .WithMetadata(MetadataType.ExceptionObject, this)
                .WithMetadata(MetadataType.RootObject, type));
        }
    }

    public FluentResults.Result UnregisterSafeEvent<T>() where T : IEvent<T>
    {
        ((IService)this).CheckDisposed();
        _luaSubscriptionFactories.Remove(typeof(T));
        if (!_subscriptions.TryGetValue(typeof(T), out var dict)) 
            return FluentResults.Result.Ok();
        dict.Values.Where(value => value.IsLuaRunner()).ToImmutableArray().ForEach(Unsubscribe);
        return FluentResults.Result.Ok();
    }

    // lua subscribe
    public void Subscribe(string interfaceName, string identifier, IDictionary<string, LuaCsFunc> callbacks)
    {
        ((IService)this).CheckDisposed();
        if (_luaSubscriptionFactories.TryGetValue(interfaceName, out var subFactory))
            subFactory(identifier, callbacks);
    }

    public FluentResults.Result SetLegacyLuaRunnerFactory<T>(Func<LuaCsFunc, T> runnerFactory) where T : IEvent<T>
    {
        var type = typeof(T);
        if (!_luaSubscriptionFactories.TryGetValue(type, out var dict))
            return FluentResults.Result.Fail(new Error($"Tried to add legacy lua factory for an event not registered for lua subscriptions."));
        
        _luaLegacySubscriptionFactories[type] = (ident, func) =>
        {
            var runner = runnerFactory(func);
            _subscriptions.TryGetOrSet(type, () => new Dictionary<OneOf<string, IEvent>, IEvent>())[ident] = runner;
        };
        return FluentResults.Result.Ok();
    }

    public void RemoveLegacyLuaRunnerFactory<T>() where T : IEvent<T>
    {
        _luaLegacySubscriptionFactories.Remove(typeof(T));
    }

    public void SetAliasToEvent<T>(string alias) where T : IEvent<T>
    {
        if (alias.IsNullOrWhiteSpace())
            return;
        _eventTypeNameAliases[alias] = typeof(T).Name;
    }

    public void RemoveEventAlias(string alias)
    {
        _eventTypeNameAliases.Remove(alias);
    }

    public void RemoveAllEventAliases<T>() where T : IEvent<T>
    {
        foreach (var keys in _eventTypeNameAliases
                     .Where(kvp => kvp.Value.IsNullOrWhiteSpace() || kvp.Value == typeof(T).Name)
                     .Select(kvp => kvp.Key).ToImmutableArray())
        {
            _eventTypeNameAliases.Remove(keys);
        }
    }

    public FluentResults.Result Subscribe<T>(T subscriber) where T : IEvent<T>
    {
        ((IService)this).CheckDisposed();
        var eventType = typeof(T);
        var dict = _subscriptions.TryGetOrSet(eventType, () => new Dictionary<OneOf<string, IEvent>, IEvent>());
        if (dict.ContainsKey(OneOf<string, IEvent>.FromT1(subscriber)))
        {
            return FluentResults.Result.Fail(
                new Error($"The subscriber for {eventType.Name} is already registered to the event.")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, subscriber));
        }
        dict[subscriber] = subscriber;
        return FluentResults.Result.Ok();
    }

    public void Unsubscribe<T>(T subscriber) where T : IEvent
    {
        ((IService)this).CheckDisposed();
        if (!_subscriptions.TryGetValue(typeof(T), out var dict))
            return;
        dict.Remove(OneOf<string, IEvent>.FromT1(subscriber));
    }

    public void ClearAllEventSubscribers<T>() where T : IEvent 
    {
        _subscriptions.Remove(typeof(T));
        if (typeof(IEventAssemblyContextUnloading) == typeof(T))
        {
            this.Subscribe<IEventAssemblyContextUnloading>(this);
        }
    }
    public void ClearAllSubscribers() 
    {
        _subscriptions.Clear();
        this.Subscribe<IEventAssemblyContextUnloading>(this);
    }

    public FluentResults.Result PublishEvent<T>(Action<T> action) where T : IEvent<T>
    {
        ((IService)this).CheckDisposed();
        var eventType = typeof(T);
        if (!_subscriptions.TryGetValue(eventType, out var dict))
        {
            return FluentResults.Result.Fail(new Error($"The event {eventType.Name} is not registered.")
                .WithMetadata(MetadataType.ExceptionObject, this));
        }

        var errors = new Queue<IError>();
        foreach (var eventSub in dict.Values)
        {
            try
            {
                action((T)eventSub);
            }
            catch (Exception e)
            {
#if DEBUG
                throw; //make errors apparent       
#endif
                errors.Enqueue(new Error($"Error while executing runner for {eventType.Name} on type {eventSub.GetType().Name}.")
                    .WithMetadata(MetadataType.ExceptionObject, this)
                    .WithMetadata(MetadataType.RootObject, eventSub)
                    .WithMetadata(MetadataType.ExceptionDetails, e.Message)
                    .WithMetadata(MetadataType.StackTrace, e.StackTrace));
            }
        }
        
        var result = errors.Count > 0 ? FluentResults.Result.Fail($"Errors while executing event type {eventType.Name}") : FluentResults.Result.Ok();
        while (errors.Count > 0)
            result = result.WithError(errors.Dequeue());
        return result;
    }

    public void Dispose()
    {
        IsDisposed = true;
        _subscriptions.Clear();
        _luaSubscriptionFactories.Clear();
        _eventTypeNameAliases.Clear();
        GC.SuppressFinalize(this);
    }

    public FluentResults.Result Reset()
    {
        ((IService)this).CheckDisposed();
        _subscriptions.Clear();
        _luaSubscriptionFactories.Clear();
        _eventTypeNameAliases.Clear();
        return FluentResults.Result.Ok();
    }

    public void OnAssemblyUnloading(WeakReference<IAssemblyLoaderService> loaderService)
    {
        if (!loaderService.TryGetTarget(out var loader))
            return;
        foreach (var assembly in loader.Assemblies)
        {
            var types = assembly.GetSafeTypes()
                .Where(t => typeof(IEvent).IsAssignableFrom(t))
                .ToImmutableArray();
            if (!types.Any())
                continue;
            foreach (var type in types)
            {
                _subscriptions.Remove(type);
                _luaSubscriptionFactories.Remove(type);
            }
        }
    }
}
