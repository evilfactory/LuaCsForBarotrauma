using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
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
    

    public bool IsResourceLoaded<T>(T resource) where T : IAssemblyResourceInfo
    {
        throw new NotImplementedException();
    }

    public Result<ImmutableArray<T>> GetTypes<T>(string namespacePrefix = null, bool includeInterfaces = false,
        bool includeAbstractTypes = false, bool includeDefaultContext = true, bool includeExplicitAssembliesOnly = false)
    {
        throw new NotImplementedException();
    }

    public Result<ImmutableArray<IAssemblyResourceInfo>> LoadAssemblyResources(ImmutableArray<IAssemblyResourceInfo> resource)
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
