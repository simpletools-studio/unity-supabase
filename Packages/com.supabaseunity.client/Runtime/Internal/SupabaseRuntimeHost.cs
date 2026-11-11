using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Supabase.Unity
{
    internal sealed class SupabaseRuntimeHost : MonoBehaviour
    {
        private static readonly object Gate = new object();
        private static readonly Queue<Action> Pending = new Queue<Action>();
        private static readonly Dictionary<IEnumerator, Action> Scheduled = new Dictionary<IEnumerator, Action>();
        private static SupabaseRuntimeHost instance;
        private static int mainThreadId;

        internal static event Action<bool> FocusChanged;
        internal static event Action<int> WebSocketOpened;
        internal static event Action<int, string> WebSocketMessage;
        internal static event Action<int, int, string> WebSocketClosed;
        internal static event Action<int, string> WebSocketError;

        // SubsystemRegistration runs on every Play entry, including when Domain Reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetState()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            CancelScheduledWork();
            FocusChanged = null;
            WebSocketOpened = null;
            WebSocketMessage = null;
            WebSocketClosed = null;
            WebSocketError = null;
            // Unity destroys the hidden runtime host on Play exit, even without Scene Reload.
            // Discard its reference before the next session creates a host.
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (instance != null)
                return;
            if (mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId != mainThreadId)
                throw new InvalidOperationException("Supabase runtime host must first be created on Unity's main thread.");

            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var hostObject = new GameObject("[Supabase Runtime]");
            hostObject.hideFlags = HideFlags.HideAndDontSave;
            if (Application.isPlaying)
                DontDestroyOnLoad(hostObject);
            instance = hostObject.AddComponent<SupabaseRuntimeHost>();
        }

        internal static void Post(Action action)
        {
            if (action == null)
                return;
            if (mainThreadId == 0 || Thread.CurrentThread.ManagedThreadId == mainThreadId)
            {
                EnsureInstance();
                action();
                return;
            }

            lock (Gate)
                Pending.Enqueue(action);
        }

        internal static void Run(IEnumerator routine, Action onStopped)
        {
            lock (Gate) Scheduled.Add(routine, onStopped);
            try
            {
                Post(delegate
                {
                    lock (Gate)
                        if (!Scheduled.ContainsKey(routine)) return;
                    instance.StartCoroutine(TrackRoutine(routine));
                });
            }
            catch
            {
                FinishRoutine(routine);
                throw;
            }
        }

        private static IEnumerator TrackRoutine(IEnumerator routine)
        {
            try
            {
                while (routine.MoveNext())
                    yield return routine.Current;
            }
            finally { FinishRoutine(routine); }
        }

        private static void FinishRoutine(IEnumerator routine)
        {
            Action stopped;
            lock (Gate)
            {
                if (!Scheduled.TryGetValue(routine, out stopped)) return;
                Scheduled.Remove(routine);
            }
            DisposeRoutine(routine, stopped);
        }

        private static void CancelScheduledWork()
        {
            KeyValuePair<IEnumerator, Action>[] snapshot;
            lock (Gate)
            {
                snapshot = new KeyValuePair<IEnumerator, Action>[Scheduled.Count];
                ((ICollection<KeyValuePair<IEnumerator, Action>>)Scheduled).CopyTo(snapshot, 0);
                Scheduled.Clear();
                Pending.Clear();
            }
            foreach (var item in snapshot)
                DisposeRoutine(item.Key, item.Value);
        }

        private static void DisposeRoutine(IEnumerator routine, Action stopped)
        {
            try { (routine as IDisposable)?.Dispose(); }
            finally { stopped(); }
        }

        internal static Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Run(DelayRoutine(delay, cancellationToken, completion), () => completion.TrySetCanceled());
            return completion.Task;
        }

        private static IEnumerator DelayRoutine(
            TimeSpan delay,
            CancellationToken cancellationToken,
            TaskCompletionSource<bool> completion)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + Math.Max(0d, delay.TotalSeconds);
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled();
                    yield break;
                }
                yield return null;
            }

            if (cancellationToken.IsCancellationRequested)
                completion.TrySetCanceled();
            else
                completion.TrySetResult(true);
        }

        private void Update()
        {
            while (true)
            {
                Action action;
                lock (Gate)
                {
                    if (Pending.Count == 0)
                        break;
                    action = Pending.Dequeue();
                }

                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            var handler = FocusChanged;
            if (handler != null)
                handler(hasFocus);
        }

        // Called by the WebGL .jslib bridge through Unity's SendMessage.
        public void SupabaseWebSocketOpened(string payload)
        {
            int id;
            if (!int.TryParse(payload, out id)) return;
            var handler = WebSocketOpened;
            if (handler != null) handler(id);
        }

        public void SupabaseWebSocketMessage(string payload)
        {
            DispatchWebSocketPayload(payload, delegate(int id, JObject json)
            {
                var handler = WebSocketMessage;
                if (handler != null) handler(id, (string)json["message"] ?? string.Empty);
            });
        }

        public void SupabaseWebSocketClosed(string payload)
        {
            DispatchWebSocketPayload(payload, delegate(int id, JObject json)
            {
                var handler = WebSocketClosed;
                if (handler != null) handler(id, (int?)json["code"] ?? 1000,
                    (string)json["reason"] ?? string.Empty);
            });
        }

        public void SupabaseWebSocketError(string payload)
        {
            DispatchWebSocketPayload(payload, delegate(int id, JObject json)
            {
                var handler = WebSocketError;
                if (handler != null) handler(id, (string)json["message"] ?? "Browser WebSocket error.");
            });
        }

        private static void DispatchWebSocketPayload(string payload, Action<int, JObject> callback)
        {
            try
            {
                var json = JObject.Parse(payload);
                callback((int)json["id"], json);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnApplicationQuit()
        {
            if (instance != this) return;
            StopAllCoroutines();
            CancelScheduledWork();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            StopAllCoroutines();
            CancelScheduledWork();
        }
    }
}
