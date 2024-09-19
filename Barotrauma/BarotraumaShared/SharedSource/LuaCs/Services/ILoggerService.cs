using System;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;

namespace Barotrauma.LuaCs.Services;

/// <summary>
/// Provides console and debug logging services
/// </summary>
public interface ILoggerService : IService
{
    void HandleException(Exception exception, string prefix = null);
    void LogError(string message);
    void LogMessage(string message, Color? serverColor = null, Color? clientColor = null);
    void Log(string message, Color? color = null, ServerLog.MessageType messageType = ServerLog.MessageType.ServerMessage);
}
