using System;
using System.Text;
using NUnit.Framework;

namespace Supabase.Unity.Tests
{
    public sealed class DatabaseTests
    {
        [SupabaseTable("score_rows")]
        private sealed class Score
        {
            [SupabaseColumn("player_name")] public string PlayerName { get; set; }
            [SupabaseColumn("score")] public int Value { get; set; }
        }

        [Test]
        public void Query_EncodesFiltersAndSendsClientHeaders()
        {
            var transport = new RecordingHttpTransport();
            var options = ConfigurationTests.ValidOptions();
            options.HttpTransport = transport;
            using (var client = new SupabaseClient(options))
            {
                var result = client.From<Score>()
                    .Select("player_name,score").Gte("score", 100).Order("score", false).Range(10, 19)
                    .Count(PostgrestCount.Exact).GetAsync().GetAwaiter().GetResult();

                Assert.IsTrue(result.IsSuccess);
                var uri = Uri.UnescapeDataString(transport.LastRequest.Uri.AbsoluteUri);
                StringAssert.Contains("/rest/v1/score_rows", uri);
                StringAssert.Contains("select=player_name,score", uri);
                StringAssert.Contains("score=gte.100", uri);
                StringAssert.Contains("order=score.desc.nullslast", uri);
                StringAssert.Contains("limit=10", uri);
                StringAssert.Contains("offset=10", uri);
                Assert.AreEqual(options.PublishableKey, transport.LastRequest.Headers["apikey"]);
                Assert.IsFalse(transport.LastRequest.Headers.ContainsKey("Authorization"));
                Assert.AreEqual("count=exact", transport.LastRequest.Headers["Prefer"]);
            }
        }

        [Test]
        public void Insert_UsesColumnNamesAndRepresentationPreference()
        {
            var transport = new RecordingHttpTransport
            {
                Response = delegate { return new SupabaseHttpResponse
                {
                    StatusCode = 201,
                    Body = Encoding.UTF8.GetBytes("[{\"player_name\":\"Ada\",\"score\":42}]")
                }; }
            };
            var options = ConfigurationTests.ValidOptions();
            options.HttpTransport = transport;
            using (var client = new SupabaseClient(options))
            {
                var result = client.From<Score>().InsertAsync(new Score { PlayerName = "Ada", Value = 42 })
                    .GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                var json = Encoding.UTF8.GetString(transport.LastRequest.Body);
                StringAssert.Contains("\"player_name\":\"Ada\"", json);
                StringAssert.Contains("\"score\":42", json);
                StringAssert.Contains("return=representation", transport.LastRequest.Headers["Prefer"]);
            }
        }

        [TestCase(0, 0, 1)]
        [TestCase(10, 19, 10)]
        public void Range_UsesInclusiveBounds(int from, int to, int expectedLimit)
        {
            var transport = new RecordingHttpTransport();
            var options = ConfigurationTests.ValidOptions();
            options.HttpTransport = transport;
            using (var client = new SupabaseClient(options))
            {
                var result = client.From<Score>().Range(from, to).GetAsync().GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                var query = Uri.UnescapeDataString(transport.LastRequest.Uri.Query);
                StringAssert.Contains("limit=" + expectedLimit, query);
                StringAssert.Contains("offset=" + from, query);
            }
        }

        [TestCase("[]", true, null)]
        [TestCase("[{\"score\":42}]", true, null)]
        [TestCase("[{\"score\":42},{\"score\":43}]", false, "multiple_rows")]
        public void MaybeSingle_EnforcesCardinality(string body, bool success, string errorCode)
        {
            var transport = new RecordingHttpTransport().Enqueue(200, body);
            var options = ConfigurationTests.ValidOptions();
            options.HttpTransport = transport;
            using (var client = new SupabaseClient(options))
            {
                var result = client.From<Score>().MaybeSingleAsync().GetAwaiter().GetResult();
                Assert.AreEqual(success, result.IsSuccess);
                if (!success) Assert.AreEqual(errorCode, result.Error.Code);
                else if (body == "[]") Assert.IsNull(result.Data);
                else Assert.AreEqual(42, result.Data.Value);
            }
        }

        [Test]
        public void Rpc_SendsParametersAndSchemaAndDecodesScalar()
        {
            var transport = new RecordingHttpTransport().Enqueue(200, "42");
            var options = ConfigurationTests.ValidOptions();
            options.DefaultSchema = "custom";
            options.HttpTransport = transport;
            using (var client = new SupabaseClient(options))
            {
                var result = client.Database.RpcAsync<int>("add_score", new { amount = 2 },
                    PostgrestCount.Exact).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(42, result.Data);
                Assert.AreEqual(SupabaseHttpMethod.Post, transport.LastRequest.Method);
                StringAssert.EndsWith("/rpc/add_score", transport.LastRequest.Uri.AbsolutePath);
                Assert.AreEqual("custom", transport.LastRequest.Headers["Content-Profile"]);
                Assert.AreEqual("custom", transport.LastRequest.Headers["Accept-Profile"]);
                Assert.AreEqual("count=exact", transport.LastRequest.Headers["Prefer"]);
                Assert.AreEqual(2, (int)Newtonsoft.Json.Linq.JObject.Parse(
                    Encoding.UTF8.GetString(transport.LastRequest.Body))["amount"]);
            }
        }

        [TestCase(null, null, "fts.hello")]
        [TestCase("english", null, "fts(english).hello")]
        [TestCase("french", "phfts", "phfts(french).hello")]
        public void TextSearch_PlacesLanguageInsideOperator(string config, string type, string expected)
        {
            var transport = new RecordingHttpTransport();
            var options = ConfigurationTests.ValidOptions();
            options.HttpTransport = transport;
            using (var client = new SupabaseClient(options))
            {
                var result = client.From<Score>().TextSearch("search", "hello", config, type)
                    .GetAsync().GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                var query = Uri.UnescapeDataString(transport.LastRequest.Uri.Query);
                StringAssert.Contains("search=" + expected, query);
            }
        }
        [TestCase(PostgrestFilterOperator.Contains, "cs")]
        [TestCase(PostgrestFilterOperator.ContainedBy, "cd")]
        [TestCase(PostgrestFilterOperator.Overlaps, "ov")]
        public void ArrayFilters_UsePostgresArrayLiterals(PostgrestFilterOperator op, string wireOperator)
        {
            var http = new RecordingHttpTransport();
            var options = ConfigurationTests.ValidOptions(); options.HttpTransport = http;
            using (var client = new SupabaseClient(options))
            {
                var result = client.From<Score>().Filter("tags", op, new[] { "unity", "a,b", "NULL", null })
                    .GetAsync().GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                StringAssert.Contains("tags=" + wireOperator + ".{\"unity\",\"a,b\",\"NULL\",NULL}",
                    Uri.UnescapeDataString(http.LastRequest.Uri.Query));
            }
        }

        [Test]
        public void JsonArrayFilter_RemainsJson()
        {
            var http = new RecordingHttpTransport();
            var options = ConfigurationTests.ValidOptions(); options.HttpTransport = http;
            using (var client = new SupabaseClient(options))
            {
                var result = client.From<Score>().Contains("document", new Newtonsoft.Json.Linq.JArray("unity"))
                    .GetAsync().GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                StringAssert.Contains("document=cs.[\"unity\"]", Uri.UnescapeDataString(http.LastRequest.Uri.Query));
            }
        }

    }
}
