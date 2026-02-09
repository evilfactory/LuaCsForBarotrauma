using Barotrauma.Networking;
using FluentResults;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MoonSharp.Interpreter;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Barotrauma.LuaCs;

public partial class LoggerService : ILoggerService
{
    public bool HideUserNames = true;

    private List<ILoggerSubscriber> logSubscribers = [];
    private ConcurrentQueue<PendingLog> logQueue = [];

#if SERVER
    private const string TargetPrefix = "[SV]";
    private const int NetMaxLength = 1024;  // character limit of vanilla Barotrauma's chat system.
    private const int NetMaxMessages = 60;

    // This is used so it's possible to call logging functions inside the serverLog
    // hook without creating an infinite loop
    private bool _isInsideLogCall = false;
#else
    private const string TargetPrefix = "[CL]";
#endif

    public LoggerService() { }

    public void Subscribe(ILoggerSubscriber subscriber)
    {
        logSubscribers.Add(subscriber);
    }

    public void Unsubscribe(ILoggerSubscriber subscriber)
    {
        logSubscribers.Remove(subscriber);
    }

    public void ProcessLogs()
    {
        while (logQueue.TryDequeue(out PendingLog log))
        {
            logSubscribers.ForEach(s => s.OnLog(log));

            DebugConsole.NewMessage(log.Message, log.Color);

#if SERVER
            if (GameMain.Server != null)
            {
                if (GameMain.Server.ServerSettings.SaveServerLogs)
                {
                    string logMessage = "[LuaCs] " + log.Message;
                    GameMain.Server.ServerSettings.ServerLog.WriteLine(logMessage, log.MessageType, false);

                    if (!_isInsideLogCall)
                    {
                        _isInsideLogCall = true;
                        GameMain.LuaCs?.Hook?.Call("serverLog", logMessage, log.MessageType);
                        _isInsideLogCall = false;
                    }
                }

                for (int i = 0; i < log.Message.Length; i += NetMaxLength)
                {
                    string subStr = log.Message.Substring(i, Math.Min(1024, log.Message.Length - i));
                    BroadcastMessage(subStr);
                }
            }

            void BroadcastMessage(string m)
            {
                foreach (var client in GameMain.Server.ConnectedClients)
                {
                    ChatMessage consoleMessage = ChatMessage.Create("", m, ChatMessageType.Console, null, textColor: log.Color);
                    GameMain.Server.SendDirectChatMessage(consoleMessage, client);

                    if (!GameMain.Server.ServerSettings.SaveServerLogs || !client.HasPermission(ClientPermissions.ServerLog))
                    {
                        continue;
                    }

                    ChatMessage logMessage = ChatMessage.Create(log.MessageType.ToString(), "[LuaCs] " + m, ChatMessageType.ServerLog, null);
                    GameMain.Server.SendDirectChatMessage(logMessage, client);
                }
            }
#endif
        }
    }

    public void Log(string message, Color? color = null, ServerLog.MessageType messageType = ServerLog.MessageType.ServerMessage)
    {
        if (HideUserNames && !Environment.UserName.IsNullOrEmpty())
        {
            message = message.Replace(Environment.UserName, "USERNAME");
        }

        message = $"{TargetPrefix} {message}";

        logQueue.Enqueue(new PendingLog(message, color, messageType));
    }

    public void LogError(string message)
    {
        Log($"{message}", Color.Red, ServerLog.MessageType.Error);
    }

    public void LogWarning(string message)
    {
        Log($"{message}", Color.Yellow, ServerLog.MessageType.ServerMessage);
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


    public void LogResults(FluentResults.Result result)
    {
        if (result == null)
        {
            LogError("Result is null");
            return;
        }

        if (result.IsSuccess)
        {
            return;
        }

        if (result.IsFailed)
        {
            foreach (var error in result.Errors)
            {
                if (error is ExceptionalError exceptionalError)
                {
                    HandleException(exceptionalError.Exception);
                }
                else
                {
                    LogError($"FluentResults::IError: {error.Message}");
                }

                if (error.Reasons != null)
                {
                    foreach (var reason in error.Reasons)
                    {
                        LogError($" - {reason.Message}");
                    }
                }
            }
        }
    }

    public void LogDebug(string message, Color? color = null)
    {
        Log(message, color ?? Color.Purple);
    }

    public void LogDebugWarning(string message)
    {
        Log(message, Color.Yellow);
    }

    public void LogDebugError(string message)
    {
        Log(message, Color.Red);
    }

    public void Dispose() { }
    public FluentResults.Result Reset() => FluentResults.Result.Ok();
    
    public bool IsDisposed { get; }
}
