using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Events;
using Barotrauma.Steam;
using FluentResults;
using OneOf;

namespace Barotrauma.LuaCs.Services;

/// <summary>
/// Provides <see cref="IPackageInfo"/> resolution for dynamically locating the best matching package at the time of consumption.
/// </summary>
public sealed class ContentPackageInfoLookup : IPackageInfoLookupService, IEventEnabledPackageListChanged, IEventAllPackageListChanged
{
    #region INTERNAL

    // packageinfo query data
    private readonly ConcurrentDictionary<OneOf.OneOf<string, ulong, (string, ulong)>, IPackageInfo> _packageInfoMap = new();
    // package query data
    private readonly ConcurrentDictionary<uint, ImmutableArray<ContentPackage>> _packageIdGroups = new();
    private readonly ConcurrentDictionary<ContentPackage, ImmutableArray<uint>> _reversePackageIdGroups = new();
    private readonly HashSet<ContentPackage> _enabledPackages;
    private readonly HashSet<ContentPackage> _allPackages;
    // threading
    private readonly AsyncReaderWriterLock _packageIdGroupsLock = new();
    private readonly AsyncReaderWriterLock _packageSetsLock = new();
    // services
    private readonly IEventService _eventService;
    private readonly IPackageListRetrievalService _packageListRetrievalService;
    
    private int _isDisposed = 0;
    private uint _idCounter = 0;

    // returns ++_idCounter;
    private uint GetNextId() => Interlocked.Increment(ref _idCounter);

    private ContentPackage GetBestMatchPackage(IPackageInfo packageInfo)
    {
        if (packageInfo is null)
            return null;
        if (!_packageIdGroups.TryGetValue(packageInfo.Id, out var packageGroup)
            || packageGroup.IsDefaultOrEmpty)
            return null;
        if (packageGroup.Length == 1)
            return packageGroup[0];

        bool nameGood = !packageInfo.Name.IsNullOrWhiteSpace();

        // try by enabled
        var prev = packageGroup;

        var packList = packageGroup;
        using (_packageSetsLock.AcquireReaderLock().GetAwaiter().GetResult())
        {
            packList = packList
                .Where(p => p is not null && _enabledPackages.Contains(p))
                .ToImmutableArray();
        }
        
        if (ReturnValue())
            return packList[0];

        // try by steam id
        if (packageInfo.SteamWorkshopId != 0)
        {
            packList = packList
                .Where(p => p.TryExtractSteamWorkshopId(out var sId) && sId.Value == packageInfo.SteamWorkshopId)
                .ToImmutableArray();
            
            if (ReturnValue())
                return packList[0];
        }

        // try by name
        if (nameGood)
        {
            packList = packList
                .Where(p => p.Name == packageInfo.Name)
                .ToImmutableArray();
            
            if (ReturnValue())
                return packList[0];
        }
        
        // try by localmods
        packList = packList.Where(p => p.Path.ToLowerInvariant().Contains("localmods"))
            .ToImmutableArray();
        
        if (ReturnValue())
            return packList[0];

        // get the first in the list
        return packList.First();
        
        bool ReturnValue()
        {
            if (packList.IsDefaultOrEmpty)
                packList = prev;
            else if (packList.Length == 1)
                return true;
            else
                prev = packList;
            return false;
        }
    }

