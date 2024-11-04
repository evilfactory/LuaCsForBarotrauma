using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    void RegisterServiceType<TSvcInterface, TService>(ServiceLifetime lifetime, ILifetime lifetimeInstance = null) where TSvcInterface : class, IReusableService where TService : class, IReusableService, TSvcInterface;
    
    /// <summary>
    /// Registers a type as a service for a given interface that can be requested by name.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="lifetime"></param>
    /// <param name="lifetimeInstance"></param>
    /// <typeparam name="TSvcInterface"></typeparam>
    /// <typeparam name="TService"></typeparam>
    void RegisterServiceType<TSvcInterface, TService>(string name, ServiceLifetime lifetime, ILifetime lifetimeInstance = null) where TSvcInterface : class, IReusableService where TService : class, IReusableService, TSvcInterface;

    /// <summary>
    /// Called whenever a new service type for a given interface is implemented.
    /// Args[0]: Interface type
    /// Args[1]: Implementing type
    /// </summary>
    event System.Action<Type, Type> OnServiceRegistered;

    /// <summary>
    /// Runs compilation of registered services.
    /// </summary>
    public void Compile();
    
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
    bool TryGetService<TSvcInterface>(out TSvcInterface service) where TSvcInterface : class, IReusableService;
    
    /// <summary>
    /// Tries to get a service for the given name and interface, returns success/failure.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="service"></param>
    /// <param name="lifetime"></param>
    /// <typeparam name="TSvcInterface"></typeparam>
    /// <returns></returns>
    bool TryGetService<TSvcInterface>(string name, out TSvcInterface service) where TSvcInterface : class, IReusableService;
    
    /// <summary>
    /// Called whenever a new service is created/instanced.
    /// Args[0]: The interface type of the service.
    /// Args[1]: The instance of the service.
    /// </summary>
    event System.Action<Type, IReusableService> OnServiceInstanced;

    #endregion

    #region ActiveServices

    /// <summary>
    /// Returns all services for the given interface.
    /// </summary>
    /// <typeparam name="TSvc"></typeparam>
    /// <returns></returns>
    ImmutableArray<TSvc> GetAllServices<TSvc>() where TSvc : class, IReusableService;

    #endregion

    // Notes: Left public due to the common use of Publicizers
    #region Internal_Use
    
    /// <summary>
    /// Notes: Internal use only if hosted by LuaCsForBarotrauma. Disposes of all services and resets DI container. Warning: unable to dispose of services held by other objects.
    /// </summary>
    void DisposeAndReset();

    #endregion
}

public enum ServiceLifetime
{
    Transient, Singleton, PerThread, Invalid, Custom
}
