using System;
using System.Collections.Generic;
using System.Threading;
using LightInject;

namespace Barotrauma.LuaCs.Services;

public class ServicesProvider : IServicesProvider
{
    private ServiceContainer _serviceContainer = new();
    
    public void RegisterServiceType<TSvcInterface, TService>(ServiceLifetime lifetime, ILifetime lifetimeInstance = null) where TSvcInterface : class, IService where TService : class, IService, TSvcInterface, new()
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

        _serviceContainer.Register<TSvcInterface, TService>(lifetimeInstance);
    }

    public void RegisterServiceType<TSvcInterface, TService>(string name, ServiceLifetime lifetime,
        ILifetime lifetimeInstance = null) where TSvcInterface : class, IService where TService : class, IService, TSvcInterface, new()
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

        _serviceContainer.Register<TSvcInterface, TService>(name, lifetimeInstance);
    }

    public void UnregisterServiceType<TSvcInterface, TService>() where TSvcInterface : class, IService where TService : class, IService, TSvcInterface, new()
    {
        throw new NotImplementedException();
    }

    public event Action<Type, Type> OnServiceRegistered;
    
    public void InjectServices<T>(T inst) where T : class
    {
        throw new NotImplementedException();
    }

    public bool TryGetService<TSvcInterface>(out IService service, out ServiceLifetime lifetime) where TSvcInterface : class, IService
    {
        throw new NotImplementedException();
    }

    public bool TryGetService<TSvcInterface>(string name, out IService service, out ServiceLifetime lifetime) where TSvcInterface : class, IService
    {
        throw new NotImplementedException();
    }

    public event Action<Type, IService> OnServiceInstanced;
    
    public List<TSvc> GetAllServices<TSvc>() where TSvc : class, IService
    {
        throw new NotImplementedException();
    }

    public void DisposeServicesOfType<TSvc>() where TSvc : class, IService
    {
        throw new NotImplementedException();
    }

    public void DisposeAllServices()
    {
        throw new NotImplementedException();
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
