using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LightInject;

namespace Barotrauma.LuaCs;

/// <summary>
/// Provides instancing and management of <see cref="IService"/>, <see cref="IReusableService"/>, and <see cref="ISystem"/>
/// instances.
/// </summary>
public interface IServicesProvider
{
    #region Type_Registration

    /// <summary>
    /// Registers a type as a service for a given interface.
    /// </summary>
    /// <remarks>NOTE: <see cref="ISystem"/> services are forced to <see cref="ServiceLifetime.Singleton"/></remarks>
    /// <param name="lifetime">The <see cref="ServiceLifetime"/> of the service when requested.</param>
    /// <param name="lifetimeInstance">Custom lifetime instance.</param>
    /// <typeparam name="TSvcInterface">Service interface.</typeparam>
    /// <typeparam name="TService">Implementing service type.</typeparam>
    void RegisterServiceType<TSvcInterface, TService>(ServiceLifetime lifetime, ILifetime lifetimeInstance = null) where TSvcInterface : class, IService where TService : class, IService, TSvcInterface;
    
    /// <summary>
    /// Registers a type as a service for a given interface that can be requested by name.
    /// </summary>
    /// <remarks>NOTE: <see cref="ISystem"/> services are forced to <see cref="ServiceLifetime.Singleton"/></remarks>
    /// <param name="name">Name of the service for lookup.</param>
    /// <param name="lifetime">The <see cref="ServiceLifetime"/> of the service when requested.</param>
    /// <param name="lifetimeInstance">Custom lifetime instance.</param>
    /// <typeparam name="TSvcInterface">Service interface.</typeparam>
    /// <typeparam name="TService">Implementing service type.</typeparam>
    void RegisterServiceType<TSvcInterface, TService>(string name, ServiceLifetime lifetime, ILifetime lifetimeInstance = null) where TSvcInterface : class, IService where TService : class, IService, TSvcInterface;
    
    /// <summary>
    /// Registers a factory for resolving the service type.
    /// </summary>
    /// <param name="factory"></param>
    /// <typeparam name="TSvcInterface"></typeparam>
    void RegisterServiceResolver<TSvcInterface>(Func<ServiceContainer, TSvcInterface> factory) where TSvcInterface : class, IService;
    
    /// <summary>
    /// Compiles/Generates IL for registered services and instantiates all registered <see cref="ISystem"/> types. 
    /// </summary>
    public void CompileAndRun();
    
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
    /// <typeparam name="TSvcInterface"></typeparam>
    /// <returns></returns>
    bool TryGetService<TSvcInterface>(out TSvcInterface service) where TSvcInterface : class, IService;
    
    /// <summary>
    /// Tries to get a service for the given interface, throws an exception upon failure.
    /// </summary>
    /// <typeparam name="TSvcInterface"></typeparam>
    /// <returns></returns>
    TSvcInterface GetService<TSvcInterface>() where TSvcInterface : class, IService;
    
    /// <summary>
    /// Tries to get a service for the given name and interface, returns success/failure.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="service"></param>
    /// <typeparam name="TSvcInterface"></typeparam>
    /// <returns></returns>
    bool TryGetService<TSvcInterface>(string name, out TSvcInterface service) where TSvcInterface : class, IService;
    
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
    ImmutableArray<TSvc> GetAllServices<TSvc>() where TSvc : class, IService;

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
