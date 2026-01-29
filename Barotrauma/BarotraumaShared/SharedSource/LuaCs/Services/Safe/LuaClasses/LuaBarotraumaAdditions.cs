using System;
using System.Collections.Generic;
using System.Text;
using MoonSharp.Interpreter;
using Microsoft.Xna.Framework;
using Barotrauma.Networking;

namespace Barotrauma.Networking
{
    partial class Client
    {
        public static IReadOnlyList<Client> ClientList
        {
            get
            {
                if (GameMain.IsSingleplayer) { return new List<Client>(); }

#if SERVER
                return GameMain.Server.ConnectedClients;
#else
                return GameMain.Client.ConnectedClients;
#endif
            }
        }

        public ulong SteamID
        {
            get
            {
                if (AccountId.TryUnwrap(out AccountId outValue) && outValue is SteamId steamId)
                {
                    return steamId.Value;
                }
                else
                {
                    return 0;
                }
            }
        }

    }

}

namespace Barotrauma 
{
    using Barotrauma.Networking;
    using System.Linq;
    using System.Reflection;



    partial class Character
    {
        
    }

    partial class Item
    {
        public object GetComponentString(string component)
        {
            Type type = GameMain.LuaCs.PluginManagementService.GetType("Barotrauma.Items.Components." + component);
            
            if (type == null)
            {
                return null;
            }

            MethodInfo method = typeof(Item).GetMethod(nameof(Item.GetComponent));
            MethodInfo generic = method.MakeGenericMethod(type);
            return generic.Invoke(this, null);
        }

    }

    partial class ItemPrefab
    {

        public static ItemPrefab GetItemPrefab(string itemNameOrId)
        {
            ItemPrefab itemPrefab =
            (MapEntityPrefab.Find(itemNameOrId, identifier: null, showErrorMessages: false) ??
            MapEntityPrefab.Find(null, identifier: itemNameOrId, showErrorMessages: false)) as ItemPrefab;

            return itemPrefab;
        }
    }

    abstract partial class MapEntity 
    {
        public void AddLinked(MapEntity entity)
        {
            linkedTo.Add(entity);
        }
    }

}

namespace Barotrauma.Items.Components
{
    using Barotrauma.Networking;

    partial class CustomInterface
    {
    }

    partial struct Signal
    {
    }
}
