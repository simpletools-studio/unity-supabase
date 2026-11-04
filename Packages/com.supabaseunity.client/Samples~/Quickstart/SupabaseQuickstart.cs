using System;
using System.Threading;
using Supabase.Unity;
using UnityEngine;

namespace Supabase.Unity.Samples
{
    public sealed class SupabaseQuickstart : MonoBehaviour
    {
        [SerializeField] private SupabaseSettings settings;
        private SupabaseClient client;
        private CancellationTokenSource lifetime;
        private string status = "Assign settings, run setup.sql in Supabase, then press Play.";
        private Vector2 scroll;

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Max(240, Screen.width - 40),
                Mathf.Max(180, Screen.height - 40)), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Supabase Quickstart");
            GUILayout.Label("1. Open Tools > Supabase > Setup and create settings.");
            GUILayout.Label("2. Run the sample's setup.sql in the Supabase SQL Editor.");
            GUILayout.Label("3. Assign settings on the Quickstart object and enter Play mode.");
            GUILayout.Space(12);
            GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private async void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (settings == null)
            {
                status = "Setup required: assign a SupabaseSettings asset to the Quickstart object.";
                return;
            }

            lifetime = new CancellationTokenSource();
            var token = lifetime.Token;
            status = "Connecting...";
            try
            {
                var current = new SupabaseClient(settings);
                client = current;
                var initialized = await current.InitializeAsync(token);
                token.ThrowIfCancellationRequested();
                if (!initialized.IsSuccess)
                {
                    LogFailure("initialize", initialized.Error);
                    return;
                }

                var result = await current.From<ScoreRow>()
                    .Select("id,player_name,score")
                    .Order("score", false)
                    .Limit(20)
                    .GetAsync(token);
                token.ThrowIfCancellationRequested();
                if (result.IsSuccess)
                {
                    status = "Loaded " + result.Data.Count + " scores.";
                    foreach (var score in result.Data)
                        status += "\n" + score.PlayerName + ": " + score.Score;
                    Debug.Log("Supabase Quickstart: loaded " + result.Data.Count + " score rows.", this);
                    foreach (var score in result.Data)
                        Debug.Log(score.PlayerName + ": " + score.Score, this);
                }
                else
                {
                    LogFailure("load scores", result.Error);
                }

                token.ThrowIfCancellationRequested();
                var channel = current.Realtime.Channel("quickstart-scores");
                channel.OnPostgresChanges(new RealtimePostgresChangeFilter
                {
                    Schema = "public",
                    Table = "scores",
                    Event = RealtimePostgresEvent.All
                }, change => Debug.Log(
                    "Supabase Quickstart: " + change.Event + " on " + change.Schema + "." + change.Table,
                    this));
                var subscribed = await channel.SubscribeAsync(token);
                token.ThrowIfCancellationRequested();
                if (!subscribed.IsSuccess)
                    LogFailure("subscribe to score changes", subscribed.Error);
                else
                    Debug.Log("Supabase Quickstart: listening for changes to public.scores.", this);
            }
            catch (OperationCanceledException) { }
            catch (SupabaseConfigurationException exception)
            {
                status = "Configuration: " + exception.Message;
                Debug.LogError("Supabase Quickstart configuration: " + exception.Message, this);
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void OnDisable()
        {
            if (lifetime != null) lifetime.Cancel();
            if (client != null) client.Dispose();
            if (lifetime != null) lifetime.Dispose();
            lifetime = null;
            client = null;
        }

        private void LogFailure(string action, SupabaseError error)
        {
            status = "Could not " + action + ": " + (error == null ? "unknown error" : error.ToString());
            Debug.LogError(
                "Supabase Quickstart could not " + action + ": " +
                (error == null ? "unknown error" : error.ToString()), this);
        }
    }

    [Serializable, SupabaseTable("scores")]
    public sealed class ScoreRow
    {
        [SupabaseColumn("id")] public long Id { get; set; }
        [SupabaseColumn("player_name")] public string PlayerName { get; set; }
        [SupabaseColumn("score")] public int Score { get; set; }
    }
}
