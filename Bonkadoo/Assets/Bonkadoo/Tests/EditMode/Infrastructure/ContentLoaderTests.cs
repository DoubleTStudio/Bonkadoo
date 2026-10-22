using System.Collections.Generic;
using System.Linq;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Content;
using Bonkadoo.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Bonkadoo.Tests.Infrastructure
{
    public class ContentLoaderTests
    {
        [Test]
        public void KitContentLoads()
        {
            var c = TestContent.Load();
            Assert.AreEqual(45, c.Rules.Puzzles.Count);
            Assert.AreEqual(5, c.Chapters.Count);
            Assert.AreEqual(15, c.Scenes.Scenes.Count);
            Assert.Greater(c.Scenes.Items.Count, 45);
            Assert.Greater(c.Rules.OrderedRules.Count, 400, "step rules + look/talk/memory rules");
            Assert.AreEqual(5, c.World.Metrics.Count);
            CollectionAssert.AreEquivalent(new[] { "en", "vi" }, c.Localization.Keys);
            Assert.AreEqual(5, c.YarnSources.Count);
        }

        [Test]
        public void PrerequisitesGateRules()
        {
            var c = TestContent.Load();
            var session = NewSession(c);
            session.EnterScene("scene.ch01.office", "boot", 0);
            // p02's lost-form step must not fire before p01 is complete
            var outcome = session.Interact(new InteractionRequest("r1", InteractionVerb.Inspect, null, "prop.ch01.lost_tray",
                "scene.ch01.office", 1));
            Assert.AreNotEqual("rule.ch01.p02.a.s1", outcome.MatchedRuleId);
            Assert.IsFalse(session.State.HasItem("item.ch01.lost_form"));
        }

        [Test]
        public void EveryPropAndExitGivesFeedback()
        {
            // GDD pillar 1: every touch creates feedback
            var c = TestContent.Load();
            foreach (var scene in c.Scenes.Scenes)
            {
                var session = NewSession(c);
                session.EnterScene(scene.Id, "s", 0);
                foreach (var h in scene.Hotspots)
                {
                    var verb = h.Type == HotspotType.Npc ? InteractionVerb.Talk : InteractionVerb.Inspect;
                    var o = session.Interact(new InteractionRequest("t-" + h.Id, verb, null, h.Id, scene.Id, 1));
                    Assert.IsTrue(o.DialogueKeys.Count > 0 || o.FeedbackKey != null, $"{h.Id} is silent");
                }
            }
        }

        [TestCase("{\"eval\": \"System.IO.File.Delete('x')\"}")]
        [TestCase("{\"flagEquals\": {\"key\": \"f\"}}")]
        [TestCase("{\"all\": [], \"any\": []}")]
        [TestCase("{\"requestVerb\": \"Explode\"}")]
        [TestCase("[]")]
        public void RejectsNonAllowlistedOrMalformedPredicates(string json)
        {
            Assert.Throws<ContentException>(() => ContentLoader.ParsePredicate(JToken.Parse(json), "test"));
        }

        [TestCase("{\"setFlag\": {\"key\": \"input.x\", \"value\": true}}")]
        [TestCase("{\"runScript\": {\"code\": \"x\"}}")]
        [TestCase("{\"adjustMetric\": {\"key\": \"trust_bop\", \"delta\": \"1\"}}")]
        public void RejectsNonAllowlistedOrMalformedEffects(string json)
        {
            Assert.Throws<ContentException>(() => ContentLoader.ParseEffect(JToken.Parse(json), "test"));
        }

        [Test]
        public void ParsesEveryAllowlistedOperator()
        {
            var predicate = JToken.Parse(@"{""all"": [
                {""any"": [{""hasItem"": {""itemId"": ""i""}}, {""not"": {""eventOccurred"": {""type"": ""evt.x""}}}]},
                {""metricAtLeast"": {""key"": ""trust_bop"", ""value"": 1}},
                {""npcDispositionAtLeast"": {""npcId"": ""npc.a"", ""value"": -1}},
                {""currentScene"": ""scene.x""}, {""requestVerb"": ""Talk""},
                {""requestTarget"": ""t""}, {""requestSource"": ""s""}, {""flagEquals"": {""key"": ""f"", ""value"": 3}}]}");
            Assert.IsInstanceOf<AllPredicate>(ContentLoader.ParsePredicate(predicate, "p"));

            foreach (var e in new[]
                     {
                         @"{""setFlag"": {""key"": ""f"", ""value"": ""x""}}", @"{""adjustMetric"": {""key"": ""k"", ""delta"": -1}}",
                         @"{""addItem"": {""itemId"": ""i""}}", @"{""removeItem"": {""itemId"": ""i""}}",
                         @"{""emitEvent"": {""type"": ""evt.x""}}", @"{""queueDialogue"": {""key"": ""d""}}",
                         @"{""queueAnimation"": {""cue"": ""c""}}", @"{""unlockExit"": {""exitId"": ""x""}}",
                         @"{""setNpcDisposition"": {""npcId"": ""n"", ""value"": 1}}",
                     })
                Assert.IsNotNull(ContentLoader.ParseEffect(JToken.Parse(e), "e"), e);
        }

        [Test]
        public void CrossCheckCatchesUnknownEventAndMissingDialogue()
        {
            var src = TestContent.Sources();
            var puzzles = JArray.Parse(src.PuzzlesJson);
            var effects = (JArray)puzzles[0]["rules"][1]["effects"];
            effects.Add(JObject.Parse("{\"emitEvent\": {\"type\": \"evt.does.not.exist\"}}"));
            effects.Add(JObject.Parse("{\"queueDialogue\": {\"key\": \"dlg.missing\"}}"));
            src.PuzzlesJson = puzzles.ToString();
            var ex = Assert.Throws<ContentException>(() => ContentLoader.Load(src));
            StringAssert.Contains("evt.does.not.exist", ex.Message);
            StringAssert.Contains("dlg.missing", ex.Message);
        }

        private static GameSession NewSession(GameContent c) =>
            new GameSession(c.World, new InteractionEngine(c.Rules), new InMemoryEventStore());

    }
}
