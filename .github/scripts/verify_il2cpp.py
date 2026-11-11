#!/usr/bin/env python3
"""Build and run the Windows x64 IL2CPP model probe against a loopback fixture."""

import argparse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import os
from pathlib import Path
import shutil
import subprocess
from threading import Thread

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = Path("Packages/com.supabaseunity.client")
SUCCESS = "SUPABASE_IL2CPP_MODEL_PROBE_PASSED"

PROBE = r"""using System;
using Newtonsoft.Json;
using Supabase.Unity;
using UnityEngine;
using UnityEngine.Scripting;

public sealed class RuntimeModelProbe : MonoBehaviour
{
    [Preserve]
    private sealed class Handwritten
    {
        [Preserve, SupabaseColumn("label")] public string Label { get; set; }
        [Preserve, SupabaseColumn("count")] public int? Count { get; set; }
        [Preserve, SupabaseColumn("nested")] public Nested Value { get; set; }
        [Preserve, SupabaseColumn("items")] public int[] Items { get; set; }
    }

    [Preserve]
    private sealed class Nested
    {
        [Preserve, JsonProperty("name")] public string Name { get; set; }
    }

    private async void Start()
    {
        try
        {
            var url = Environment.GetEnvironmentVariable("SUPABASE_PROBE_URL");
            using (var client = new SupabaseClient(new SupabaseClientOptions
            {
                ProjectUrl = url,
                PublishableKey = "sb_publishable_probe",
                AutoRefreshToken = false
            }))
            {
                var generated = await client.From<Probe.Models.GeneratedRow>().GetAsync();
                if (!generated.IsSuccess) throw new InvalidOperationException(generated.Error.ToString());
                var row = generated.Data[0];
                if (row.Label != "unity" || row.Count != 42 || (string)row.Nested["name"] != "fixture" ||
                    row.Items.Count != 2 || row.Items[1] != 2)
                    throw new InvalidOperationException("Generated model values were not preserved.");
                var result = await client.Database.RpcAsync<Handwritten>("probe_row");
                if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
                var value = result.Data;
                if (value.Label != "unity" || value.Count != 42 || value.Value.Name != "fixture" ||
                    value.Items.Length != 2 || value.Items[1] != 2)
                    throw new InvalidOperationException("Handwritten model values were not preserved.");
            }
            Debug.Log("SUPABASE_IL2CPP_MODEL_PROBE_PASSED");
            Application.Quit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Application.Quit(1);
        }
    }
}
"""

BUILD = r"""using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildRuntimeModelProbe
{
    public static void Generate()
    {
        var generator = Type.GetType("Supabase.Unity.Editor.SupabaseModelGeneratorWindow, Supabase.Unity.Editor", true);
        var method = generator.GetMethod("GenerateModel", BindingFlags.Static | BindingFlags.NonPublic);
        var definition = JObject.Parse(@"{""properties"":{""label"":{""type"":""string""},""count"":{""type"":""integer""},""nested"":{""type"":""object""},""items"":{""type"":""array"",""items"":{""type"":""integer""}}}}");
        var generated = (string)method.Invoke(null, new object[] { "probe_rows", "GeneratedRow", definition, "Probe.Models" });
        File.WriteAllText("Assets/GeneratedRow.cs", generated);
    }

    public static void Build()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Medium);
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var probe = new GameObject("Runtime model probe");
        probe.AddComponent(Type.GetType("RuntimeModelProbe, Assembly-CSharp", true));
        const string path = "Assets/RuntimeModelProbe.unity";
        EditorSceneManager.SaveScene(scene, path);
        var output = Environment.GetEnvironmentVariable("SUPABASE_PROBE_OUTPUT");
        var report = BuildPipeline.BuildPlayer(new[] { path }, output, BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("IL2CPP build failed.");
    }
}
"""

class ModelFixture(BaseHTTPRequestHandler):
    def do_GET(self):
        self.reply(as_list=True)

    def do_POST(self):
        self.reply(as_list=False)

    def reply(self, as_list):
        row = b'{"label":"unity","count":42,"nested":{"name":"fixture"},"items":[1,2]}'
        body = b"[" + row + b"]" if as_list else row
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):
        pass


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", required=True, type=Path, help="Unity 6 Editor executable")
    parser.add_argument("--output", required=True, type=Path, help="New directory for the probe and logs")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True)
    project = output / "project"
    editor = project / "Assets/Editor"
    editor.mkdir(parents=True)
    (project / "ProjectSettings").mkdir()
    shutil.copytree(ROOT / PACKAGE, project / PACKAGE)
    (project / "Packages/manifest.json").write_text(
        '{"dependencies":{"com.unity.modules.imgui":"1.0.0",'
        '"com.unity.modules.jsonserialize":"1.0.0",'
        '"com.unity.modules.unitywebrequest":"1.0.0"}}\n', encoding="utf-8")
    (editor / "BuildRuntimeModelProbe.cs").write_text(BUILD, encoding="utf-8")
    player = output / "player/RuntimeModelProbe.exe"
    player.parent.mkdir()
    environment = os.environ.copy()
    environment["SUPABASE_PROBE_OUTPUT"] = str(player)
    unity = [str(args.unity.resolve()), "-batchmode", "-nographics", "-quit", "-projectPath", str(project)]
    subprocess.run(unity + ["-executeMethod", "BuildRuntimeModelProbe.Generate", "-logFile",
                           str(output / "generate.log")], env=environment, timeout=900, check=True)
    (project / "Assets/RuntimeModelProbe.cs").write_text(PROBE, encoding="utf-8")
    subprocess.run(unity + ["-executeMethod", "BuildRuntimeModelProbe.Build", "-logFile",
                           str(output / "build.log")], env=environment, timeout=900, check=True)

    runtime_log = output / "runtime.log"
    with ThreadingHTTPServer(("127.0.0.1", 0), ModelFixture) as server:
        worker = Thread(target=server.serve_forever, daemon=True)
        worker.start()
        environment["SUPABASE_PROBE_URL"] = "http://127.0.0.1:" + str(server.server_port)
        try:
            subprocess.run([str(player), "-batchmode", "-nographics", "-logFile", str(runtime_log)],
                           env=environment, timeout=90, check=True)
        finally:
            server.shutdown()

    if SUCCESS not in runtime_log.read_text(encoding="utf-8", errors="replace"):
        raise SystemExit("The player did not report a successful model round trip. See " + str(runtime_log))
    print("Windows x64 IL2CPP model probe passed with Medium managed stripping.")


if __name__ == "__main__":
    main()
