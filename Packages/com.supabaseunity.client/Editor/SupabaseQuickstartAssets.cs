using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Supabase.Unity.Editor
{
    internal sealed class SupabaseQuickstartAssets
    {
        internal string ScriptPath { get; private set; }
        internal string ScenePath { get; private set; }
        internal string SqlPath { get; private set; }
        internal int CopyCount { get; private set; }

        internal static SupabaseQuickstartAssets FindImported()
        {
            var candidates = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("SupabaseQuickstart t:MonoScript", new[] { "Assets" }))
                candidates.Add(AssetDatabase.GUIDToAssetPath(guid));
            return Resolve(candidates, path => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null);
        }

        internal static SupabaseQuickstartAssets Resolve(IEnumerable<string> candidates, Func<string, bool> exists)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in candidates)
                if (IsImportedPath(path) && Path.GetFileName(path) == "SupabaseQuickstart.cs" && exists(path))
                    paths.Add(path);
            if (paths.Count == 0) return null;
            var script = paths.Min;
            var folder = script.Substring(0, script.LastIndexOf('/') + 1);
            var scene = folder + "Quickstart.unity";
            var sql = folder + "setup.sql";
            return new SupabaseQuickstartAssets
            {
                ScriptPath = script,
                ScenePath = exists(scene) ? scene : null,
                SqlPath = exists(sql) ? sql : null,
                CopyCount = paths.Count
            };
        }

        private static bool IsImportedPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("\\")) return false;
            foreach (var segment in path.Split('/'))
                if (segment == ".." || segment == "." || segment.Length == 0) return false;
            return true;
        }

        internal static bool TryOpenScene(string path, Func<bool> isPlayingOrChanging,
            Func<string, bool> sceneExists, Func<bool> saveModifiedScenes, Action<string> openScene)
        {
            if (!IsImportedPath(path) || Path.GetFileName(path) != "Quickstart.unity" ||
                isPlayingOrChanging() || !sceneExists(path)) return false;
            if (!saveModifiedScenes()) return false;
            // A save dialog can yield to Editor events; recheck before replacing the active scene.
            if (isPlayingOrChanging() || !sceneExists(path)) return false;
            openScene(path);
            return true;
        }

        internal bool OpenScene()
        {
            return TryOpenScene(ScenePath,
                () => EditorApplication.isPlayingOrWillChangePlaymode,
                path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null,
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo,
                path => EditorSceneManager.OpenScene(path, OpenSceneMode.Single));
        }
    }
}
