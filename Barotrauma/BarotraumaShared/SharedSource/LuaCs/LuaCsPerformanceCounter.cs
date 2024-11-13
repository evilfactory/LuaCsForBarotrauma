using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Barotrauma
{
    public class LuaCsPerformanceCounter
    {
        public bool EnablePerformanceCounter = false;

        public double UpdateElapsedTime;
        public Dictionary<string, Dictionary<string, double>> HookElapsedTime = new Dictionary<string, Dictionary<string, double>>();

        public static double MemoryUsage
        {
            get
            {
                Process proc = Process.GetCurrentProcess();
                double memory = Math.Round(proc.PrivateMemorySize64 / (1024.0 * 1024.0), 2);
                proc.Dispose();
                return memory;
            }
        }

        public void SetHookElapsedTicks(string eventName, string hookName, long ticks)
        {
            if (!HookElapsedTime.ContainsKey(eventName)) 
            { 
                HookElapsedTime[eventName] = new Dictionary<string, double>(); 
            }

            HookElapsedTime[eventName][hookName] = (double)ticks / Stopwatch.Frequency;
        }
    }
}