    private async Task SyncPackagesLists(IReadOnlyList<ContentPackage> enabledPackages,
        IReadOnlyList<ContentPackage> allPackages)
    {
        if (enabledPackages is null || allPackages is null)
            return;
        
        // take all locks
        using var l1 = await _packageIdGroupsLock.AcquireWriterLock();
        using var l2 = await _packageSetsLock.AcquireWriterLock();
        
        // calc diffs
        var toAddAll = allPackages.Except(_allPackages).ToHashSet();
        var toAddEnabled = enabledPackages.Except(_enabledPackages).ToHashSet();
        var toRemoveAll = _allPackages.Except(allPackages).ToHashSet();
        var toRemoveEnabled = _enabledPackages.Except(enabledPackages).ToHashSet();
            
        // remove old
        if (toRemoveAll.Any())
        {
            foreach (var package in toRemoveAll)
            {
                if (package is null)
                    continue;
                
                _allPackages.Remove(package);
                
                // try to find id lookup
                if (!_reversePackageIdGroups.TryGetValue(package, out var idGroup))
                    continue;
            
                // found packs
                if (!idGroup.IsDefaultOrEmpty)
                {
                    foreach (var id in idGroup)
                    {
                        if (!_packageIdGroups.TryGetValue(id, out var packageGroup)
                            || packageGroup.IsDefaultOrEmpty)
                            continue;
                        _packageIdGroups[id] = packageGroup.RemoveAll(p => toRemoveAll.Contains(p));
                    }    
                }

                // remove ref
                _reversePackageIdGroups.Remove(package, out _);
            }
        }

        if (toRemoveEnabled.Any())
        {
            foreach (var package in toRemoveEnabled)
            {
                if (package is null)
                    continue;
                _enabledPackages.Remove(package);
            }
        }
        
        // add new
        if (toAddAll.Any())
        {
            foreach (var package in toAddAll)
            {
                if (package is null)
                    continue;
                
                _allPackages.Add(package);

                var steamId = package.TryExtractSteamWorkshopId(out var id) ? id.Value : 0;
                IPackageInfo packageInfo;
                Queue<uint> idListsToAdd = new();
                if (!package.Name.IsNullOrWhiteSpace() && steamId > 0)
                {
                    // combined key
                    packageInfo = GetOrCreateInfoForMap(package, (package.Name, steamId));
                    AddToPackageIdGroups(packageInfo.Id, package);
                    // string key
                    packageInfo = GetOrCreateInfoForMap(package, package.Name);
                    AddToPackageIdGroups(packageInfo.Id, package);
                    // steamId key
                    packageInfo = GetOrCreateInfoForMap(package, steamId);
                    AddToPackageIdGroups(packageInfo.Id, package);
                }
                
                // try find in the existing list, or make a new one
                IPackageInfo GetOrCreateInfoForMap(ContentPackage package, OneOf.OneOf<string, ulong, (string, ulong)> infoKey)
                {
                    return _packageInfoMap.TryGetValue(infoKey, out var pInfo) 
                        ? pInfo 
                        : new PackageInfo(package, GetNextId(), GetBestMatchPackage);
                }

                // add to package lookups
                void AddToPackageIdGroups(uint id, ContentPackage package)
                {
                    if (_packageIdGroups.TryGetValue(id, out var packageGroup))
                    {
                        if (!packageGroup.Contains(package))
                            _packageIdGroups[id] = packageGroup.Add(package);
                    }
                    else
                        _packageIdGroups[id] = new[] { package }.ToImmutableArray();

                    if (_reversePackageIdGroups.TryGetValue(package, out var idGroup))
                    {
                        if (!idGroup.Contains(id))
                            _reversePackageIdGroups[package] = idGroup.Add(id);
                    }
                    else
                        _reversePackageIdGroups[package] = new[] { id }.ToImmutableArray();
                }
            }
        }

        if (toAddEnabled.Any())
        {
            foreach (var package in toAddEnabled)
            {
                if (package is null)
                    continue;
                _enabledPackages.Add(package);
            }
        }
    }
    
    private async Task<Result<IPackageInfo>> LookupInternal(OneOf.OneOf<string, ulong, (string, ulong)> infoKey)
    {
        using (await _packageIdGroupsLock.AcquireReaderLock())
        {
            if (_packageInfoMap.TryGetValue(infoKey, out var packageInfo))
                return FluentResults.Result.Ok(packageInfo);
        }

        // change to write lock
        using (await _packageIdGroupsLock.AcquireWriterLock())
        {
            // create one
            var packageInfo = infoKey.Match<IPackageInfo>(
                sPackName => new PackageInfo(sPackName, GetNextId(), GetBestMatchPackage),
                uSteamId => new PackageInfo(uSteamId, GetNextId(), GetBestMatchPackage),
                cKey => new PackageInfo(cKey.Item1, cKey.Item2, GetNextId(), GetBestMatchPackage)
            );
            _packageInfoMap[infoKey] = packageInfo;
            // empty array
            _packageIdGroups[packageInfo.Id] = ImmutableArray<ContentPackage>.Empty;
            return FluentResults.Result.Ok(packageInfo);
        }
    }

    #endregion

    public ContentPackageInfoLookup(IEventService eventService, IPackageListRetrievalService packageListRetrievalService)
    {
        _eventService = eventService ?? throw new ArgumentNullException(
            $"{nameof(ContentPackageInfoLookup)}: {nameof(eventService)} cannot be null.");
        _packageListRetrievalService = packageListRetrievalService ?? throw new ArgumentNullException(nameof(packageListRetrievalService));
        this._enabledPackages = new HashSet<ContentPackage>();
        this._allPackages = new HashSet<ContentPackage>();
    }

