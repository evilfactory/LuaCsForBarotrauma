

using System;
using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;

namespace Barotrauma
{
    class LuaRequire
    {
        private Script lua { get; set; }
        private Dictionary<string, DynValue> loadedModules { get; set; }

        private bool GetExistingReturnValue(string moduleName, ref DynValue returnValue)
        {
            return loadedModules.TryGetValue(
              moduleName,
              out returnValue
            );
        }

        private string FixContentPackagePath(string contentPackagePath)
        {
            contentPackagePath = Path.TrimEndingDirectorySeparator(
              new FileInfo(contentPackagePath)  // filelist.xml
                .Directory
                .FullName
                .CleanUpPathCrossPlatform()
            );

            return contentPackagePath;
        }
        private string GetContentPackagePath(string path)
        {
            IEnumerable<ContentPackage> allContentPackages = ContentPackageManager.AllPackages;
            foreach (ContentPackage contentPackage in allContentPackages)
            {
                string contentPackagePath = FixContentPackagePath(contentPackage.Path);
                if (path.StartsWith(contentPackagePath))
                {
                    return contentPackagePath;
                }
            }

            // Return null if we can't find a content package that
            // this module belongs to.
            return null;
        }

        private string GetContentPackagePath(string moduleName, Table environment)
        {
            string filePath = lua.Options
              .ScriptLoader
              .ResolveModuleName(
                moduleName,
                environment
              );
            filePath = Path.TrimEndingDirectorySeparator(
              new FileInfo(filePath)
                .Directory
                .FullName
                .CleanUpPathCrossPlatform()
            );

            return GetContentPackagePath(filePath);
        }

        private void SaveReturnValue(string moduleName, DynValue returnValue)
        {
            loadedModules[moduleName] = returnValue;
        }

        private void ExecuteModule(string moduleName, Table environment, ref DynValue returnValue)
        {
            DynValue loadFunc = lua.RequireModule(
                moduleName,
                environment
            );
            string packagePath = GetContentPackagePath(
              moduleName,
              environment
            );

            returnValue = lua.Call(
              loadFunc,
              packagePath
            );

        }

        // Lua modules that have been previously loaded by require() will
        // not be loaded again; instead, their initial return value is
        // preserved and returned again on subsequent attempts.
        public DynValue Require(string moduleName, Table globalContext)
        {
            DynValue returnValue = null;
            Table environment = globalContext ?? lua.Globals;

            if (GetExistingReturnValue(moduleName, ref returnValue))
                return returnValue;

            ExecuteModule(moduleName, environment, ref returnValue);
            if (
              returnValue == null
              || returnValue.IsNil()
              || returnValue.IsVoid()
            )
                returnValue = DynValue.NewBoolean(true);
            SaveReturnValue(moduleName, returnValue);
            return returnValue;
        }

        public LuaRequire(Script lua)
        {
            this.lua = lua;
            loadedModules = new Dictionary<string, DynValue>();
        }
    }
}