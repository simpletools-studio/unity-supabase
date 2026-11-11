using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Supabase.Unity.Tests
{
    public sealed class RuntimeLifecycleTests
    {
        [UnityTest]
        public IEnumerator FocusReturn_RefreshesAnExpiringSession()
        {
            var transport = new RecordingHttpTransport()
                .Enqueue(200, AuthTests.SessionJson.Replace("3600", "30"))
                .Enqueue(200, AuthTests.SessionJson.Replace("access-one", "access-two"));
            var options = ConfigurationTests.ValidOptions();
            options.HttpTransport = transport;
            options.AutoRefreshToken = false;
            var gameObject = new GameObject("Focus routing test");
            try
            {
                using (var client = new SupabaseClient(options))
                {
                    var signIn = client.Auth.SignInWithPasswordAsync("a@b.co", "fixture-password");
                    Assert.IsTrue(signIn.GetAwaiter().GetResult().IsSuccess);
                    var host = gameObject.AddComponent<SupabaseRuntimeHost>();
                    var callback = typeof(SupabaseRuntimeHost).GetMethod("OnApplicationFocus",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    callback.Invoke(host, new object[] { false });
                    Assert.AreEqual(1, transport.Requests.Count);
                    callback.Invoke(host, new object[] { true });

                    var deadline = Time.realtimeSinceStartup + 5;
                    while (client.Auth.CurrentSession.AccessToken != "access-two" &&
                        Time.realtimeSinceStartup < deadline)
                        yield return null;

                    Assert.AreEqual("access-two", client.Auth.CurrentSession.AccessToken);
                    Assert.AreEqual(2, transport.Requests.Count);
                    StringAssert.Contains("grant_type=refresh_token", transport.LastRequest.Uri.Query);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator DisposingRuntimeTransport_CancelsAnInFlightRequest()
        {
            yield return new EnterPlayMode();

            using (var releaseResponse = new ManualResetEventSlim())
            using (var fixture = new LoopbackSupabaseFixture(request =>
            {
                releaseResponse.Wait(10000);
                return ContractHttpResponse.Json(200, "{}");
            }))
            using (var transport = new UnityWebRequestTransport())
            {
                try
                {
                    var request = transport.SendAsync(new SupabaseHttpRequest
                    {
                        Uri = fixture.ProjectUrl,
                        Method = SupabaseHttpMethod.Post,
                        Body = new byte[] { 123, 125 },
                        ContentType = "application/json",
                        Timeout = TimeSpan.FromSeconds(15)
                    });
                    var deadline = Time.realtimeSinceStartup + 5;
                    while (fixture.Requests.Count == 0 && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.AreEqual(1, fixture.Requests.Count);
                    Assert.AreEqual("POST", fixture.Requests[0].Method);

                    transport.Dispose();
                    deadline = Time.realtimeSinceStartup + 3;
                    while (!request.IsCompleted && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.IsTrue(request.IsCanceled, "Disposal must cancel the request before the server responds.");
                    Assert.AreEqual(1, fixture.Requests.Count, "Cancellation must not replay a write.");
                }
                finally
                {
                    releaseResponse.Set();
                }
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RepeatedPlaySessions_DoNotReconnectAfterManualDisconnect()
        {
            var previousEnabled = UnityEditor.EditorSettings.enterPlayModeOptionsEnabled;
            var previousOptions = UnityEditor.EditorSettings.enterPlayModeOptions;
            UnityEditor.EditorSettings.enterPlayModeOptionsEnabled = true;
            UnityEditor.EditorSettings.enterPlayModeOptions =
                UnityEditor.EnterPlayModeOptions.DisableDomainReload |
                UnityEditor.EnterPlayModeOptions.DisableSceneReload;
            try
            {
                for (var session = 0; session < 2; session++)
                {
                    yield return new EnterPlayMode(false);
                    var createdSockets = new List<RecordingWebSocketTransport>();
                    var options = ConfigurationTests.ValidOptions();
                    options.HttpTransport = new RecordingHttpTransport();
                    options.WebSocketTransportFactory = () =>
                    {
                        var socket = new RecordingWebSocketTransport();
                        createdSockets.Add(socket);
                        return socket;
                    };

                    using (var client = new SupabaseClient(options))
                    {
                        Assert.IsTrue(client.Realtime.ConnectAsync().GetAwaiter().GetResult().IsSuccess);
                        createdSockets[0].CloseAsync(1006, "Fixture disconnect").GetAwaiter().GetResult();
                        Assert.IsTrue(client.Realtime.DisconnectAsync().GetAwaiter().GetResult().IsSuccess);
                        var deadline = Time.realtimeSinceStartup + 1.25f;
                        while (Time.realtimeSinceStartup < deadline)
                            yield return null;
                        Assert.AreEqual(1, createdSockets.Count, "A pending reconnect must respect manual disconnect.");
                        Assert.IsFalse(client.Realtime.IsConnected);
                    }

                    var timer = SupabaseRuntimeHost.Delay(TimeSpan.FromMinutes(1), CancellationToken.None);
                    yield return new ExitPlayMode();
                    Assert.IsTrue(timer.IsCanceled, "Play exit must settle outstanding runtime timers.");
                }
            }
            finally
            {
                UnityEditor.EditorSettings.enterPlayModeOptions = previousOptions;
                UnityEditor.EditorSettings.enterPlayModeOptionsEnabled = previousEnabled;
            }
        }

        [UnityTest]
        public IEnumerator RealtimeReconnectSoak_DrainsTimersAndPendingPushes()
        {
            yield return new EnterPlayMode();
            const int warmupCycles = 20;
            const int measuredCycles = 200;
            const long retainedHeapLimit = 2 * 1024 * 1024;
            var scheduled = (System.Collections.IDictionary)typeof(SupabaseRuntimeHost)
                .GetField("Scheduled", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            long before = 0;
            var started = Time.realtimeSinceStartup;
            for (var cycle = 0; cycle < warmupCycles + measuredCycles; cycle++)
            {
                var options = ConfigurationTests.ValidOptions();
                options.HttpTransport = new RecordingHttpTransport();
                RecordingWebSocketTransport previous = null;
                options.WebSocketTransportFactory = () =>
                {
                    if (previous != null)
                        Assert.AreEqual(SupabaseWebSocketState.Closed, previous.State);
                    previous = new RecordingWebSocketTransport { AcknowledgeBroadcast = true };
                    return previous;
                };
                using (var client = new SupabaseClient(options))
                {
                    var channel = client.Realtime.Channel("fixture");
                    Assert.IsTrue(channel.SubscribeAsync().GetAwaiter().GetResult().IsSuccess);
                    Assert.IsTrue(client.Realtime.ReconnectAsync().GetAwaiter().GetResult().IsSuccess);
                    Assert.AreEqual(RealtimeChannelState.Joined, channel.State);
                    Assert.AreEqual(0, client.Realtime.PendingPushCount);
                }
                yield return null;
                yield return null;
                Assert.AreEqual(SupabaseWebSocketState.Closed, previous.State);
                Assert.AreEqual(0, scheduled.Count, "Disposed clients must release their scheduled timers.");
                if (cycle == warmupCycles - 1)
                    before = GC.GetTotalMemory(true);
            }
            var retained = GC.GetTotalMemory(true) - before;
            Debug.Log("Realtime reconnect soak: " + measuredCycles + " measured cycles in " +
                (Time.realtimeSinceStartup - started).ToString("F2") + " seconds, retained managed heap delta " +
                retained + " bytes.");
            Assert.Less(retained, retainedHeapLimit);
            yield return new ExitPlayMode();
        }

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
