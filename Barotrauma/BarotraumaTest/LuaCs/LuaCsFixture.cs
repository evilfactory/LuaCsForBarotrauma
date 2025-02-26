/*
extern alias Client;

using Client::Barotrauma;
using System;
using System.Runtime.ExceptionServices;

// TODO: Rewrite all of this.

namespace TestProject.LuaCs
{
    /// <summary>
    /// Shared LuaCsSetup instance.
    /// </summary>
    /// <remarks>
    /// Don't use this unless you need to test logic that makes use of
    /// a shared state (static variables).
    /// </remarks>
    public class LuaCsFixture : IDisposable
    {
        public LuaCsFixture()
        {
            LuaCsLogger.ExceptionHandler = (ex, _) =>
            {
                // Pretend we never caught the exception in the first place
                // (this allows us to preserve the stack trace)
                var di = ExceptionDispatchInfo.Capture(ex);
                di.Throw();
            };
        }

        internal LuaCsSetup LuaCs { get; } = new();

        void IDisposable.Dispose() => LuaCs.Stop();
    }
}
*/
