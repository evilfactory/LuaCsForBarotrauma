using Barotrauma.Networking;
using MoonSharp.Interpreter;
using Sigil;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Barotrauma.LuaCs;

public interface IDefaultLuaRegistrar : IService
{
    public void RegisterAll();
}

public class DefaultLuaRegistrar : IDefaultLuaRegistrar
{
    public bool IsDisposed { get; private set; }

    private readonly ILuaUserDataService _userDataService;
    private readonly ISafeLuaUserDataService _safeUserDataService;
    private readonly ILoggerService _loggerService;

    public DefaultLuaRegistrar(ILoggerService loggerService, ILuaUserDataService userDataService, ISafeLuaUserDataService safeUserDataService)
    {
        _userDataService = userDataService;
        _safeUserDataService = safeUserDataService;
        _loggerService = loggerService;
    }

    private void RegisterShared()
    {
        _userDataService.RegisterType("System.TimeSpan");
        _userDataService.RegisterType("System.Exception");
        _userDataService.RegisterType("System.Console");
        _userDataService.RegisterType("System.Exception");

        _userDataService.RegisterType("Barotrauma.Success`2");
        _userDataService.RegisterType("Barotrauma.Failure`2");
        _userDataService.RegisterType("Barotrauma.Range`1");

        List<Assembly> assembliesToScan = [typeof(DefaultLuaRegistrar).Assembly, typeof(Identifier).Assembly, typeof(Microsoft.Xna.Framework.Vector2).Assembly];

        foreach (var type in assembliesToScan.SelectMany(a => a.GetTypes()))
        {
            if (type.IsEnum || type.IsDefined(typeof(CompilerGeneratedAttribute)) || !_safeUserDataService.IsAllowed(type.FullName))
            {
                continue;
            }

            _loggerService.LogMessage($"Registered {type.FullName}");

            _userDataService.RegisterType(type.FullName);
        }


        _userDataService.RegisterType("Barotrauma.LuaSByte");
        _userDataService.RegisterType("Barotrauma.LuaByte");
        _userDataService.RegisterType("Barotrauma.LuaInt16");
        _userDataService.RegisterType("Barotrauma.LuaUInt16");
        _userDataService.RegisterType("Barotrauma.LuaInt32");
        _userDataService.RegisterType("Barotrauma.LuaUInt32");
        _userDataService.RegisterType("Barotrauma.LuaInt64");
        _userDataService.RegisterType("Barotrauma.LuaUInt64");
        _userDataService.RegisterType("Barotrauma.LuaSingle");
        _userDataService.RegisterType("Barotrauma.LuaDouble");

        _userDataService.RegisterType("Barotrauma.Level+InterestingPosition");
        _userDataService.RegisterType("Barotrauma.Networking.RespawnManager+TeamSpecificState");

        _userDataService.RegisterType("Barotrauma.CharacterParams+AIParams");
        _userDataService.RegisterType("Barotrauma.CharacterParams+TargetParams");
        _userDataService.RegisterType("Barotrauma.CharacterParams+InventoryParams");
        _userDataService.RegisterType("Barotrauma.CharacterParams+HealthParams");
        _userDataService.RegisterType("Barotrauma.CharacterParams+ParticleParams");
        _userDataService.RegisterType("Barotrauma.CharacterParams+SoundParams");

        _userDataService.RegisterType("Barotrauma.FabricationRecipe+RequiredItemByIdentifier");
        _userDataService.RegisterType("Barotrauma.FabricationRecipe+RequiredItemByTag");

        _userDataService.MakeFieldAccessible(_userDataService.RegisterType("Barotrauma.StatusEffect"), "user");


        _userDataService.RegisterType("Barotrauma.ContentPackageManager+PackageSource");
        _userDataService.RegisterType("Barotrauma.ContentPackageManager+EnabledPackages");

        _userDataService.RegisterType("System.Xml.Linq.XElement");
        _userDataService.RegisterType("System.Xml.Linq.XName");
        _userDataService.RegisterType("System.Xml.Linq.XAttribute");
        _userDataService.RegisterType("System.Xml.Linq.XContainer");
        _userDataService.RegisterType("System.Xml.Linq.XDocument");
        _userDataService.RegisterType("System.Xml.Linq.XNode");


        _userDataService.RegisterType("Barotrauma.Networking.ServerSettings+SavedClientPermission");
        _userDataService.RegisterType("Barotrauma.Inventory+ItemSlot");


        _userDataService.MakeFieldAccessible(_userDataService.RegisterType("Barotrauma.Items.Components.CustomInterface"), "customInterfaceElementList");
        _userDataService.RegisterType("Barotrauma.Items.Components.CustomInterface+CustomInterfaceElement");

        _userDataService.RegisterType("Barotrauma.DebugConsole+Command");

        {
            var descriptor = _userDataService.RegisterType("Barotrauma.NetLobbyScreen");

#if SERVER                
            _userDataService.MakeFieldAccessible(descriptor, "subs");
#endif
        }

        _userDataService.RegisterType("FarseerPhysics.Dynamics.Body");
        _userDataService.RegisterType("FarseerPhysics.Dynamics.World");
        _userDataService.RegisterType("FarseerPhysics.Dynamics.Fixture");
        _userDataService.RegisterType("FarseerPhysics.ConvertUnits");
        _userDataService.RegisterType("FarseerPhysics.Collision.AABB");
        _userDataService.RegisterType("FarseerPhysics.Collision.ContactFeature");
        _userDataService.RegisterType("FarseerPhysics.Collision.ManifoldPoint");
        _userDataService.RegisterType("FarseerPhysics.Collision.ContactID");
        _userDataService.RegisterType("FarseerPhysics.Collision.Manifold");
        _userDataService.RegisterType("FarseerPhysics.Collision.RayCastInput");
        _userDataService.RegisterType("FarseerPhysics.Collision.ClipVertex");
        _userDataService.RegisterType("FarseerPhysics.Collision.RayCastOutput");
        _userDataService.RegisterType("FarseerPhysics.Collision.EPAxis");
        _userDataService.RegisterType("FarseerPhysics.Collision.ReferenceFace");
        _userDataService.RegisterType("FarseerPhysics.Collision.Collision");

        _userDataService.RegisterType("Barotrauma.PrefabCollection`1");
        _userDataService.RegisterType("Barotrauma.PrefabSelector`1");
        _userDataService.RegisterType("Barotrauma.Pair`2");

        var toolBox = UserData.RegisterType(typeof(ToolBox));
#if CLIENT           
        _userDataService.RemoveMember(toolBox, "OpenFileWithShell");
#endif
    }

#if CLIENT
    private void RegisterClient()
    {
        _userDataService.RegisterType("Microsoft.Xna.Framework.Graphics.Effect");
        _userDataService.RegisterType("Microsoft.Xna.Framework.Graphics.EffectParameterCollection");
        _userDataService.RegisterType("Microsoft.Xna.Framework.Graphics.EffectParameter");

        _userDataService.RegisterType("Microsoft.Xna.Framework.Graphics.SpriteBatch");
        _userDataService.RegisterType("Microsoft.Xna.Framework.Graphics.Texture2D");
        _userDataService.RegisterType("EventInput.KeyboardDispatcher");
        _userDataService.RegisterType("EventInput.KeyEventArgs");
        _userDataService.RegisterType("Microsoft.Xna.Framework.Input.Keys");
        _userDataService.RegisterType("Microsoft.Xna.Framework.Input.KeyboardState");


        _userDataService.RegisterType("Barotrauma.Inventory+SlotReference");
    }
#elif SERVER
    private void RegisterServer()
    {
        _userDataService.RegisterType("Barotrauma.Character+TeamChangeEventData");
    }
#endif

    public void RegisterAll()
    {
        RegisterShared();
#if CLIENT
        RegisterClient();  
#elif SERVER
        RegisterServer();
#endif
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
