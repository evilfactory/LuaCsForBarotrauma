using System;
using System.Collections.Generic;
using System.IO;

namespace Barotrauma
{
    [Obsolete("Make your class implement IAssemblyPlugin instead.")]
    public abstract class ACsMod : IAssemblyPlugin
    {
        private static List<ACsMod> mods = new List<ACsMod>();
        public static List<ACsMod> LoadedMods { get => mods; }

        private const string MOD_STORE = "LocalMods/.modstore";
        public static string GetStoreFolder<T>() where T : ACsMod
        {
            if (!Directory.Exists(MOD_STORE)) Directory.CreateDirectory(MOD_STORE);
            var modFolder = $"{MOD_STORE}/{typeof(T)}";
            if (!Directory.Exists(modFolder)) Directory.CreateDirectory(modFolder);
            return modFolder;
        }

        public bool IsDisposed { get; private set; }

        /// Mod initialization
        public ACsMod()
        {
            IsDisposed = false;
            LoadedMods.Add(this);
        }

        /// <summary>
        /// Called as soon as plugin loading begins, use this for internal setup only.
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// Called once all plugins have completed Initialization. Put cross-mod code here.
        /// </summary>
        public virtual void OnLoadCompleted() { }

        /// <summary>
        /// [NotImplemented] Called before vanilla content is loaded. Use to patch Barotrauma classes before they're
        /// instantiated.
        /// </summary>
        public void PreInitPatching() { }

        public virtual void Dispose()
        {
            try
            {
                Stop();
            }
            catch (Exception e)
            {
                LuaCsLogger.HandleException(e, LuaCsMessageOrigin.CSharpMod);
            }

            LoadedMods.Remove(this);
            IsDisposed = true;
        }
        
        public abstract void Stop();
    }
}
