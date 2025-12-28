using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using LightInject;

namespace Barotrauma.LuaCs.Services;


public class ServicesProvider : IServicesProvider
{
    private ServiceContainer _serviceContainerInst;
    private ServiceContainer ServiceContainer
    {
        get
        {
            // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
            if (_serviceContainerInst is null)
                _serviceContainerInst = new ServiceContainer();
            return _serviceContainerInst;
        }
    }
    
    private readonly ReaderWriterLockSlim _serviceLock = new();
    
    public void RegisterServiceType<TSvcInterface, TService>(ServiceLifetime lifetime, ILifetime lifetimeInstance = null) where TSvcInterface : class, IService where TService : class, IService, TSvcInterface
    {
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
            ServiceContainer.Register<TSvcInterface, TService>(lifetimeInstance);
            OnServiceRegistered?.Invoke(typeof(TSvcInterface), typeof(TService));
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
            OnServiceRegistered?.Invoke(typeof(TSvcInterface), typeof(TService));
        }
        finally
        {
            _serviceLock.ExitReadLock();
        }
    }

    public void Compile()
    {
        try
        {
            _serviceLock.EnterReadLock();
            ServiceContainer?.Compile();
        }
        finally
        {
            _serviceLock.ExitReadLock();
        }
    }

    public event Action<Type, Type> OnServiceRegistered;
    
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

    [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining)]
    public void DisposeAndReset()
    {
        // Plugins should never be allowed to execute this.
        if (Assembly.GetCallingAssembly() != Assembly.GetExecutingAssembly())
        {
            throw new MethodAccessException(
                $"Assembly {Assembly.GetCallingAssembly().FullName} attempted to call DisposeAllServices().");
        }

        try
        {
            _serviceLock.EnterWriteLock();
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
