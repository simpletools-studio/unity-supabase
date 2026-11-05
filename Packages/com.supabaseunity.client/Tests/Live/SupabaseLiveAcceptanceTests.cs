using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Supabase.Unity.LiveTests
{
    public sealed class SupabaseLiveAcceptanceTests
    {
        [UnityEngine.Scripting.Preserve, SupabaseTable("unity_acceptance_scores")]
        private sealed class AcceptanceScore
        {
            [SupabaseColumn("id")] public string Id { get; set; }
            [SupabaseColumn("user_id")] public string UserId { get; set; }
            [SupabaseColumn("run_id")] public string RunId { get; set; }
            [SupabaseColumn("score")] public int Score { get; set; }
            [SupabaseColumn("tags")] public string[] Tags { get; set; }
        }

        [UnityTest]
        public IEnumerator AuthDatabaseRealtimeStorageAndFunctions_WorkTogether()
        {
            return Run(Journey);
        }

        [UnityTest]
        public IEnumerator DatabaseAndStorage_IsolateTwoUsers()
        {
            return Run(Isolation);
        }

        [UnityTest]
        public IEnumerator BroadcastAndPresence_WorkBetweenTwoClients()
        {
            return Run(BroadcastAndPresence);
        }

        private static IEnumerator Run(Func<SupabaseClient, CancellationToken, Task> scenario)
        {
            var url = Environment.GetEnvironmentVariable("SUPABASE_TEST_URL");
            var key = Environment.GetEnvironmentVariable("SUPABASE_TEST_PUBLISHABLE_KEY");
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
                Assert.Ignore("Set SUPABASE_TEST_URL and SUPABASE_TEST_PUBLISHABLE_KEY to run live acceptance.");
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
            using (var client = NewClient())
            {
                var task = scenario(client, cancellation.Token);
                while (!task.IsCompleted) yield return null;
                task.GetAwaiter().GetResult();
            }
        }

        private static SupabaseClient NewClient()
        {
            return new SupabaseClient(new SupabaseClientOptions
            {
                ProjectUrl = Environment.GetEnvironmentVariable("SUPABASE_TEST_URL"),
                PublishableKey = Environment.GetEnvironmentVariable("SUPABASE_TEST_PUBLISHABLE_KEY"),
                AutoRefreshToken = false, PersistSession = false
            });
        }

        private static async Task Login(SupabaseClient client, CancellationToken token, bool second = false)
        {
            var email = Environment.GetEnvironmentVariable(second ? "SUPABASE_TEST_SECOND_EMAIL" : "SUPABASE_TEST_EMAIL");
            var password = Environment.GetEnvironmentVariable(second ? "SUPABASE_TEST_SECOND_PASSWORD" : "SUPABASE_TEST_PASSWORD");
            if (string.IsNullOrWhiteSpace(email)) email = second ? "unity-acceptance-second@example.test" : "unity-acceptance@example.test";
            if (string.IsNullOrWhiteSpace(password)) password = "Unity-Acceptance-123!";
            var result = await client.Auth.SignInWithPasswordAsync(email, password, cancellationToken: token);
            if (!result.IsSuccess)
            {
                var signup = await client.Auth.SignUpWithPasswordAsync(email, password, cancellationToken: token);
                Check(signup, "Auth fixture signup");
                if (client.Auth.CurrentSession == null)
                    Check(await client.Auth.SignInWithPasswordAsync(email, password, cancellationToken: token), "Auth fixture sign-in");
            }
            Assert.IsNotNull(client.Auth.CurrentSession, "Fixture account requires a confirmed email/session.");
        }

        private static async Task Journey(SupabaseClient client, CancellationToken token)
        {
            await Login(client, token);
            Check(await client.Auth.RefreshSessionAsync(cancellationToken: token), "Auth refresh");
            Check(await client.Auth.GetUserAsync(token), "Auth user");
            var run = Guid.NewGuid().ToString("N");
            var path = client.Auth.CurrentUser.Id + "/" + run + ".txt";
            var moved = path + ".moved.txt";
            var copied = path + ".copy.txt";
            var signed = path + ".signed.txt";
            var bucket = client.Storage.From("unity-acceptance");
            try
            {
                RealtimePostgresChange observed = null;
                var channel = client.Realtime.Channel("acceptance:" + run)
                    .OnPostgresChanges(new RealtimePostgresChangeFilter
                    {
                        Event = RealtimePostgresEvent.Insert, Table = "unity_acceptance_scores", Filter = "run_id=eq." + run
                    }, change => observed = change);
                RealtimeSystemMessage status = null;
                channel.SystemMessageReceived += message => { if (message.Extension == "postgres_changes") status = message; };
                Check(await SubscribeWithWarmup(channel, token), "Realtime subscribe");
                await Until(() => status != null, token, "Postgres subscription status");
                Assert.AreEqual("ok", status.Status, "Postgres subscription must be ready before writing.");
                var inserted = await client.From<AcceptanceScore>().InsertAsync(new AcceptanceScore
                { RunId = run, Score = 41, Tags = new[] { "unity", "sdk" } }, cancellationToken: token);
                Check(inserted, "Database insert");
                Assert.AreEqual(client.Auth.CurrentUser.Id, inserted.Data[0].UserId);
                await Until(() => observed != null, token, "Postgres insert delivery");
                var selected = await client.From<AcceptanceScore>().Eq("run_id", run).SingleAsync(token);
                Check(selected, "Database single"); Assert.AreEqual(41, selected.Data.Score);
                var missing = await client.From<AcceptanceScore>().Eq("run_id", run + "missing").MaybeSingleAsync(token);
                Check(missing, "Database maybe-single"); Assert.IsNull(missing.Data);
                var page = await client.From<AcceptanceScore>().Eq("run_id", run).Order("id").Range(0, 0).Count().GetAsync(token);
                Check(page, "Database count/range"); Assert.AreEqual(1, page.Data.Count); Assert.AreEqual(1, page.Metadata.Count);
                var search = await client.From<AcceptanceScore>().Eq("run_id", run).TextSearch("run_id", run, "english").GetAsync(token);
                Check(search, "Database configured full-text"); Assert.AreEqual(1, search.Data.Count);
                var contains = await client.From<AcceptanceScore>().Eq("run_id", run).Contains("tags", new[] { "unity" }).GetAsync(token);
                Check(contains, "Database array literal"); Assert.AreEqual(1, contains.Data.Count);
                var csv = await client.From<AcceptanceScore>().Eq("run_id", run).GetCsvAsync(token);
                Check(csv, "Database CSV"); StringAssert.Contains(run, csv.Data);
                var rpc = await client.Database.RpcAsync<string>("unity_acceptance_echo", new { value = run }, cancellationToken: token);
                Check(rpc, "Database RPC"); Assert.AreEqual(run, rpc.Data);
                Check(await client.From<AcceptanceScore>().Eq("run_id", run).UpdateAsync(new { score = 42 },
                    new PostgrestWriteOptions { Returning = PostgrestReturn.Minimal }, token), "Database minimal update");

                var bytes = Encoding.UTF8.GetBytes("unity-supabase-live-acceptance");
                Check(await bucket.UploadAsync(path, bytes, new StorageUploadOptions { ContentType = "text/plain" }, token), "Storage upload");
                var duplicate = await bucket.UploadAsync(path, bytes, new StorageUploadOptions { ContentType = "text/plain" }, token);
                Assert.IsFalse(duplicate.IsSuccess, "Duplicate upload without upsert must fail.");
                Check(await bucket.UpdateAsync(path, bytes, new StorageUploadOptions { ContentType = "text/plain" }, token), "Storage update");
                var download = await bucket.DownloadAsync(path, cancellationToken: token);
                Check(download, "Storage download"); CollectionAssert.AreEqual(bytes, download.Data);
                Check(await bucket.InfoAsync(path, token), "Storage info");
                var list = await bucket.ListAsync(client.Auth.CurrentUser.Id, new StorageListOptions { Search = run, Limit = 10 }, token);
                Check(list, "Storage list"); Assert.GreaterOrEqual(list.Data.Count, 1);
                var url = await bucket.CreateSignedUrlAsync(path, 60, cancellationToken: token);
                Check(url, "Storage signed URL"); Assert.IsTrue(Uri.IsWellFormedUriString(url.Data.SignedUrl, UriKind.Absolute));
                Check(await bucket.CreateSignedUrlsAsync(new[] { path }, 60, token), "Storage signed URL batch");
                var uploadUrl = await bucket.CreateSignedUploadUrlAsync(signed, cancellationToken: token);
                Check(uploadUrl, "Storage signed upload creation");
                Check(await bucket.UploadToSignedUrlAsync(signed, uploadUrl.Data.Token, bytes,
                    new StorageUploadOptions { ContentType = "text/plain" }, token), "Storage signed upload");
                Check(await bucket.CopyAsync(path, copied, cancellationToken: token), "Storage copy");
                Check(await bucket.MoveAsync(path, moved, cancellationToken: token), "Storage move");
                var function = await client.Functions.InvokeAsync<JObject>("unity-acceptance",
                    new FunctionInvokeOptions { Body = new { run_id = run } }, token);
                Check(function, "Functions JSON"); Assert.IsTrue((bool)function.Data["accepted"]);
                var raw = await client.Functions.InvokeAsync("unity-acceptance",
                    new FunctionInvokeOptions { RawBody = Encoding.UTF8.GetBytes("{\"run_id\":\"" + run + "\"}") }, token);
                Check(raw, "Functions raw body/response"); Assert.AreEqual(200, raw.Data.StatusCode);
                var rejected = await client.Functions.InvokeAsync("unity-acceptance", new FunctionInvokeOptions { Method = SupabaseHttpMethod.Get }, token);
                Assert.IsFalse(rejected.IsSuccess); Assert.AreEqual(405, rejected.Error.StatusCode);
            }
            finally
            {
                using (var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    await Cleanup(() => bucket.RemoveAsync(new[] { path, moved, copied, signed }, cleanup.Token), "Storage");
                    await Cleanup(() => client.From<AcceptanceScore>().Eq("run_id", run).DeleteAsync(cancellationToken: cleanup.Token), "Database");
                }
            }
        }

        private static async Task Isolation(SupabaseClient first, CancellationToken token)
        {
            await Login(first, token);
            using (var second = NewClient())
            {
                await Login(second, token, true);
                Assert.AreNotEqual(first.Auth.CurrentUser.Id, second.Auth.CurrentUser.Id, "Use two distinct fixture accounts.");
                var run = Guid.NewGuid().ToString("N");
                var path = first.Auth.CurrentUser.Id + "/" + run + ".txt";
                try
                {
                    Check(await first.From<AcceptanceScore>().InsertAsync(new AcceptanceScore { RunId = run, Score = 1 }, cancellationToken: token), "Owner insert");
                    Check(await first.Storage.From("unity-acceptance").UploadAsync(path, Encoding.UTF8.GetBytes("private"),
                        new StorageUploadOptions { ContentType = "text/plain" }, token), "Owner upload");
                    var hidden = await second.From<AcceptanceScore>().Eq("run_id", run).GetAsync(token);
                    Check(hidden, "Other-user select"); Assert.AreEqual(0, hidden.Data.Count);
                    var update = await second.From<AcceptanceScore>().Eq("run_id", run).UpdateAsync(new { score = 99 }, cancellationToken: token);
                    Check(update, "Other-user update"); Assert.AreEqual(0, update.Data.Count);
                    var denied = await second.Storage.From("unity-acceptance").DownloadAsync(path, cancellationToken: token);
                    Assert.IsFalse(denied.IsSuccess, "Another user's private object must not be readable.");
                    var overwrite = await second.Storage.From("unity-acceptance").UploadAsync(path, Encoding.UTF8.GetBytes("changed"),
                        new StorageUploadOptions { ContentType = "text/plain", Upsert = true }, token);
                    Assert.IsFalse(overwrite.IsSuccess, "Another user's private object must not be writable.");
                    var owner = await first.From<AcceptanceScore>().Eq("run_id", run).SingleAsync(token);
                    Check(owner, "Owner state"); Assert.AreEqual(1, owner.Data.Score);
                }
                finally
                {
                    using (var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                    {
                        await Cleanup(() => first.Storage.From("unity-acceptance").RemoveAsync(new[] { path }, cleanup.Token), "Isolation Storage");
                        await Cleanup(() => first.From<AcceptanceScore>().Eq("run_id", run).DeleteAsync(cancellationToken: cleanup.Token), "Isolation Database");
                    }
                }
            }
        }

        private static async Task BroadcastAndPresence(SupabaseClient first, CancellationToken token)
        {
            await Login(first, token);
            using (var second = NewClient())
            {
                await Login(second, token, true);
                var topic = "acceptance:" + Guid.NewGuid().ToString("N");
                var received = false;
                var a = first.Realtime.Channel(topic, new RealtimeChannelConfig { BroadcastAcknowledge = true, PresenceKey = "first" });
                var b = second.Realtime.Channel(topic, new RealtimeChannelConfig { PresenceKey = "second" })
                    .OnBroadcast("probe", payload => received = (string)payload["value"] == "hello");
                Check(await SubscribeWithWarmup(a, token), "First channel");
                Check(await SubscribeWithWarmup(b, token), "Second channel");
                Check(await a.TrackAsync(new { user = "first" }, token), "Presence track");
                await Until(() => b.PresenceState["first"] != null, token, "Remote presence");
                Check(await a.SendBroadcastAsync("probe", new { value = "hello" }, token), "Broadcast acknowledgement");
                await Until(() => received, token, "Remote broadcast");
                Check(await a.UntrackAsync(token), "Presence untrack");
                await Until(() => b.PresenceState["first"] == null, token, "Remote presence leave");
            }
        }

        private static async Task<SupabaseResult> SubscribeWithWarmup(RealtimeChannel channel, CancellationToken token)
        {
            SupabaseResult result = null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                result = await channel.SubscribeAsync(token);
                if (result.IsSuccess || result.Error == null || !result.Error.IsRetryable) return result;
                // A freshly started local Realtime tenant may still be opening its database connection.
                Debug.Log("Realtime acceptance readiness retry " + (attempt + 1));
                await Task.Delay(1000, token);
            }
            return result;
        }

        private static async Task Until(Func<bool> ready, CancellationToken token, string operation)
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while (!ready() && Time.realtimeSinceStartup < deadline)
            { token.ThrowIfCancellationRequested(); await Task.Yield(); }
            Assert.IsTrue(ready(), operation + " did not complete before its deadline.");
        }

        private static void Check(SupabaseResult result, string operation)
        { Assert.IsNotNull(result, operation); Assert.IsTrue(result.IsSuccess, operation + " failed: " + result.Error); }

        private static void Check<T>(SupabaseResult<T> result, string operation)
        { Assert.IsNotNull(result, operation); Assert.IsTrue(result.IsSuccess, operation + " failed: " + result.Error); }

        private static async Task Cleanup<T>(Func<Task<SupabaseResult<T>>> action, string operation)
        {
            try
            {
                var result = await action();
                if (!result.IsSuccess) Debug.LogWarning(operation + " fixture cleanup failed: " + result.Error);
            }
            catch (Exception exception)
            {
                // A cleanup error must not hide the failure that caused cleanup.
                Debug.LogWarning(operation + " fixture cleanup failed: " + exception.GetType().Name);
            }
        }
    }
}
