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
