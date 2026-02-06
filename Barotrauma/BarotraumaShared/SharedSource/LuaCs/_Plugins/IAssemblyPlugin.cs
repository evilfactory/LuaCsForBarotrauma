using System;
using Barotrauma.LuaCs.Events;

namespace Barotrauma.LuaCs;

public interface IAssemblyPlugin : IDisposable, IEventPluginPreInitialize, IEventPluginInitialize, IEventPluginLoadCompleted { }
