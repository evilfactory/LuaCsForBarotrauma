using System.Collections.Generic;

namespace Barotrauma.LuaCs.Services.Compatibility;

internal partial interface ILuaCsTimer : ILuaCsShim
{
    public static double Time => Timing.TotalTime;
    public static double GetTime() => Time;
    public static double AccumulatorMax { get; set; }

    public void Clear();
    public void Wait(LuaCsAction action, int millisecondDelay);
    public void NextFrame(LuaCsAction action);
}
