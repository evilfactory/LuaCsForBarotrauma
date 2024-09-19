using System;
using System.Collections.Generic;
using LightInject;

namespace Barotrauma.LuaCs.Services;

/// <summary>
/// Provides instancing and management of IServices.
/// </summary>
public interface IServicesProvider
{
    #region Type_Registration

    /// <summary>
    /// Registers a type as a service for a given interface.
    /// </summary>
    /// <param name="lifetime"></param>
    /// <param name="lifetimeInstance"></param>
    /// <typeparam name="TSvcInterface"></typeparam>
    /// <typeparam name="TService"></typeparam>
    void RegisterServiceType<TSvcInterface, TService>(ServiceLifetime lifetime, ILifetime lifetimeInstance = null) where TSvcInterface : class, IService where TService : class, IService;
    
    /// <summary>
    /// Removes a type's registration from being available for the given interface.
    /// </summary>
    /// <typeparam name="TSvcInterface"></typeparam>
    /// <typeparam name="TService"></typeparam>
    void UnregisterServiceType<TSvcInterface, TService>() where TSvcInterface : class, IService where TService : class, IService;

    /// <summary>
    /// Called whenever a new service type for a given interface is implemented.
    /// Args[0]: Interface type
    /// Args[1]: Implementing type
    /// </summary>
    event System.Action<Type, Type> OnServiceRegistered;
    
    #endregion

    #region Services_Instancing_Injection

    /// <summary>
    /// Injects services into the properties of already instanced objects.
    /// </summary>
    /// <param name="inst"></param>
    /// <typeparam name="T"></typeparam>
    void InjectServices<T>(T inst) where T : class;
    
    /// <summary>
    /// Tries to get a service for the given interface, returns success/failure.
    /// </summary>
    /// <param name="service"></param>
    /// <param name="lifetime"></param>
    /// <typeparam name="TSvcInterface"></typeparam>
    /// <returns></returns>
    bool TryGetService<TSvcInterface>(out IService service, out ServiceLifetime lifetime) where TSvcInterface : class, IService;
    
    /// <summary>
    /// Called whenever a new service is created/instanced.
    /// Args[0]: The interface type of the service.
    /// Args[1]: The instance of the service.
    /// </summary>
    event System.Action<Type, IService> OnServiceInstanced;

    #endregion

    #region ActiveServices

    /// <summary>
    /// Returns all services for the given interface.
    /// </summary>
    /// <typeparam name="TSvc"></typeparam>
    /// <returns></returns>
    List<TSvc> GetAllServices<TSvc>() where TSvc : class, IService;

    #endregion

    #region Internal_Use
    
    /// <summary>
    /// Disposes of all services for a type. Warning: unable to dispose of services held by other objects.
    /// </summary>
    /// <typeparam name="TSvc"></typeparam>
    internal void DisposeServicesOfType<TSvc>() where TSvc : class, IService;
    
    /// <summary>
    /// Disposes of all services and resets DI container. Warning: unable to dispose of services held by other objects.
    /// </summary>
    internal void DisposeAllServices();

    #endregion
}

public enum ServiceLifetime
{
    Transient, Singleton, PerInstance, PerThread, Invalid, Custom
}
