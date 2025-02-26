extern alias Client;

using Client::Barotrauma;
using MoonSharp.Interpreter;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Xunit;

namespace TestProject.LuaCs
{
    internal static class HookPatchHelpers
    {
        public class PatchHandle : IDisposable
        {
            private readonly Action disposeAction;

            public PatchHandle(string patchId, Action disposeAction)
            {
                this.disposeAction = disposeAction;
                this.PatchId = patchId;
            }

            public string PatchId { get; }

            public void Dispose() => disposeAction();
        }

        private static List<string> BuildHookPatchArgsList(
            string? patchId,
            string className,
            string methodName,
            string[]? parameters)
        {
            static string Stringify(object value) =>
                "\"" + value.ToString()!.Replace(@"\", @"\\").Replace("\"", "\\\"") + "\"";

            var args = new List<string>();
            if (patchId != null) args.Add(Stringify(patchId));
            args.Add(Stringify(className));
            args.Add(Stringify(methodName));
            if (parameters != null)
            {
                var sb = new StringBuilder();
                sb.Append("{ ");
                foreach (var param in parameters)
                {
                    sb.Append(Stringify(param));
                    sb.Append(", ");
                }
                sb.Append(" }");
                args.Add(sb.ToString());
            }
            return args;
        }

        private static DynValue DoHookPatch(
            this LuaCsSetup luaCs,
            string? patchId,
            string className,
            string methodName,
            string[]? parameters,
            string function,
            LuaCsHook.HookMethodType patchType)
        {
            var args = BuildHookPatchArgsList(patchId, className, methodName, parameters);
            args.Add(function);
            args.Add(patchType switch
            {
                LuaCsHook.HookMethodType.Before => "Hook.HookMethodType.Before",
                LuaCsHook.HookMethodType.After => "Hook.HookMethodType.After",
                _ => throw new NotImplementedException(),
            });
            throw new NotImplementedException();
            //return luaCs.Lua.DoString($"return Hook.Patch({string.Join(", ", args)})");
        }

        private static DynValue DoHookRemovePatch(
            this LuaCsSetup luaCs,
            string? patchId,
            string className,
            string methodName,
            string[]? parameters,
            LuaCsHook.HookMethodType patchType)
        {
            var args = BuildHookPatchArgsList(patchId, className, methodName, parameters);
            args.Add(patchType switch
            {
                LuaCsHook.HookMethodType.Before => "Hook.HookMethodType.Before",
                LuaCsHook.HookMethodType.After => "Hook.HookMethodType.After",
                _ => throw new NotImplementedException(),
            });
            throw new NotImplementedException();
            //return luaCs.Lua.DoString($"return Hook.RemovePatch({string.Join(", ", args)})");
        }

        public static PatchHandle AddPrefix<T>(this LuaCsSetup luaCs, string body, string methodName, string[]? parameters = null, string? patchId = null)
        {
            var className = typeof(T).FullName!;
            var returnValue = luaCs.DoHookPatch(patchId, className, methodName, parameters, @$"
                function(instance, ptable)
                {body}
                end
            ", LuaCsHook.HookMethodType.Before);
            Assert.Equal(DataType.String, returnValue.Type);
            return new(returnValue.String, () => luaCs.RemovePrefix<T>(returnValue.String, methodName, parameters));
        }

        public static PatchHandle AddPostfix<T>(this LuaCsSetup luaCs, string body, string methodName, string[]? parameters = null, string? patchId = null)
        {
            var className = typeof(T).FullName!;
            var returnValue = luaCs.DoHookPatch(patchId, className, methodName, parameters, @$"
                function(instance, ptable)
                {body}
                end
            ", LuaCsHook.HookMethodType.After);
            Assert.Equal(DataType.String, returnValue.Type);
            return new(returnValue.String, () => luaCs.RemovePostfix<T>(returnValue.String, methodName, parameters));
        }

        public static bool RemovePrefix<T>(this LuaCsSetup luaCs, string patchId, string methodName, string[]? parameters = null)
        {
            var className = typeof(T).FullName!;
            var returnValue = luaCs.DoHookRemovePatch(patchId, className, methodName, parameters, LuaCsHook.HookMethodType.Before);
            Assert.Equal(DataType.Boolean, returnValue.Type);
            return returnValue.Boolean;
        }

        public static bool RemovePostfix<T>(this LuaCsSetup luaCs, string patchId, string methodName, string[]? parameters = null)
        {
            var className = typeof(T).FullName!;
            var returnValue = luaCs.DoHookRemovePatch(patchId, className, methodName, parameters, LuaCsHook.HookMethodType.After);
            Assert.Equal(DataType.Boolean, returnValue.Type);
            return returnValue.Boolean;
        }

        public class PatchTargetHandle : IDisposable
        {
            private readonly SemaphoreSlim @lock;

            public PatchTargetHandle(SemaphoreSlim @lock)
            {
                this.@lock = @lock;
            }

            public void Dispose() => @lock.Release();
        }

        private static readonly ConcurrentDictionary<Type, SemaphoreSlim> PatchTargetLocks = new();

        public static PatchTargetHandle LockPatchTarget<T>()
        {
            if (!PatchTargetLocks.TryGetValue(typeof(T), out var @lock))
            {
                PatchTargetLocks[typeof(T)] = @lock = new SemaphoreSlim(1);
            }
            @lock.Wait();
            return new(@lock);
        }
    }
}
