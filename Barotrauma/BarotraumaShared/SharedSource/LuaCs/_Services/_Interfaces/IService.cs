using System;
using Microsoft.Toolkit.Diagnostics;

namespace Barotrauma.LuaCs;

/// <summary>
/// Represents a <see cref="IReusableService"/> that is automatically instantiated at startup for the lifetime of the
/// <see cref="IServiceProvider"/> instance.
/// </summary>
public interface ISystem : IReusableService { }

/// <summary>
/// Defines a service that can be reset to it's post-constructor state and reused without needing to be disposed.
/// Intended for persistent services.
/// </summary>
public interface IReusableService : IService
{
    /// <summary>
    /// Returns the service to its original state (post-instantiation).
    /// Allows a service instance to be reused without disposing of the instance. 
    /// </summary>
    FluentResults.Result Reset();
}

/// <summary>
/// Base interface inherited by all services.
/// </summary>
/// <exception cref="ObjectDisposedException">Throws exception if `IsDisposed` return true.</exception>
public interface IService : IDisposable
{
    bool IsDisposed { get; }
    public void CheckDisposed()
    {
        if (IsDisposed) 
            ThrowHelper.ThrowObjectDisposedException($"Tried to call method on disposed object '{this.GetType().Name}'!");
    }

    static void CheckDisposed(IService service)
    {
        if (service.IsDisposed) 
            ThrowHelper.ThrowObjectDisposedException($"Tried to call method on disposed object '{service.GetType().Name}'!");
    }
}
