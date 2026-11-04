using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

namespace Supabase.Unity.Tests
{
    public sealed class RuntimeLifecycleTests
    {
        [Test]
        public void NextPlaySession_DiscardsQueuedWorkAndEventSubscribers()
        {
            SupabaseRuntimeHost.ResetState();
            bool staleWorkRan = false;
            Action<bool> focus = delegate { staleWorkRan = true; };
            SupabaseRuntimeHost.FocusChanged += focus;
            SupabaseRuntimeHost.WebSocketOpened += delegate { staleWorkRan = true; };
            SupabaseRuntimeHost.WebSocketMessage += delegate { staleWorkRan = true; };
            SupabaseRuntimeHost.WebSocketClosed += delegate { staleWorkRan = true; };
            SupabaseRuntimeHost.WebSocketError += delegate { staleWorkRan = true; };
            var worker = new Thread(() => SupabaseRuntimeHost.Post(() => staleWorkRan = true));
            worker.Start();
            Assert.IsTrue(worker.Join(5000), "Worker did not queue its callback.");
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var pending = (Queue<Action>)typeof(SupabaseRuntimeHost).GetField("Pending", flags).GetValue(null);
            Assert.AreEqual(1, pending.Count);

            // Simulates SubsystemRegistration on a second entry with Domain Reload disabled.
            SupabaseRuntimeHost.ResetState();
            Assert.AreEqual(0, pending.Count, "Callbacks from the previous Play session must not run.");
            foreach (var name in new[] { "FocusChanged", "WebSocketOpened", "WebSocketMessage",
                "WebSocketClosed", "WebSocketError" })
                Assert.IsNull(typeof(SupabaseRuntimeHost).GetField(name, flags).GetValue(null), name);
            Assert.IsFalse(staleWorkRan);
        }
    }
}