    public void Dispose()
    {
        IsDisposed = true;
        // locks
        using var l1 = _packageIdGroupsLock.AcquireWriterLock().GetAwaiter().GetResult();
        using var l2 = _packageSetsLock.AcquireWriterLock().GetAwaiter().GetResult();
        
        _eventService.Unsubscribe<IEventEnabledPackageListChanged>(this);
        _eventService.Unsubscribe<IEventAllPackageListChanged>(this);
        
        _packageIdGroups.Clear();
        _packageInfoMap.Clear();
        _reversePackageIdGroups.Clear();
    }
    
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }

    public FluentResults.Result Reset()
    {
        if (IsDisposed)
            return FluentResults.Result.Fail($"Service is disposed.");
        
        using var l1 = _packageIdGroupsLock.AcquireWriterLock().GetAwaiter().GetResult();
        using var l2 = _packageSetsLock.AcquireWriterLock().GetAwaiter().GetResult();
        
        _packageIdGroups.Clear();
        _packageInfoMap.Clear();
        _reversePackageIdGroups.Clear();
        
        RefreshPackageLists();
        
        return FluentResults.Result.Ok();
    }

    public void OnEnabledPackageListChanged(CorePackage package, IEnumerable<RegularPackage> regularPackages)
    {
        ((IService)this).CheckDisposed();
        SyncPackagesLists( 
            regularPackages.Select(p => (ContentPackage)p).ToImmutableArray().Add(package), 
            _allPackages.ToImmutableArray())
            .GetAwaiter().GetResult();
    }

    public void OnAllPackageListChanged(IEnumerable<CorePackage> corePackages, IEnumerable<RegularPackage> regularPackages)
    {
        ((IService)this).CheckDisposed();
        SyncPackagesLists(
            _enabledPackages.ToImmutableArray(),
            regularPackages.Select(p => p as ContentPackage)
                .Union(corePackages.Select(p => p as ContentPackage))
                .ToImmutableArray()
            ).GetAwaiter().GetResult();
    }
    
    public async Task<Result<IPackageInfo>> Lookup(string packageName)
    {
        ((IService)this).CheckDisposed();
        if(packageName.IsNullOrWhiteSpace())
            return FluentResults.Result.Fail($"Name is null or empty.");
        return await LookupInternal(packageName);
    }
    
    public async Task<Result<IPackageInfo>> Lookup(string packageName, ulong steamWorkshopId)
    {
        ((IService)this).CheckDisposed();
        if (packageName.IsNullOrWhiteSpace() || steamWorkshopId == 0)
            return FluentResults.Result.Fail($"Name or steam id is null or empty.");
        return await LookupInternal((packageName, steamWorkshopId));
    }

    public async Task<Result<IPackageInfo>> Lookup(ulong steamWorkshopId)
    {
        ((IService)this).CheckDisposed();
        if (steamWorkshopId is 0)
            return FluentResults.Result.Fail($"SteamId is 0.");
        return await LookupInternal(steamWorkshopId);
    }

    public async Task<Result<IPackageInfo>> Lookup(ContentPackage package)
    {
        ((IService)this).CheckDisposed();
        if (package is null)
            return FluentResults.Result.Fail($"Package is null.");
        
        if (package.TryExtractSteamWorkshopId(out var steamWorkshopId) && steamWorkshopId.Value != 0)
        {
            if (!package.Name.IsNullOrWhiteSpace())
                return await LookupInternal((package.Name, steamWorkshopId.Value));
            else
                return await LookupInternal(steamWorkshopId.Value);
        }
        
        if (!package.Name.IsNullOrWhiteSpace())
            return await LookupInternal(package.Name);
        
        return FluentResults.Result.Fail($"Package name is null and steamid is 0.");
    }

    public void RefreshPackageLists()
    {
        ((IService)this).CheckDisposed();
        if (Thread.CurrentThread != GameMain.MainThread)
            throw new InvalidOperationException($"{nameof(ContentPackageInfoLookup)}: {nameof(RefreshPackageLists)} must be run on the main thread.");
        var enabledPackages = _packageListRetrievalService.GetEnabledContentPackages().ToImmutableArray();
        var allPackages = _packageListRetrievalService.GetAllContentPackages().ToImmutableArray();
        SyncPackagesLists(enabledPackages, allPackages).GetAwaiter().GetResult();
    }
}
