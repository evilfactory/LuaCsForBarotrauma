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
        public static bool HideUserNames = true;

#if SERVER
        private const string LogPrefix = "SV";
        private const int NetMaxLength = 1024;  // character limit of vanilla Barotrauma's chat system.
        private const int NetMaxMessages = 60;

        // This is used so its possible to call logging functions inside the serverLog
        // hook without creating an infinite loop
        private static bool lockLog = false;
#else
        private const string LogPrefix = "CL";
#endif

        public static LuaCsMessageLogger MessageLogger;
        public static LuaCsExceptionHandler ExceptionHandler;

        public static void HandleException(Exception ex, LuaCsMessageOrigin origin)
        {
            string errorString = "";
            switch (ex)
            {
                case NetRuntimeException netRuntimeException:
                    if (netRuntimeException.DecoratedMessage == null)
                    {
                        errorString = netRuntimeException.ToString();
                    }
                    else
                    {
                        // FIXME: netRuntimeException.ToString() doesn't print the InnerException's stack trace...
                        errorString = $"{netRuntimeException.DecoratedMessage}: {netRuntimeException}";
                    }
                    break;
                case InterpreterException interpreterException:
                    if (interpreterException.DecoratedMessage == null)
                    {
                        errorString = interpreterException.ToString();
                    }
                    else
                    {
                        errorString = interpreterException.DecoratedMessage;
                    }
                    break;
                default:
                    errorString = ex.StackTrace != null
                        ? ex.ToString()
                        : $"{ex}\n{Environment.StackTrace}";
                    break;
            }
             
            LogError(Environment.UserName + " " + errorString, origin);
        }

        public static void LogError(string message, LuaCsMessageOrigin origin)
        {
            if (HideUserNames && !Environment.UserName.IsNullOrEmpty())
            {
                message = message.Replace(Environment.UserName, "USERNAME");
            }

            switch (origin)
            {
                case LuaCsMessageOrigin.LuaCs:
                case LuaCsMessageOrigin.Unknown:
                    LogError($"[{LogPrefix} ERROR] {message}");
                    break;
                case LuaCsMessageOrigin.LuaMod:
                    LogError($"[{LogPrefix} LUA ERROR] {message}");
                    break;
                case LuaCsMessageOrigin.CSharpMod:
                    LogError($"[{LogPrefix} CS ERROR] {message}");
                    break;
            }
        }

        public static void LogError(string message)
        {
            Log($"{message}", Color.Red, ServerLog.MessageType.Error);
        }

        public static void LogMessage(string message, Color? serverColor = null, Color? clientColor = null)
        {
            if (serverColor == null) { serverColor = Color.MediumPurple; }
            if (clientColor == null) { clientColor = Color.Purple; }

#if SERVER
            Log(message, serverColor);
#else
            Log(message, clientColor);
#endif
        }

        public static void Log(string message, Color? color = null, ServerLog.MessageType messageType = ServerLog.MessageType.ServerMessage)
        {
            MessageLogger?.Invoke(message);

            DebugConsole.NewMessage(message, color);

#if SERVER
            void broadcastMessage(string m)
            {
                foreach (var client in GameMain.Server.ConnectedClients)
                {
                    //if (client.ChatMsgQueue.Count > NetMaxMessages)
                    //{
                        // If there's an error or message happening many times per second (inside Update loop for example)
                        // we will need to discart some messages so the client doesn't get overloaded by all
                        // those net messages.
                    //    continue;
                    //}

                    ChatMessage consoleMessage = ChatMessage.Create("", m, ChatMessageType.Console, null, textColor: color);
                    GameMain.Server.SendDirectChatMessage(consoleMessage, client);

                    if (!GameMain.Server.ServerSettings.SaveServerLogs || !client.HasPermission(ClientPermissions.ServerLog))
                    {
                        continue;
                    }

                    ChatMessage logMessage = ChatMessage.Create(messageType.ToString(), "[LuaCs] " + m, ChatMessageType.ServerLog, null);
                    GameMain.Server.SendDirectChatMessage(logMessage, client);
                }
            }

            if (GameMain.Server != null)
            {
                if (GameMain.Server.ServerSettings.SaveServerLogs)
                {
                    string logMessage = "[LuaCs] " + message;
                    GameMain.Server.ServerSettings.ServerLog.WriteLine(logMessage, messageType, false);

                    if (!lockLog)
                    {
                        lockLog = true;
                        GameMain.LuaCs?.Hook?.Call("serverLog", logMessage, messageType);
                        lockLog = false;
                    }
                }

                for (int i = 0; i < message.Length; i += NetMaxLength)
                {
                    string subStr = message.Substring(i, Math.Min(1024, message.Length - i));

                    broadcastMessage(subStr);
                }
            }
#endif
        }
    }

    partial class LuaCsSetup
    {
        // Compatibility with cs mods that use this method.
        public static void PrintLuaError(object message) => LuaCsLogger.LogError($"{message}", LuaCsMessageOrigin.LuaMod);
        public static void PrintCsError(object message) => LuaCsLogger.LogError($"{message}", LuaCsMessageOrigin.CSharpMod);
        public static void PrintGenericError(object message) => LuaCsLogger.LogError($"{message}", LuaCsMessageOrigin.LuaCs);

        internal void PrintMessage(object message) => LuaCsLogger.LogMessage($"{message}");

        public static void PrintCsMessage(object message) => LuaCsLogger.LogMessage($"{message}");

        internal void HandleException(Exception ex, LuaCsMessageOrigin origin) => LuaCsLogger.HandleException(ex, origin);
    }
}
