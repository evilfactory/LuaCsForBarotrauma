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
using Barotrauma.LuaCs.Events;
using FluentResults;
using FluentResults.LuaCs;
using ImpromptuInterface.Build;
using Microsoft.CodeAnalysis;
using OneOf;

namespace Barotrauma.LuaCs.Services;

public class PluginManagementService : IPluginManagementService, IAssemblyManagementService
{
    private readonly Func<IAssemblyLoaderService.LoaderInitData, IAssemblyLoaderService> _assemblyLoaderServiceFactory;
    private readonly ConcurrentDictionary<ContentPackage, (List<IAssemblyResourceInfo> ResourceInfos, IAssemblyLoaderService Loader)> _packageAssemblyResources = new();
    private readonly ConcurrentDictionary<ContentPackage, List<IDisposable>> _pluginInstances = new();
    private readonly Lazy<IEventService> _eventService;
    private readonly ConditionalWeakTable<IAssemblyLoaderService, ContentPackage> _unloadingAssemblyLoaders = new();
    private readonly ConditionalWeakTable<Assembly, ConcurrentDictionary<string, Type>> _assemblyTypesCache = new();

    public PluginManagementService(
        Func<IAssemblyLoaderService.LoaderInitData, 
            IAssemblyLoaderService> assemblyLoaderServiceFactory,
        Lazy<IEventService> eventService)
    {
        _assemblyLoaderServiceFactory = assemblyLoaderServiceFactory;
        _eventService = eventService;
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoadedGlobal;
    }

    private void OnAssemblyLoadedGlobal(object sender, AssemblyLoadEventArgs args)
    {
        // cache types by name
        try
        {
            var context = AssemblyLoadContext.GetLoadContext(args.LoadedAssembly);
            if (context is not IAssemblyLoaderService loaderService)
                return;
            _eventService.Value.PublishEvent<IEventAssemblyLoaded>(sub => sub.OnAssemblyLoaded(args.LoadedAssembly));
            var lookupDict = new ConcurrentDictionary<string, Type>();
            foreach (var type in args.LoadedAssembly.GetSafeTypes())
            {
                lookupDict[type.FullName ??  type.Name] = type;
            }
            _assemblyTypesCache.AddOrUpdate(args.LoadedAssembly, lookupDict);
        }
        catch (Exception e)
        {
            // ignored
            return;
        }
    }

    private int _isDisposed = 0;
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public FluentResults.Result Reset()
    {
        if (IsDisposed)
            return FluentResults.Result.Fail($"{nameof(PluginManagementService)} is disposed!");

        throw new NotImplementedException();
    }

    public Result<ImmutableArray<Type>> GetImplementingTypes<T>(bool includeInterfaces = false,
        bool includeAbstractTypes = false, bool includeDefaultContext = true)
    {
        var builder = ImmutableArray.CreateBuilder<Type>();

        if (this._packageAssemblyResources.Any())
        {
            foreach (var resource in this._packageAssemblyResources
                         .Where(res => !res.Value.Loader.IsReferenceOnlyMode))
            {
                builder.AddRange(resource.Value.Loader.Assemblies
                    .SelectMany(assembly => assembly.GetSafeTypes())
                    .Where(type => type.IsAssignableTo(typeof(T)))
                    .Where(type => includeInterfaces || !type.IsInterface)
                    .Where(type => includeAbstractTypes || !type.IsAbstract));
            }
        }

        if (includeDefaultContext)
        {
            builder.AddRange(AssemblyLoadContext.Default.Assemblies
                .SelectMany(assembly => assembly.GetSafeTypes())
                .Where(type => type.IsAssignableTo(typeof(T)))
                .Where(type => includeInterfaces || !type.IsInterface)
                .Where(type => includeAbstractTypes || !type.IsAbstract));
        }
        
        return builder.Count == 0 
            ? FluentResults.Result.Fail($"Failed to find any types that implement {typeof(T).Name})") 
            : FluentResults.Result.Ok(builder.ToImmutable());
    }

    public Type GetType(string typeName, bool isByRefType = false, bool includeInterfaces = false,
        bool includeDefaultContext = true)
    {
        if (includeDefaultContext)
        {
            var type = Type.GetType(typeName, false);
        }
        
        // TODO: implement by-ref type resolution
        throw new NotImplementedException();
    }

    public FluentResults.Result LoadAssemblyResources(ImmutableArray<IAssemblyResourceInfo> resource)
    {
#if DEBUG
        return FluentResults.Result.Fail($"{nameof(LoadAssemblyResources)}: Plugin loading not currently implemented.");
#endif
        throw new NotImplementedException();
    }

    public ImmutableArray<Result<(Type, T)>> ActivateTypeInstances<T>(ImmutableArray<Type> types, bool serviceInjection = true,
        bool hostInstanceReference = false) where T : IDisposable
    {
#if DEBUG
        return ImmutableArray<Result<(Type, T)>>.Empty;
#endif
        throw new NotImplementedException();
    }

    public FluentResults.Result UnloadManagedAssemblies()
    {
        var res = new FluentResults.Result();
        
        // cleanup managed plugins
        if (_pluginInstances.Any())
        {
            foreach (var packageInstances in _pluginInstances)
            {
                if (!packageInstances.Value.Any())
                    continue;
                
                foreach (var disposable in packageInstances.Value)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception e)
                    {
                        res = res.WithError(new ExceptionalError(e)
                            .WithMetadata(MetadataType.ExceptionObject, this));
                    }
                }
            }
            _pluginInstances.Clear();
        }
        
        _assemblyTypesCache.Clear();
            
        // cleanup running assembly contexts
        if (_packageAssemblyResources.Any())
        {
            foreach (var resource in _packageAssemblyResources.ToImmutableDictionary())
            {
                if (resource.Value.Loader is not null)
                {
                    try
                    {
                        resource.Value.Loader.Dispose();
                        _unloadingAssemblyLoaders.AddOrUpdate(resource.Value.Loader, resource.Key);
                        _packageAssemblyResources.TryRemove(resource);
                        _packageAssemblyResources.TryAdd(resource.Key, (resource.Value.ResourceInfos, null));
                    }
                    catch (Exception e)
                    {
                        res = res.WithError(new ExceptionalError(e)
                            .WithMetadata(MetadataType.ExceptionObject, this));
                    }
                }
            }
        }

        return res.WithSuccess($"Unloading of managed assemblies started successfully,");
    }

    public Result<Assembly> GetLoadedAssembly(OneOf<AssemblyName, string> assemblyName, in Guid[] excludedContexts)
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<MetadataReference> GetDefaultMetadataReferences(bool includeDefaultContext = true)
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<IAssemblyLoaderService> AssemblyLoaderServices { get; }
}
