using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.IO;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using System.Linq;
using System.Threading.Tasks;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services.Safe;
using FluentResults;

namespace Barotrauma.LuaCs.Services.Safe
{
    public class LuaScriptLoader : ScriptLoaderBase, ILuaScriptLoader
    {
        public LuaScriptLoader(ISafeStorageService storageService, Lazy<ILoggerService> loggerService)
        {
            this._storageService = storageService;
            this._loggerService = loggerService;
            storageService.UseCaching = true;
        }
        
        private readonly ISafeStorageService _storageService;
        private readonly Lazy<ILoggerService> _loggerService;
        
        public override object LoadFile(string file, Table globalContext)
        {
            IService.CheckDisposed(this);
            if (file.IsNullOrWhiteSpace())
            {
                return null;
            }
            
            var res = _storageService.TryLoadText(file);

            if (res.IsFailed || res is not { Value: { } script})
            {
                UnsafeLogErrors($"Failed to load file '{file}'.", res.ToResult());
                return null;
            }

            if (script.IsNullOrWhiteSpace())
            {
                UnsafeLogErrors($"The file '{file}' is  empty. ", res.ToResult());
                return null;
            }

            return script;
        }

        public void ClearCaches()
        {
            IService.CheckDisposed(this);
            _storageService?.PurgeCache();
        }

        public async Task<Result<ImmutableArray<(ContentPath Path, Result<string>)>>> CacheResourcesAsync(ImmutableArray<ILuaScriptResourceInfo> resourceInfos)
        {
            IService.CheckDisposed(this);
            if (!_storageService.UseCaching)
            {
                return FluentResults.Result.Fail($"Caching is not enabled.");
            }
            
            return await this._storageService.LoadPackageTextFilesAsync([..resourceInfos.SelectMany(ri => ri.FilePaths)]);
        }

        public override bool ScriptFileExists(string file)
        {
            IService.CheckDisposed(this);
            var result = _storageService.FileExists(file);
            if (result is { IsFailed: true })
            {
                UnsafeLogErrors($"Unable to find and load file \"{file}\".", result.ToResult());
                return false;
            }
            
            return true;
        }

        private void UnsafeLogErrors(string message, FluentResults.Result result = null)
        {
            _loggerService.Value.LogError($"{nameof(LuaScriptLoader)}: {message}");
            if (result is null || result.Errors.Count <= 0)
            {
                return;
            }
            
            foreach (var error in result.Errors)
            {
                _loggerService.Value.LogError($"{nameof(LuaScriptLoader)}: Error: {error.Message}.");
            }
        }

        public void Dispose()
        {
            if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
            {
                return;
            }
            
            _storageService?.Dispose();
            _loggerService?.Value.Dispose();
        }

        private int _isDisposed = 0;
        public bool IsDisposed => ModUtils.Threading.GetBool(ref _isDisposed);
        
        public bool IsFileAccessible(string path, bool readOnly, bool checkWhitelistOnly = true)
        {
            IService.CheckDisposed(this);
            return _storageService.IsFileAccessible(path, readOnly, checkWhitelistOnly);
        }

        public void AddFileToWhitelist(string path, bool readOnly = true)
        {
            IService.CheckDisposed(this);
            _storageService.AddFileToWhitelist(path, readOnly);
        }

        public void AddFilesToWhitelist(ImmutableArray<string> paths, bool readOnly = true)
        {
            IService.CheckDisposed(this);
            _storageService.AddFilesToWhitelist(paths, readOnly);
        }

        public void RemoveFileFromAllWhitelists(string path)
        {
            IService.CheckDisposed(this);
            _storageService.RemoveFileFromAllWhitelists(path);
        }

        public FluentResults.Result SetReadOnlyWhitelist(ImmutableArray<string> filePaths)
        {
            IService.CheckDisposed(this);
            return  _storageService.SetReadOnlyWhitelist(filePaths);
        }

        public FluentResults.Result SetReadWriteWhitelist(ImmutableArray<string> filePaths)
        {
            IService.CheckDisposed(this);
            return _storageService.SetReadWriteWhitelist(filePaths);
        }

        public void ClearAllWhitelists()
        {
            IService.CheckDisposed(this);
            _storageService.ClearAllWhitelists();
        }
    }
}
