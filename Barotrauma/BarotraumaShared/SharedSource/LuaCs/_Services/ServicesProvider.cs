using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using LightInject;

namespace Barotrauma.LuaCs;


public class ServicesProvider : IServicesProvider
{
    private ServiceContainer _serviceContainerInst;
    private ServiceContainer ServiceContainer => _serviceContainerInst;
    /// <summary>
    /// Definition: [Key: InterfaceType, Value: ConcreteTypes]
    /// </summary>
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Type>> _systemTypeDefs = new();
    /// <summary>
    /// Definition: [Key: ConcreteType, Value: TypeInstance]
    /// </summary>
    private readonly ConcurrentDictionary<Type, ISystem> _systemInstances = new();
    private readonly ReaderWriterLockSlim _serviceLock = new();

    public ServicesProvider()
    {
        _serviceContainerInst = new ServiceContainer(new ContainerOptions()
        {
            EnablePropertyInjection = false
        });
        
        //_serviceContainerInst.Register<IServicesProvider>((f) => this);
    }
    
    public void RegisterServiceType<TSvcInterface, TService>(ServiceLifetime lifetime, ILifetime lifetimeInstance = null) where TSvcInterface : class, IService where TService : class, IService, TSvcInterface
    {
        // ISystem services must run as a lifetime singleton
        if (typeof(TSvcInterface).IsAssignableTo(typeof(ISystem)))
        {
            lifetimeInstance = new PerContainerLifetime();
            _systemTypeDefs.GetOrAdd(typeof(TSvcInterface), (type) => new ConcurrentBag<Type>());
        }
        
        if (lifetimeInstance is null)
        {
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    lifetimeInstance = new PerContainerLifetime();
                    break;
                case ServiceLifetime.PerThread:
                    lifetimeInstance = new PerThreadLifetime();
                    break;
                // treat these as transient
                case ServiceLifetime.Transient:
                case ServiceLifetime.Invalid:
                case ServiceLifetime.Custom:
                default:
                    lifetimeInstance = null;
                    break;
            }
        }
        
