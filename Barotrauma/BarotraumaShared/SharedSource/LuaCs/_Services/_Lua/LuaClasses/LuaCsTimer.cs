using Barotrauma.LuaCs.Events;
using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Compatibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Barotrauma
{
    public class LuaCsTimer : ILuaCsTimer, IEventUpdate
    {
        public static double Time => Timing.TotalTime;
        public static double GetTime() => Time;
        public static double AccumulatorMax
        {
            get
            {
                return Timing.AccumulatorMax;
            }
            set
            {
                Timing.AccumulatorMax = value;
            }
        }

        private class TimerComparer : IComparer<TimedAction>
        {
            public int Compare(TimedAction timedAction1, TimedAction timedAction2)
            {
                if (timedAction1 == null || timedAction2 == null)
                    return 0;
                return -Math.Sign(timedAction2.ExecutionTime - timedAction1.ExecutionTime);
            }
        }

        private class TimedAction
        {
            public LuaCsAction Action
            {
                get;
                private set;
            }

            public double ExecutionTime
            {
                get;
                private set;
            }
            
            public TimedAction(LuaCsAction action, int delayMs)
            {
                this.Action = action;
                ExecutionTime = Time + (delayMs / 1000f);
            }
        }
        
        private List<TimedAction> timedActions = new List<TimedAction>();

        private readonly IEventService _eventService;

        public LuaCsTimer(IEventService eventService)
        {
            _eventService = eventService;
            _eventService.Subscribe<IEventUpdate>(this);
        }

        private void AddTimer(TimedAction timedAction)
        {
            if (timedAction == null)
            {
                throw new ArgumentNullException(nameof(timedAction));
            }

            lock (timedActions)
            {
                int insertionPoint = timedActions.BinarySearch(timedAction, new TimerComparer());

                if (insertionPoint < 0)
                {
                    insertionPoint = ~insertionPoint;
                }

                timedActions.Insert(insertionPoint, timedAction);
            }
        }

        public void Clear()
        {
            timedActions = new List<TimedAction>();
        }

        public void Wait(LuaCsAction action, int millisecondDelay)
        {
            TimedAction timedAction = new TimedAction(action, millisecondDelay);
            AddTimer(timedAction);
        }

        public void NextFrame(LuaCsAction action)
        {
            TimedAction timedAction = new TimedAction(action, 0);
            AddTimer(timedAction);
        }

        public void Dispose()
        {
            _eventService.Unsubscribe<IEventUpdate>(this);
        }

        public void OnUpdate(double fixedDeltaTime)
        {
            lock (timedActions)
            {
                TimedAction[] timedCopy = timedActions.ToArray();
                for (int i = 0; i < timedCopy.Length; i++)
                {
                    TimedAction timedAction = timedCopy[i];
                    if (Time >= timedAction.ExecutionTime)
                    {
                        try
                        {
                            timedAction.Action();
                        }
                        catch (Exception e)
                        {
                            LuaCsLogger.HandleException(e, LuaCsMessageOrigin.CSharpMod);
                        }

                        timedActions.Remove(timedAction);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        public bool IsDisposed => false;
    }
}
