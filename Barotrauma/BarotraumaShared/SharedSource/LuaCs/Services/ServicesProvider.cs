using System;
using System.Collections.Generic;
using LightInject;

namespace Barotrauma.LuaCs.Services;

public class ServicesProvider : IServicesProvider
{
    
    
    public void RegisterServiceType<TSvcInterface, TService>(ServiceLifetime lifetime, ILifetime lifetimeInstance = null) where TSvcInterface : class, IService where TService : class, IService
    {
        throw new NotImplementedException();
    }

    public void UnregisterServiceType<TSvcInterface, TService>() where TSvcInterface : class, IService where TService : class, IService
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
