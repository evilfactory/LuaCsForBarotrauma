using System;
using System.Diagnostics.CodeAnalysis;
using Barotrauma.Items.Components;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs;

/// <summary>
/// Provides a deterministic ID for a given <see cref="IDataInfo"/> instance under multiple circumstances, for use with
/// network synchronization.
/// </summary>
internal interface INetworkIdProvider : IService
{
    /// <summary>
    /// Deterministically generates a GUID for the given parameters.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The GUID for the entity.</returns>
    Guid GetNetworkIdForInstance([NotNull] IDataInfo instance);
    
    /// <summary>
    /// Deterministically generates a GUID for the given parameters.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="attachedEntity">The <see cref="Entity"/> that this instance is attached to, if any.</param>
    /// <typeparam name="TEntity">The entity type, if any.</typeparam>
    /// <returns>The GUID for the entity.</returns>
    Guid GetNetworkIdForInstance<TEntity>([NotNull] IDataInfo instance, TEntity attachedEntity) where TEntity : Entity;
    
    /// <summary>
    /// Deterministically generates a GUID for the given parameters.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="attachedItemComponent">The <see cref="ItemComponent"/> that this instance is attached to, if any.</param>
    /// <returns>The GUID for the entity.</returns>
    Guid GetNetworkIdForInstance([NotNull] IDataInfo instance, [MaybeNull] ItemComponent attachedItemComponent);
}
