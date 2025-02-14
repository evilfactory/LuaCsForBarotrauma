using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using Barotrauma.Extensions;
using Barotrauma.LuaCs.Data;
using FluentResults;
using Microsoft.CodeAnalysis;

namespace Barotrauma.LuaCs.Services;

public class PluginManagementService : IPluginManagementService, IAssemblyManagementService
{
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }
    private int _isDisposed;
    
    private readonly ReaderWriterLockSlim _operationsLock = new(LockRecursionPolicy.SupportsRecursion);
    
    private readonly ConcurrentDictionary<Guid, IAssemblyLoaderService> _assemblyServices = new();
    private readonly ConcurrentDictionary<IAssemblyResourceInfo, Guid> _resourceData = new();
    private readonly Lazy<IEventService> _eventService;
    private readonly Func<IAssemblyLoaderService> _assemblyServiceFactory;
    private ImmutableDictionary<string, Type> _cachedTypes = null;
    private ImmutableDictionary<string, Type> DefaultTypeCache => _cachedTypes ??= AssemblyLoadContext.Default.Assemblies
        .SelectMany(ass => ass.GetSafeTypes()).ToImmutableDictionary(type => type.FullName, type => type);


    public bool IsResourceLoaded<T>(T resource) where T : IAssemblyResourceInfo
    {
        ((IService)this).CheckDisposed();
        return _resourceData.ContainsKey(resource);
    }

    public Result<ImmutableArray<Type>> GetImplementingTypes<T>(string namespacePrefix = null, bool includeInterfaces = false,
        bool includeAbstractTypes = false, bool includeDefaultContext = true)
    {
        ((IService)this).CheckDisposed();
        var types = ImmutableArray.CreateBuilder<Type>();
        _operationsLock.EnterReadLock();
        try
        {
            if (AssemblyLoaderServices.Any())
            {
                types.AddRange(AssemblyLoaderServices
                    .SelectMany(als => als.UnsafeGetTypesInAssemblies())
                    .Where(t => t is not null)
                    .Where(type => typeof(T).IsAssignableFrom(type))
                    .Where(type => includeInterfaces || !type.IsInterface)
                    .Where(type => includeAbstractTypes || !type.IsAbstract)
                    .Where(type => namespacePrefix is not null && type.FullName is not null && type.FullName.StartsWith(namespacePrefix)));
            }

            if (includeDefaultContext)
            {
                types.AddRange(AssemblyLoadContext.Default.Assemblies
                    .SelectMany(ass => ass.GetSafeTypes())
                    .Where(t => t is not null)
                    .Where(type => typeof(T).IsAssignableFrom(type))
                    .Where(type => includeInterfaces || !type.IsInterface)
                    .Where(type => includeAbstractTypes || !type.IsAbstract)
                    .Where(type => namespacePrefix is not null && type.FullName is not null && type.FullName.StartsWith(namespacePrefix)));
            }

            return types.MoveToImmutable();
        }
        finally
        {
            _operationsLock.ExitReadLock();
        }
    }

    public Type GetType(string typeName)
    {
        ((IService)this).CheckDisposed();
        _operationsLock.EnterReadLock();
        try
        {
            if (DefaultTypeCache.TryGetValue(typeName, out var type))
                return type;
            if (AssemblyLoaderServices.None())
                return null;
            foreach (var loaderService in AssemblyLoaderServices)
            {
                if (loaderService.GetTypeInAssemblies(typeName) is { IsSuccess: true, Value: not null } ret)
                    return ret.Value;
            }
            return null;
        }
        finally
        {
            _operationsLock.ExitReadLock();
        }
    }

    public Result<ImmutableArray<IAssemblyResourceInfo>> LoadAssemblyResources(ImmutableArray<IAssemblyResourceInfo> resource)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<Result<(Type, T)>> ActivateTypeInstances<T>(ImmutableArray<Type> types, bool serviceInjection = true,
        bool hostInstanceReference = false) where T : IDisposable
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result UnloadHostedReferences()
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result UnloadAllAssemblyResources()
    {
        throw new NotImplementedException();
    }

    public Result<Assembly> GetLoadedAssembly(string assemblyName, in Guid[] excludedContexts)
    {
        ((IService)this).CheckDisposed();
        _operationsLock.EnterReadLock();
        try
        {
            foreach (var (guid, context) in _assemblyServices)
            {
                if (excludedContexts.Length > 0 && excludedContexts.Contains(guid))
                    continue;
                if (context.GetAssemblyByName(assemblyName) is { IsSuccess: true, Value: not null } ret)
                    return ret.Value;
            }
            return FluentResults.Result.Fail($"Could not find assembly {assemblyName}");
        }
        finally
        {
            _operationsLock.ExitReadLock();
        }
    }

    public Result<Assembly> GetLoadedAssembly(AssemblyName assemblyName, in Guid[] excludedContexts) 
        => GetLoadedAssembly(assemblyName.FullName, excludedContexts);

    public ImmutableArray<MetadataReference> GetDefaultMetadataReferences() => 
        Basic.Reference.Assemblies.Net60.References.All.Select(Unsafe.As<MetadataReference>).ToImmutableArray();

    public ImmutableArray<MetadataReference> GetAddInContextsMetadataReferences()
    {
        ((IService)this).CheckDisposed();
        _operationsLock.EnterReadLock();
        try
        {
            if (_assemblyServices.IsEmpty)
                return ImmutableArray<MetadataReference>.Empty;
            var builder = ImmutableArray.CreateBuilder<MetadataReference>();
            foreach (var context in _assemblyServices.Values)
                builder.AddRange(context.AssemblyReferences);
            return builder.ToImmutable();
        }
        finally
        {
            _operationsLock.ExitReadLock();
        }
    }

    public ImmutableArray<IAssemblyLoaderService> AssemblyLoaderServices { get; }

    public void Dispose()
    {
        // TODO release managed resources here
        throw new NotImplementedException();
    }
    
    public FluentResults.Result Reset()
    {
        throw new NotImplementedException();
    }
}
