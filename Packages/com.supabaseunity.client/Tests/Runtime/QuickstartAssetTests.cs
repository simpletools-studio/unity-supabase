using System;
using System.Collections.Generic;
using NUnit.Framework;
using Supabase.Unity.Editor;

namespace Supabase.Unity.Tests
{
    public sealed class QuickstartAssetTests
    {
        [TestCase("Assets/SupabaseUnity/Samples/Quickstart/")]
        [TestCase("Assets/Samples/Supabase for Unity/0.4.0/Quickstart/")]
        public void Discovery_ResolvesSceneAndSqlFromSameImportedCopy(string folder)
        {
            var files = new HashSet<string> { folder + "SupabaseQuickstart.cs", folder + "Quickstart.unity", folder + "setup.sql" };
            var sample = SupabaseQuickstartAssets.Resolve(files, files.Contains);
            Assert.AreEqual(folder + "Quickstart.unity", sample.ScenePath);
            Assert.AreEqual(folder + "setup.sql", sample.SqlPath);
            Assert.AreEqual(1, sample.CopyCount);
        }

        [Test]
        public void Discovery_DoesNotMixCompanionFilesBetweenCopies()
        {
            var files = new HashSet<string> { "Assets/A/SupabaseQuickstart.cs", "Assets/B/SupabaseQuickstart.cs",
                "Assets/B/Quickstart.unity", "Assets/B/setup.sql" };
            var sample = SupabaseQuickstartAssets.Resolve(new[] { "Assets/B/SupabaseQuickstart.cs", "Assets/A/SupabaseQuickstart.cs" }, files.Contains);
            Assert.AreEqual("Assets/A/SupabaseQuickstart.cs", sample.ScriptPath);
            Assert.AreEqual(2, sample.CopyCount);
            Assert.IsNull(sample.ScenePath);
            Assert.IsNull(sample.SqlPath);
        }

        [Test]
        public void Discovery_ExcludesPackageSourcesAndSimilarScriptNames()
        {
            var candidates = new[] { "Packages/com.supabaseunity.client/Samples~/Quickstart/SupabaseQuickstart.cs",
                "Assets/../Packages/SupabaseQuickstart.cs", "Assets/CopySupabaseQuickstart.cs", "Assets/X/./SupabaseQuickstart.cs" };
            Assert.IsNull(SupabaseQuickstartAssets.Resolve(candidates, path => true));
        }

        [Test]
        public void Discovery_DiscardsDeletedCandidatesAndDuplicateSearchResults()
        {
            var sample = SupabaseQuickstartAssets.Resolve(new[] { "Assets/Old/SupabaseQuickstart.cs",
                "Assets/Current/SupabaseQuickstart.cs", "Assets/Current/SupabaseQuickstart.cs" },
                path => path == "Assets/Current/SupabaseQuickstart.cs");
            Assert.AreEqual(1, sample.CopyCount);
            Assert.AreEqual("Assets/Current/SupabaseQuickstart.cs", sample.ScriptPath);
        }

        [Test]
        public void SceneOpening_CancelledSavePromptDoesNotReplaceScene()
        {
            bool opened = false;
            Assert.IsFalse(SupabaseQuickstartAssets.TryOpenScene("Assets/Demo/Quickstart.unity", () => false,
                path => true, () => false, path => opened = true));
            Assert.IsFalse(opened);
        }

        [Test]
        public void SceneOpening_PlayTransitionDoesNotPromptOrOpen()
        {
            Assert.IsFalse(SupabaseQuickstartAssets.TryOpenScene("Assets/Demo/Quickstart.unity", () => true,
                path => true, () => { Assert.Fail("Must not prompt while entering Play mode."); return true; },
                path => Assert.Fail("Must not replace a playing scene.")));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SceneOpening_RechecksPlayStateAndExistenceAfterSaveDialog(bool playTransition)
        {
            bool afterPrompt = false;
            Assert.IsFalse(SupabaseQuickstartAssets.TryOpenScene("Assets/Demo/Quickstart.unity",
                () => afterPrompt && playTransition, path => !afterPrompt || playTransition,
                () => { afterPrompt = true; return true; }, path => Assert.Fail("Scene became unavailable during the dialog.")));
        }

        [TestCase(null)]
        [TestCase("Packages/Example/Quickstart.unity")]
        [TestCase("Assets/../Other/Quickstart.unity")]
        [TestCase("Assets/Other.unity")]
        public void SceneOpening_InvalidTargetDoesNotPrompt(string path)
        {
            Assert.IsFalse(SupabaseQuickstartAssets.TryOpenScene(path, () => false, value => true,
                () => { Assert.Fail("Invalid target must not open a save dialog."); return true; },
                value => Assert.Fail("Invalid target must not be opened.")));
        }

        [Test]
        public void SceneOpening_ValidTargetSavesBeforeOpeningExactlyOnce()
        {
            var actions = new List<string>();
            Assert.IsTrue(SupabaseQuickstartAssets.TryOpenScene("Assets/Demo/Quickstart.unity", () => false,
                path => true, () => { actions.Add("save"); return true; }, path => actions.Add(path)));
            CollectionAssert.AreEqual(new[] { "save", "Assets/Demo/Quickstart.unity" }, actions);
        }
    }
}
