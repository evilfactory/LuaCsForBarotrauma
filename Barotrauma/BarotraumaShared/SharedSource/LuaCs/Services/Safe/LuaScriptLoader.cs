using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using System.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Safe;

namespace Barotrauma.LuaCs.Services.Safe
{
    public class LuaScriptLoader : ScriptLoaderBase, ILuaScriptLoader
    {
        public LuaScriptLoader(IStorageService storageService, Lazy<ILoggerService> loggerService, ILuaScriptServicesConfig  luaScriptServicesConfig)
        {
            this._storageService = storageService;
            this._loggerService = loggerService;
            this._luaScriptServicesConfig = luaScriptServicesConfig;
            _storageService.UseCaching = false;
        }
        
        private readonly IStorageService _storageService;
        private readonly Lazy<ILoggerService> _loggerService;
        private readonly ILuaScriptServicesConfig _luaScriptServicesConfig;
        
        public override object LoadFile(string file, Table globalContext)
        {
            ((IService)this).CheckDisposed();

            if (!CanReadFromPath(file))
            {
                LogErrors<string>($"File access to \"{file}\" is not allowed.");
                return null;
            }

            if (_storageService.TryLoadText(file) is not { IsSuccess: true, Value: not null } script)
            {
                LogErrors<string>($"Failed to load file \"{file}\".");
                return null;
            }

            if (script.Value.IsNullOrWhiteSpace())
            {
                LogErrors<string>($"The file \"{file}\" was empty.");
                return null;
            }

            return script.Value;
        }

        public override bool ScriptFileExists(string file)
        {
            ((IService)this).CheckDisposed();

            if (!CanReadFromPath(file))
            {
                LogErrors<string>($"File access to \"{file}\" is not allowed.");
                return false;
            }

            var result = _storageService.FileExists(file);
            
            if (result is { IsFailed: true })
            {
                LogErrors<string>($"Unable to find and load file \"{file}\".");
                return false;
            }
            
            return result.IsSuccess;
        }

        private bool CanReadFromPath(string file)
        {
            return _storageService.IsFileAccessible(file, true, false);
        }

        private bool CanWriteToPath(string file)
        {
            return _storageService.IsFileAccessible(file, false, false);
        }

        private void LogErrors<T>(string message, FluentResults.Result<T> result = null)
        {
            _loggerService.Value.LogError($"{nameof(LuaScriptLoader)}: {message}");
            
            if (result is null || result.Errors.Count <= 0) 
                return;
            
            foreach (var error in result.Errors)
            {
                _loggerService.Value.LogError($"{nameof(LuaScriptLoader)}: Error: {error.Message}.");
            }
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;
            
            _storageService.Dispose();
            _loggerService.Value.Dispose();
        }

        public bool IsDisposed { get; private set; }
    }
}
