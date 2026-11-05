using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Supabase.Unity.Tests
{
    public sealed class ServiceContractTests
    {
        private sealed class Payload { public int value { get; set; } }

        [TestCase("{\"signedURL\":\"/object/sign/private/file.txt?token=fixture\"}")]
        [TestCase("{\"signedUrl\":\"/object/sign/private/file.txt?token=fixture\"}")]
        public void SignedDownload_NormalizesBothResponseSpellings(string body)
        {
            var http = new RecordingHttpTransport().Enqueue(200, body);
            using (var client = Client(http))
            {
                var result = client.Storage.From("private").CreateSignedUrlAsync("file.txt", 60).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                StringAssert.Contains("/storage/v1/object/sign/private/file.txt?token=fixture", result.Data.SignedUrl);
                var request = http.LastRequest;
                Assert.AreEqual(SupabaseHttpMethod.Post, request.Method);
                Assert.AreEqual(60, (int)JObject.Parse(Encoding.UTF8.GetString(request.Body))["expiresIn"]);
            }
        }

        [Test]
        public void StorageList_SendsPaginationSearchAndSort()
        {
            var http = new RecordingHttpTransport().Enqueue(200, "[]");
            using (var client = Client(http))
            {
                var result = client.Storage.From("private").ListAsync("folder", new StorageListOptions
                { Limit = 5, Offset = 10, Search = "report", SortBy = new StorageSortOptions { Column = "created_at", Order = "desc" } }).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(SupabaseHttpMethod.Post, http.LastRequest.Method);
                StringAssert.EndsWith("/object/list/private", http.LastRequest.Uri.AbsolutePath);
                var body = JObject.Parse(Encoding.UTF8.GetString(http.LastRequest.Body));
                Assert.AreEqual("folder", (string)body["prefix"]);
                Assert.AreEqual(5, (int)body["limit"]); Assert.AreEqual(10, (int)body["offset"]);
                Assert.AreEqual("report", (string)body["search"]);
                Assert.AreEqual("created_at", (string)body["sortBy"]["column"]);
                Assert.AreEqual("desc", (string)body["sortBy"]["order"]);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StorageList_OmitsUnsetOptions(bool partial)
        {
            var http = new RecordingHttpTransport().Enqueue(200, "[]");
            using (var client = Client(http))
            {
                var result = client.Storage.From("private").ListAsync("folder",
                    partial ? new StorageListOptions { Limit = 5 } : null).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                var body = JObject.Parse(Encoding.UTF8.GetString(http.LastRequest.Body));
                Assert.IsNull(body["sortBy"], "Unset sortBy must be omitted, not sent as JSON null.");
                Assert.IsNull(body["offset"]);
                Assert.IsNull(body["search"]);
                if (partial) Assert.AreEqual(5, (int)body["limit"]);
                else Assert.IsNull(body["limit"]);
            }
        }

        [Test]
        public void StorageCopy_SendsDestinationBucketAndUpsert()
        {
            var http = new RecordingHttpTransport().Enqueue(200, "{}");
            using (var client = Client(http))
            {
                var result = client.Storage.From("source").CopyAsync("one.txt", "two.txt", "destination", true).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                var body = JObject.Parse(Encoding.UTF8.GetString(http.LastRequest.Body));
                Assert.AreEqual("source", (string)body["bucketId"]);
                Assert.AreEqual("destination", (string)body["destinationBucket"]);
                Assert.AreEqual("one.txt", (string)body["sourceKey"]);
                Assert.AreEqual("two.txt", (string)body["destinationKey"]);
                Assert.IsTrue((bool)body["upsert"]);
            }
        }

        [Test]
        public void StorageUpload_SendsBinaryAndEncodedMetadata()
        {
            var http = new RecordingHttpTransport().Enqueue(200, "{}");
            using (var client = Client(http))
            {
                var bytes = new byte[] { 0, 255, 1 };
                var result = client.Storage.From("private").UploadAsync("folder/file.bin", bytes,
                    new StorageUploadOptions { Metadata = new JObject { ["name"] = "ö" }, CacheControl = "60", Upsert = true }).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                CollectionAssert.AreEqual(bytes, http.LastRequest.Body);
                Assert.AreEqual("max-age=60", http.LastRequest.Headers["cache-control"]);
                Assert.AreEqual("true", http.LastRequest.Headers["x-upsert"]);
                var metadata = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(http.LastRequest.Headers["x-metadata"])));
                Assert.AreEqual("ö", (string)metadata["name"]);
            }
        }

        [TestCase("", true)]
        [TestCase("not json", false)]
        public void FunctionsTyped_HandlesEmptyAndMalformedJson(string body, bool success)
        {
            var http = new RecordingHttpTransport().Enqueue(200, body);
            using (var client = Client(http))
            {
                var result = client.Functions.InvokeAsync<Payload>("fixture").GetAwaiter().GetResult();
                Assert.AreEqual(success, result.IsSuccess);
                if (success) Assert.IsNull(result.Data);
                else Assert.AreEqual(SupabaseErrorKind.Serialization, result.Error.Kind);
            }
        }

        [Test]
        public void FunctionsRaw_PreservesBytesStatusAndHeaders()
        {
            var http = new RecordingHttpTransport().Enqueue(202, "plain response");
            using (var client = Client(http))
            {
                var result = client.Functions.InvokeAsync("fixture", new FunctionInvokeOptions
                { RawBody = new byte[] { 0, 255 }, ContentType = "application/octet-stream", Method = SupabaseHttpMethod.Put }).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess); Assert.AreEqual(202, result.Data.StatusCode);
                Assert.AreEqual("plain response", result.Data.Text);
                CollectionAssert.AreEqual(new byte[] { 0, 255 }, http.LastRequest.Body);
                Assert.AreEqual("application/octet-stream", http.LastRequest.ContentType);
                Assert.AreEqual(SupabaseHttpMethod.Put, http.LastRequest.Method);
            }
        }

        [Test]
        public void AuthOtp_SendsPhoneChannelWithoutEmailPkce()
        {
            var http = new RecordingHttpTransport().Enqueue(200, "{}");
            using (var client = Client(http))
            {
                var result = client.Auth.SignInWithOtpAsync("+15555550100", new AuthOtpOptions
                { Channel = "sms", ShouldCreateUser = false }).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                var body = JObject.Parse(Encoding.UTF8.GetString(http.LastRequest.Body));
                Assert.AreEqual("+15555550100", (string)body["phone"]);
                Assert.AreEqual("sms", (string)body["channel"]);
                Assert.IsFalse((bool)body["create_user"]);
                Assert.IsNull(body["email"]); Assert.IsNull(body["code_challenge"]);
            }
        }

        [Test]
        public void AuthSso_SendsPkceAndDecodesRedirect()
        {
            var http = new RecordingHttpTransport().Enqueue(200, "{\"url\":\"https://identity.example.test/login\"}");
            using (var client = Client(http))
            {
                var result = client.Auth.SignInWithSsoAsync(new AuthSsoOptions
                { Domain = "example.test" }).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                StringAssert.EndsWith("/auth/v1/sso", http.LastRequest.Uri.AbsolutePath);
                var body = JObject.Parse(Encoding.UTF8.GetString(http.LastRequest.Body));
                Assert.AreEqual("example.test", (string)body["domain"]);
                Assert.AreEqual("s256", (string)body["code_challenge_method"]);
                Assert.IsNotEmpty((string)body["code_challenge"]);
                Assert.IsTrue((bool)body["skip_http_redirect"]);
                Assert.IsNotNull(result.Data);
            }
        }

        private static SupabaseClient Client(RecordingHttpTransport http)
        { var options = ConfigurationTests.ValidOptions(); options.HttpTransport = http; return new SupabaseClient(options); }
    }
}
