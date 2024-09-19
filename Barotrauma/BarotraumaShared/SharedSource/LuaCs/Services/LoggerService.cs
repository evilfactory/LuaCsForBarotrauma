using System;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using MoonSharp.Interpreter;

namespace Barotrauma.LuaCs.Services;

public partial class LoggerService : ILoggerService
{
    public bool HideUserNames = true;

#if SERVER
        private const string LogPrefix = "SV";
        private const int NetMaxLength = 1024;  // character limit of vanilla Barotrauma's chat system.
        private const int NetMaxMessages = 60;

        // This is used so it's possible to call logging functions inside the serverLog
        // hook without creating an infinite loop
        private bool _lockLog = false;
#else
    private const string LogPrefix = "CL";
#endif
    
    public void HandleException(Exception exception, string prefix = null)
    {
        string errorString = "";
        switch (exception)
        {
            case NetRuntimeException netRuntimeException:
                if (netRuntimeException.DecoratedMessage == null)
                {
                    errorString = $"{prefix ?? ""}{netRuntimeException.ToString()}";
                }
                else
                {
                    // FIXME: netRuntimeException.ToString() doesn't print the InnerException's stack trace...
                    errorString = $"{prefix ?? ""}{netRuntimeException.DecoratedMessage}: {netRuntimeException}";
                }
                break;
            case InterpreterException interpreterException:
                if (interpreterException.DecoratedMessage == null)
                {
                    errorString = $"{prefix ?? ""}{interpreterException.ToString()}";
                }
                else
                {
                    errorString = $"{prefix ?? ""}{interpreterException.DecoratedMessage}";
                }
                break;
            default:
                string s = exception.StackTrace != null ? exception.ToString() : $"{exception}\n{Environment.StackTrace}";
                errorString = $"{prefix ?? ""}{s}";
                break;
        }
             
        LogError(prefix + Environment.UserName + " " + errorString);
    }

    public void LogError(string message)
    {
        if (HideUserNames && !Environment.UserName.IsNullOrEmpty())
        {
            message = message.Replace(Environment.UserName, "USERNAME");
        }

        Log($"{message}", Color.Red, ServerLog.MessageType.Error);
    }

    public void LogMessage(string message, Color? serverColor = null, Color? clientColor = null)
    {
        serverColor ??= Color.MediumPurple;
        clientColor ??= Color.Purple;

#if SERVER
        Log(message, serverColor);
#else
        Log(message, clientColor);
#endif
    }

    public void Log(string message, Color? color = null, ServerLog.MessageType messageType = ServerLog.MessageType.ServerMessage)
    {
        DebugConsole.NewMessage(message, color);

#if SERVER
        void BroadcastMessage(string m)
        {
            foreach (var client in GameMain.Server.ConnectedClients)
            {
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

                if (!_lockLog)
                {
                    _lockLog = true;
                    GameMain.LuaCs?.Hook?.Call("serverLog", logMessage, messageType);
                    _lockLog = false;
                }
            }

            for (int i = 0; i < message.Length; i += NetMaxLength)
            {
                string subStr = message.Substring(i, Math.Min(1024, message.Length - i));
                BroadcastMessage(subStr);
            }
        }
#endif
    }

    public void Dispose() { }
}
