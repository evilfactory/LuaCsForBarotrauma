using System;
using Barotrauma.LuaCs.Events;

namespace Barotrauma;

public interface IAssemblyPlugin : IDisposable, IEventPluginPreInitialize, IEventPluginInitialize, IEventPluginLoadCompleted { }
