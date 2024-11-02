using System;

namespace Barotrauma.LuaCs.Services;

/// <summary>
/// Base interface inherited by all services
/// </summary>
public interface IService : IDisposable
{
    /// <summary>
    /// Returns the service to its original state (post-instantiation).
    /// Allows a service instance to be reused without disposing of the instance. 
    /// </summary>
    void Reset();
}
