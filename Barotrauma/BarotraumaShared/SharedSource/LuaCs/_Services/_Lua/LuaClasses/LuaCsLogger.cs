using System;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using MoonSharp.Interpreter;

namespace Barotrauma
{
    internal enum LuaCsMessageOrigin
    {
        LuaCs,
        Unknown,
        LuaMod,
        CSharpMod,
    }

    partial class LuaCsLogger
    {
        public static void HandleException(Exception ex, LuaCsMessageOrigin origin)
        {
            GameMain.LuaCs.Logger.HandleException(ex);
        }

        public static void LogError(string message, LuaCsMessageOrigin origin)
        {
            GameMain.LuaCs.Logger.LogError(message);
        }

        public static void LogError(string message)
        {
            GameMain.LuaCs.Logger.LogError(message);
        }

        public static void LogMessage(string message, Color? serverColor = null, Color? clientColor = null)
        {
            GameMain.LuaCs.Logger.LogMessage(message, serverColor, clientColor);
        }

        public static void Log(string message, Color? color = null, ServerLog.MessageType messageType = ServerLog.MessageType.ServerMessage)
        {
            GameMain.LuaCs.Logger.Log(message, color, messageType);
        }
    }

    partial class LuaCsSetup
    {
        // Compatibility with cs mods that use this method.
        public static void PrintLuaError(object message) => GameMain.LuaCs.Logger.LogError($"{message}");
        public static void PrintCsError(object message) => GameMain.LuaCs.Logger.LogError($"{message}");
        public static void PrintGenericError(object message) => GameMain.LuaCs.Logger.LogError($"{message}");

        internal void PrintMessage(object message) => GameMain.LuaCs.Logger.LogMessage($"{message}");

        public static void PrintCsMessage(object message) => GameMain.LuaCs.Logger.LogMessage($"{message}");

        internal void HandleException(Exception ex, LuaCsMessageOrigin origin) => GameMain.LuaCs.Logger.HandleException(ex);
    }
}
