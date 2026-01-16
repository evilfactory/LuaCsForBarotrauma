using System;
using System.IO;
using Barotrauma.Networking;

namespace Barotrauma;

partial class LuaCsSetup
{
    /// <summary>
    /// Handles changes in game states tracked by screen changes.
    /// </summary>
    /// <param name="screen">The new game screen.</param>
    public partial void OnScreenSelected(Screen screen)
    {
        // the server is always in the running state unless explicitly stopped.
        if (screen == UnimplementedScreen.Instance)
            SetRunState(RunState.Unloaded);
        SetRunState(RunState.Running);
    }
}