        try
        {
            _serviceLock.EnterReadLock();
            if (lifetimeInstance is not null)
                ServiceContainer.Register<TSvcInterface, TService>(lifetimeInstance);
            else
                ServiceContainer.Register<TSvcInterface, TService>();
        }
        finally
        {
            _serviceLock.ExitReadLock();
        }
    }

    public void RegisterServiceType<TSvcInterface, TService>(string name, ServiceLifetime lifetime,
        ILifetime lifetimeInstance = null) where TSvcInterface : class, IService where TService : class, IService, TSvcInterface
    {
        if (name.IsNullOrWhiteSpace())
        {
            throw new ArgumentNullException($"Tried to register a service of type {typeof(TService).Name} but the name provided is null or empty." );
        }
        
        // ISystem services must run as a lifetime singleton
        if (typeof(TSvcInterface).IsAssignableTo(typeof(ISystem)))
        {
            lifetimeInstance = new PerContainerLifetime();
            _systemTypeDefs.GetOrAdd(typeof(TSvcInterface), (type) => new ConcurrentBag<Type>());
        }
        
        if (lifetimeInstance is null)
        {
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    lifetimeInstance = new PerContainerLifetime();
                    break;
                case ServiceLifetime.PerThread:
                    lifetimeInstance = new PerThreadLifetime();
                    break;
                // treat these as transient
                case ServiceLifetime.Transient:
                case ServiceLifetime.Invalid:
                case ServiceLifetime.Custom:    // lifetime should not be null here
                default:
                    lifetimeInstance = new PerRequestLifeTime();
                    break;
            }
        }

        try
        {
            _serviceLock.EnterReadLock();
            ServiceContainer.Register<TSvcInterface, TService>(name, lifetimeInstance);
        }
        finally
        {
            _serviceLock.ExitReadLock();
        }
    }

    public void RegisterServiceResolver<TSvcInterface>(Func<ServiceContainer, TSvcInterface> factory) where TSvcInterface : class, IService
    {
        try
        {
            _serviceLock.EnterReadLock();
            ServiceContainer.Register<TSvcInterface>(f => factory(ServiceContainer));
        }
        finally
        {
            _serviceLock.ExitReadLock();
        }
    }

    public void CompileAndRun()
    {
        try
        {
            _serviceLock.EnterWriteLock();
            ServiceContainer?.Compile();
            foreach (var typeDef in _systemTypeDefs.Values.SelectMany(type => type))
            {
                if (_systemInstances.ContainsKey(typeDef))
                {
                    continue;
                }
                _systemInstances[typeDef] = (ISystem)ServiceContainer?.TryGetInstance(typeDef);
            }
        }
        finally
        {
            _serviceLock.ExitWriteLock();
        }
    }
    
    public void InjectServices<T>(T inst) where T : class
    {
        try
        {
            _serviceLock.EnterReadLock();
            ServiceContainer.InjectProperties(inst);
        }
        finally
        {
            _serviceLock.ExitReadLock();
        }
    }

    public bool TryGetService<TSvcInterface>(out TSvcInterface service) where TSvcInterface : class, IService
    {
        try
        {
            _serviceLock.EnterReadLock();
            service = ServiceContainer.TryGetInstance<TSvcInterface>();
            return service is not null;
        }
        catch  
        {
            service = null;
            return false;
        }
        finally
        {
            _serviceLock.ExitReadLock();
        }
    }

    public TSvcInterface GetService<TSvcInterface>() where TSvcInterface : class, IService
    {
        try
        {
            _serviceLock.EnterReadLock();
            return ServiceContainer.GetInstance<TSvcInterface>();
        }
        finally
        {
            _serviceLock.ExitReadLock();
        }
    }

    public bool TryGetService<TSvcInterface>(string name, out TSvcInterface service) where TSvcInterface : class, IService
    {
        try
        {
            _serviceLock.EnterReadLock();
            service = ServiceContainer.TryGetInstance<TSvcInterface>(name);
            return service is not null;
        }
        catch
        {
            service = null;
            return false;
        }
        finally
        {
            _serviceLock.ExitReadLock();
        }
    }

    public event Action<Type, IService> OnServiceInstanced;
    
    public ImmutableArray<TSvc> GetAllServices<TSvc>() where TSvc : class, IService
    {
        try
        {
            _serviceLock.EnterReadLock();
            return ServiceContainer.GetAllInstances<TSvc>().ToImmutableArray();
        }
        finally
        {
            _serviceLock.ExitReadLock();
        }
    }

    [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.Synchronized)]
    public void DisposeAndReset()
    {
        // Plugins should never be allowed to execute this.
        if (Assembly.GetCallingAssembly() != Assembly.GetExecutingAssembly())
        {
            throw new MethodAccessException(
                $"Assembly {Assembly.GetCallingAssembly().FullName} attempted to call {nameof(DisposeAndReset)}().");
        }

        try
        {
            _serviceLock.EnterWriteLock();
            foreach (var system in _systemInstances.Values)
            {
                try
                {
                    system.Dispose();
                }
                catch (Exception e)
                {
                    // ignored, no logging services available.
                }
            }
            _systemInstances.Clear();
            _systemTypeDefs.Clear();
            _serviceContainerInst?.Dispose();
            _serviceContainerInst = new ServiceContainer();
        }
        finally
        {
            _serviceLock.ExitWriteLock();
        }
    }
}

public class PerThreadLifetime : ILifetime
{
    private readonly ThreadLocal<object> _instance = new();
    
    public object GetInstance(Func<object> createInstance, Scope scope)
    {
        if (_instance.Value is null)
        {
            var inst = createInstance.Invoke();
            // IDisposable dispatch
            if (inst is IDisposable disposable)
            {
                if (scope is null)
                {
                    throw new InvalidOperationException("Attempt disposable object without a valid scope.");
                }
                scope.TrackInstance(disposable);
            }

            _instance.Value = inst;
        }
        
        return _instance.Value;
    }
}
