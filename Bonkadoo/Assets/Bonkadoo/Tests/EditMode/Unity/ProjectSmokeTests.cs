using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Bonkadoo.Infrastructure.Content;
using Bonkadoo.Infrastructure.Unity;
using Bonkadoo.Tests.Support;
using NUnit.Framework;
using UnityEditor;

namespace Bonkadoo.Tests.Unity
{
    public class ProjectSmokeTests
    {
        [TestCase("Bonkadoo.Domain")]
        [TestCase("Bonkadoo.Application")]
        [TestCase("Bonkadoo.Infrastructure")]
        [TestCase("Bonkadoo.Narrative")]
        public void EngineFreeAssembliesDoNotReferenceUnity(string assemblyName)
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == assemblyName);
            var unityRefs = asm.GetReferencedAssemblies().Where(r => r.Name.StartsWith("Unity")).Select(r => r.Name).ToArray();
            CollectionAssert.IsEmpty(unityRefs);
        }

        [Test]
        public void SyncedResourcesMatchKitContent()
        {
            var resources = Path.Combine("Assets", "Bonkadoo", "Content", "Resources", "BonkadooContent");
            foreach (var rel in new[] { "world/world.json", "puzzles/puzzles.json", "events/catalog.json", "localization/en.json",
                         "localization/vi.json", "chapters/ch01.json", "chapters/ch05.json", "scenes/scenes.json", "scenes/ambient.json",
                         "items/items.json", "dialogue/lines.json", "dialogue/ch03.yarn" })
            {
                var synced = File.ReadAllBytes(Path.Combine(resources, rel.EndsWith(".yarn") ? rel.Replace(".yarn", ".txt") : rel));
                var kit = File.ReadAllBytes(TestContent.ContentPath(rel));
                CollectionAssert.AreEqual(kit, synced, $"{rel} drifted; run Tools/sync_content.py");
            }
        }

        [Test]
        public void ContentLoadsFromResources()
        {
            var content = ContentLoader.Load(ResourcesContent.Load());
            Assert.AreEqual(45, content.Rules.Puzzles.Count);
        }

        [Test]
        public void BuildHasTheFiveArchitectureScenesBootstrapFirst()
        {
            var names = EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
            CollectionAssert.AreEqual(new[] { "Bootstrap", "Menu", "ChapterHub", "Gameplay", "Credits" }, names);
        }

        [Test]
        public void NarrativeCompilesInsideUnityWithPackageDlls()
        {
            var content = ContentLoader.Load(ResourcesContent.Load());
            var flow = new Bonkadoo.Infrastructure.GameFlow(content,
                new Bonkadoo.Infrastructure.Save.SaveService(new Bonkadoo.Infrastructure.Save.InMemoryFileStore()), new Ticks());
            Assert.Greater(flow.Narrative.Nodes.Count, 15);
        }

        private sealed class Ticks : Bonkadoo.Application.ITickSource { public long Now => 1; }

        [Test]
        public void LandscapeOnly()
        {
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortrait);
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortraitUpsideDown);
            Assert.IsTrue(PlayerSettings.allowedAutorotateToLandscapeLeft || PlayerSettings.allowedAutorotateToLandscapeRight);
        }
    }
}
