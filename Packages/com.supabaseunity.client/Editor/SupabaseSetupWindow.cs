using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Supabase.Unity.Editor
{
    internal sealed class SupabaseSetupWindow : EditorWindow
    {
        private const string DefaultSettingsFolder = "Assets/Supabase";
        private const string DefaultSettingsPath = DefaultSettingsFolder + "/SupabaseSettings.asset";
        private const string DocumentationUrl =
            "https://github.com/DorukTan/unity-supabase/blob/v0.7.0/Packages/com.supabaseunity.client/Documentation~/getting-started.md";
        private const string QuickstartUrl =
            "https://github.com/DorukTan/unity-supabase/tree/v0.7.0/Packages/com.supabaseunity.client/Samples~/Quickstart";
        private const string DashboardUrl = "https://supabase.com/dashboard/projects";
        private const string PackageName = "com.supabaseunity.client";

        private SupabaseSettings settings;
        private Vector2 scrollPosition;
        private bool testingConnection;
        private bool showRuntimeOptions;
        private bool quickstartSearched;
        private SupabaseQuickstartAssets quickstartAssets;
        private string quickstartNotice;
        private string configurationNotice;
        private string connectionStatus;
        private MessageType connectionStatusType = MessageType.None;
        private UnityWebRequest activeRequest;
        private SupabaseSettings connectionSettings;
        private string connectionConfiguration;
        private int connectionVersion;

        private static float SectionSpacing
        {
            get { return EditorGUIUtility.standardVerticalSpacing * 4f; }
        }

        private static float PrimaryButtonHeight
        {
            get { return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 3f; }
        }

        [MenuItem("Tools/Supabase/Setup")]
        private static void OpenMenu()
        {
            Open(null);
        }

        internal static void Open(SupabaseSettings preferredSettings)
        {
            var window = GetWindow<SupabaseSetupWindow>("Supabase Setup");
            window.minSize = new Vector2(460f, 560f);
            if (preferredSettings != null)
                window.settings = preferredSettings;
            else
                window.FindSettings();
            window.Show();
        }

        private void OnEnable()
        {
            if (settings == null)
                FindSettings();
        }

        private void OnDisable()
        {
            InvalidateConnection();
        }

        private void OnProjectChange()
        {
            quickstartSearched = false;
            quickstartNotice = null;
            Repaint();
        }

        private void OnFocus()
        {
            quickstartSearched = false;
        }

        private void OnGUI()
        {
            SynchronizeConnectionStatus();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            var contentWidth = Mathf.Max(
                320f,
                position.width - EditorGUIUtility.singleLineHeight - SectionSpacing);
            EditorGUILayout.BeginVertical(GUILayout.Width(contentWidth));
            DrawHeader();

            EditorGUILayout.Space(SectionSpacing);
            DrawConfigurationStep();
            EditorGUILayout.Space(SectionSpacing);
            DrawConnectionStep();
            EditorGUILayout.Space(SectionSpacing);
            DrawQuickstartStep();
            EditorGUILayout.Space(SectionSpacing);
            DrawResources();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader()
        {
            GUILayout.Label(
                "Configure the client, check the connection, and run the Quickstart.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            GUILayout.Label(
                "Publishable keys only. Never place secret or service-role keys in a Unity project.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawConfigurationStep()
        {
            SupabaseClientOptions options;
            string validationError;
            var configurationValid = SupabaseSettingsInspectorGui.TryValidate(
                settings, out options, out validationError);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSectionHeader(
                "1", "Client settings",
                settings == null ? "Not configured" : configurationValid ? "Ready" : "Needs attention");
            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

            EditorGUI.BeginDisabledGroup(testingConnection);
            var selected = (SupabaseSettings)EditorGUILayout.ObjectField(
                "Settings asset", settings, typeof(SupabaseSettings), false);
            EditorGUI.EndDisabledGroup();
            if (selected != settings)
            {
                settings = selected;
                configurationNotice = null;
                InvalidateConnection();
                configurationValid = SupabaseSettingsInspectorGui.TryValidate(
                    settings, out options, out validationError);
            }

            if (settings == null)
            {
                GUILayout.Label(
                    "Create a settings asset, or select one that is already in the project.",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
                if (ActionButton("Create Settings Asset", GUILayout.Height(PrimaryButtonHeight)))
                    CreateSettingsAsset();
                if (ActionButton("Find Existing Settings"))
                {
                    FindSettings();
                    configurationNotice = settings == null
                        ? "No SupabaseSettings asset was found in this project."
                        : "Found " + AssetDatabase.GetAssetPath(settings) + ".";
                }
                if (!string.IsNullOrEmpty(configurationNotice))
                    EditorGUILayout.HelpBox(configurationNotice, MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            GUILayout.Label(AssetDatabase.GetAssetPath(settings), EditorStyles.wordWrappedMiniLabel);
            EditorGUI.BeginDisabledGroup(testingConnection);
            if (SupabaseSettingsInspectorGui.DrawFields(new SerializedObject(settings), ref showRuntimeOptions))
                InvalidateConnection();
            EditorGUI.EndDisabledGroup();
            configurationValid = SupabaseSettingsInspectorGui.TryValidate(settings, out options, out validationError);

            if (configurationValid)
            {
                configurationNotice = null;
                EditorGUILayout.HelpBox(
                    "Configuration looks valid. Test the connection when you are ready.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Error);
            }

            if (!string.IsNullOrEmpty(configurationNotice))
                EditorGUILayout.HelpBox(configurationNotice, MessageType.Info);
            if (ActionButton("Show Settings Asset"))
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawConnectionStep()
        {
            SupabaseClientOptions options;
            string validationError;
            var configurationValid = SupabaseSettingsInspectorGui.TryValidate(
                settings, out options, out validationError);
            var connectionState = testingConnection
                ? "Testing"
                : connectionStatusType == MessageType.Error
                    ? "Check failed"
                    : !string.IsNullOrEmpty(connectionStatus)
                        ? "Connection verified"
                        : configurationValid ? "Ready" : "Waiting";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSectionHeader("2", "Check the connection", connectionState);
            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            GUILayout.Label(
                "Checks the URL and publishable key against Supabase Auth. It does not test tables or RLS.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

            var canTest = configurationValid && !testingConnection;
            EditorGUI.BeginDisabledGroup(!canTest);
            if (ActionButton(
                    testingConnection ? "Testing..." : "Test Project Connection",
                    GUILayout.Height(PrimaryButtonHeight)))
                TestConnection();
            EditorGUI.EndDisabledGroup();

            if (!configurationValid)
                GUILayout.Label(
                    "Finish step 1 before testing the connection.",
                    EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(connectionStatus))
                EditorGUILayout.HelpBox(connectionStatus, connectionStatusType);
            EditorGUILayout.EndVertical();
        }

        private void DrawQuickstartStep()
        {
            if (!quickstartSearched)
            {
                quickstartAssets = SupabaseQuickstartAssets.FindImported();
                quickstartSearched = true;
            }
            var sample = quickstartAssets;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSectionHeader("3", "Run the Quickstart", sample == null ? "Not imported" : "Imported");
            GUILayout.Label(sample == null
                    ? "Import Quickstart from the package's Samples tab, then follow its SQL and scene setup instructions."
                    : sample.ScenePath != null
                        ? "Run setup.sql in the Supabase SQL Editor. Open the sample scene, assign settings to the Quickstart object, then press Play."
                        : "This imported sample has no demo scene. Run setup.sql, add SupabaseQuickstart to a GameObject, and assign settings. See the guide for details.",
                EditorStyles.wordWrappedMiniLabel);
            if (sample != null)
            {
                GUILayout.Label(sample.ScenePath ?? sample.ScriptPath, EditorStyles.wordWrappedMiniLabel);
                if (sample.CopyCount > 1)
                    GUILayout.Label("Multiple sample copies found. Using the path shown above.", EditorStyles.wordWrappedMiniLabel);
                if (sample.ScenePath != null)
                {
                    EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);
                    if (ActionButton("Open Quickstart Scene"))
                    {
                        try
                        {
                            if (!sample.OpenScene())
                                quickstartNotice = "Scene opening was cancelled or is unavailable. The current scene was not replaced.";
                            else quickstartNotice = null;
                        }
                        catch (Exception exception)
                        {
                            quickstartNotice = "Could not open the Quickstart scene: " + SupabaseHttp.Redact(exception.Message);
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                        GUILayout.Label("Exit Play mode before opening the sample scene.", EditorStyles.wordWrappedMiniLabel);
                }
                else if (ActionButton("Show Imported Quickstart")) ShowAsset(sample.ScriptPath);
                if (sample.SqlPath != null && ActionButton("Show setup.sql")) ShowAsset(sample.SqlPath);
            }
            else if (ActionButton("Open Package Manager"))
                UnityEditor.PackageManager.UI.Window.Open(PackageName);
            if (ActionButton("Read the Quickstart Guide")) Application.OpenURL(QuickstartUrl);
            if (!string.IsNullOrEmpty(quickstartNotice))
                EditorGUILayout.HelpBox(quickstartNotice, MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private static void ShowAsset(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void DrawResources()
        {
            EditorGUILayout.LabelField("Resources", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (ActionButton("Dashboard", EditorStyles.miniButtonLeft))
                Application.OpenURL(DashboardUrl);
            if (ActionButton("Documentation", EditorStyles.miniButtonMid))
                Application.OpenURL(DocumentationUrl);
            if (ActionButton("Model Generator", EditorStyles.miniButtonRight))
                SupabaseModelGeneratorWindow.Open(settings);
            EditorGUILayout.EndHorizontal();
        }

        internal static bool ActionButton(string label, params GUILayoutOption[] options)
        {
            return ActionButton(label, GUI.skin.button, options);
        }

        private static bool ActionButton(string label, GUIStyle style, params GUILayoutOption[] options)
        {
            var content = new GUIContent(label);
            var rect = GUILayoutUtility.GetRect(content, style, options);
            var id = GUIUtility.GetControlID(GUI.enabled ? FocusType.Keyboard : FocusType.Passive, rect);
            var previousChanged = GUI.changed;
            // A momentary toggle uses Unity's native mouse and keyboard handling with button styling.
            var activated = GUI.Toggle(rect, id, false, content, style);
            GUI.changed = previousChanged;
            if (Event.current.type == EventType.Repaint)
            {
                var focus = (ActionFocus)GUIUtility.GetStateObject(typeof(ActionFocus), id);
                var focused = GUI.enabled && GUIUtility.keyboardControl == id;
                if (focused && !focus.WasFocused)
                {
                    GUI.ScrollTo(rect);
                    EditorWindow.focusedWindow?.Repaint();
                }
                focus.WasFocused = focused;
            }
            return activated;
        }

        private sealed class ActionFocus
        {
            public bool WasFocused;
        }

        private static void DrawSectionHeader(string number, string title, string status)
        {
            GUILayout.Label(number + ". " + title, EditorStyles.boldLabel);
            GUILayout.Label(status, EditorStyles.wordWrappedMiniLabel);
        }

        private void CreateSettingsAsset()
        {
            EnsureAssetFolder(DefaultSettingsFolder);
            var path = AssetDatabase.GenerateUniqueAssetPath(DefaultSettingsPath);
            settings = CreateInstance<SupabaseSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
            configurationNotice = "Created " + path + ". Add the Project URL and publishable key from the Supabase Dashboard Connect dialog.";
            connectionStatus = null;
            connectionStatusType = MessageType.None;
        }

        private void FindSettings()
        {
            var selectedSettings = Selection.activeObject as SupabaseSettings;
            if (selectedSettings != null)
            {
                settings = selectedSettings;
                return;
            }

            var guids = AssetDatabase.FindAssets("t:SupabaseSettings");
            var paths = new string[guids.Length];
            for (var index = 0; index < guids.Length; index++)
                paths[index] = AssetDatabase.GUIDToAssetPath(guids[index]);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            settings = paths.Length == 0
                ? null
                : AssetDatabase.LoadAssetAtPath<SupabaseSettings>(paths[0]);
        }

        private async void TestConnection()
        {
            await TestConnectionAsync();
        }

        internal void SynchronizeConnectionStatus()
        {
            if (connectionConfiguration != null &&
                (settings != connectionSettings || settings == null ||
                 !string.Equals(connectionConfiguration, EditorJsonUtility.ToJson(settings), StringComparison.Ordinal)))
                InvalidateConnection();
        }

        private void InvalidateConnection()
        {
            connectionVersion++;
            connectionSettings = null;
            connectionConfiguration = null;
            connectionStatus = null;
            connectionStatusType = MessageType.None;
            testingConnection = false;
            var request = activeRequest;
            activeRequest = null;
            if (request != null) request.Abort();
        }

        private bool IsCurrentConnection(int version)
        {
            if (this == null || version != connectionVersion) return false;
            SynchronizeConnectionStatus();
            return version == connectionVersion;
        }

        internal async Task TestConnectionAsync()
        {
            if (testingConnection) return;
            connectionSettings = settings;
            connectionConfiguration = settings == null ? null : EditorJsonUtility.ToJson(settings);
            var version = ++connectionVersion;
            testingConnection = true;
            connectionStatus = "Connecting...";
            connectionStatusType = MessageType.None;
            Repaint();

            try
            {
                using (var request = CreateConnectionRequest(settings.ToOptions()))
                {
                    activeRequest = request;
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Delay(25);

                    if (!IsCurrentConnection(version)) return;

#if UNITY_2020_2_OR_NEWER
                    var succeeded = request.result == UnityWebRequest.Result.Success;
#else
                    var succeeded = !request.isNetworkError && !request.isHttpError;
#endif
                    if (succeeded)
                    {
                        connectionStatus = "Connected successfully. The project URL and client key were accepted.";
                        connectionStatusType = MessageType.Info;
                    }
                    else
                    {
                        connectionStatus = FormatConnectionFailure(request.responseCode, request.error);
                        connectionStatusType = MessageType.Error;
                    }
                }
            }
            catch (Exception exception)
            {
                if (!IsCurrentConnection(version)) return;
                connectionStatus = "Connection test failed: " + SupabaseHttp.Redact(exception.Message);
                connectionStatusType = MessageType.Error;
            }
            finally
            {
                if (version == connectionVersion)
                {
                    activeRequest = null;
                    testingConnection = false;
                }
                if (this != null)
                    Repaint();
            }
        }

        internal static UnityWebRequest CreateConnectionRequest(SupabaseClientOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            var endpoints = options.ValidateAndResolve();
            var request = UnityWebRequest.Get(endpoints.Auth.AbsoluteUri.TrimEnd('/') + "/settings");
            request.timeout = 15;
            request.redirectLimit = 0;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("apikey", options.PublishableKey);
            request.SetRequestHeader("X-Client-Info", SupabaseHttp.ClientInfo);
            return request;
        }

        internal static string FormatConnectionFailure(long statusCode, string requestError)
        {
            if (statusCode == 401 || statusCode == 403)
                return "Supabase rejected the connection (HTTP " + statusCode +
                       "). Copy the Project URL and publishable key again from the Dashboard Connect dialog.";
            if (statusCode > 0)
                return "Supabase returned HTTP " + statusCode +
                       ". The project is reachable; check its status and Auth configuration.";
            return "Could not reach the project. Check the URL, internet connection, proxy, and firewall. " +
                   SupabaseHttp.Redact(requestError ?? string.Empty);
        }

        private static void EnsureAssetFolder(string folder)
        {
            var segments = folder.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
