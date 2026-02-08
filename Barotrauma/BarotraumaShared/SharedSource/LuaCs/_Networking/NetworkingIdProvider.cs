using System;
using Barotrauma.Items.Components;
using Barotrauma.LuaCs.Data;
using System.Security.Cryptography;
using System.Text;

namespace Barotrauma.LuaCs;

internal class NetworkingIdProvider : INetworkIdProvider
{
    public void Dispose()
    {
        //stateless service
    }

    public bool IsDisposed => false;

    private Guid GetNetworkIdFromStringMd5(string id)
    {
        return new Guid(MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(id)));
    }
    
    public Guid GetNetworkIdForInstance(IDataInfo instance)
    {
        var str = $"{instance.OwnerPackage.Name}.{instance.InternalName}";
        return GetNetworkIdFromStringMd5(str);
    }

    public Guid GetNetworkIdForInstance<TEntity>(IDataInfo instance, TEntity attachedEntity) where TEntity : Entity
    {
        var str = $"{nameof(TEntity)}({attachedEntity.ID}).{instance.OwnerPackage.Name}.{instance.InternalName}";
        return GetNetworkIdFromStringMd5(str);
    }

    public Guid GetNetworkIdForInstance(IDataInfo instance, ItemComponent attachedItemComponent)
    {
        var attachedEntity = attachedItemComponent.Item;
        var str = $"{attachedEntity.GetType().Name}({attachedEntity.ID}).ComponentId({attachedEntity.Components.IndexOf(attachedItemComponent)}).{instance.OwnerPackage.Name}.{instance.InternalName}";
        return GetNetworkIdFromStringMd5(str);
    }
}
