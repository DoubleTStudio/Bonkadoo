using System.Collections.Generic;
using Bonkadoo.Infrastructure.Localization;
using Bonkadoo.Tests.Support;
using NUnit.Framework;

namespace Bonkadoo.Tests.Infrastructure
{
    public class LocalizerTests
    {
        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Tables() =>
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["en"] = new Dictionary<string, string> { ["ui.play"] = "Play", ["ui.only_en"] = "Only" },
                ["vi"] = new Dictionary<string, string> { ["ui.play"] = "Chơi" },
            };

        [Test]
        public void KitTablesHaveParity()
        {
            CollectionAssert.IsEmpty(Localizer.FindParityGaps(TestContent.Load().Localization));
        }

        [Test]
        public void KitVietnameseTableKeepsDiacritics()
        {
            var vi = TestContent.Load().Localization["vi"];
            Assert.IsTrue(vi.Values.ContainsAnySubstring("ư", "ơ", "đ", "ố"), "vi.json looks unaccented");
        }

        [Test]
        public void SwitchesLanguageWithoutRestartAndNotifies()
        {
            // TC-009 groundwork
            var loc = new Localizer(Tables(), "en", development: false);
            string notified = null;
            loc.LanguageChanged += l => notified = l;
            Assert.AreEqual("Play", loc.Get("ui.play"));
            loc.SetLanguage("vi");
            Assert.AreEqual("Chơi", loc.Get("ui.play"));
            Assert.AreEqual("vi", notified);
        }

        [Test]
        public void MissingKeyFallsBackVisiblyInDevelopmentOnly()
        {
            Assert.AreEqual("[EN] Only", new Localizer(Tables(), "vi", true).Get("ui.only_en"));
            Assert.AreEqual("Only", new Localizer(Tables(), "vi", false).Get("ui.only_en"));
            Assert.AreEqual("⟦ui.nope⟧", new Localizer(Tables(), "vi", true).Get("ui.nope"));
            Assert.AreEqual("", new Localizer(Tables(), "vi", false).Get("ui.nope"));
        }

        [Test]
        public void ReportsParityGaps()
        {
            CollectionAssert.AreEqual(new[] { "vi:ui.only_en" }, Localizer.FindParityGaps(Tables()));
        }

        [Test]
        public void RejectsUnsupportedLanguage()
        {
            Assert.Throws<System.ArgumentException>(() => new Localizer(Tables(), "fr", false));
        }
    }

    internal static class EnumerableExt
    {
        public static bool ContainsAnySubstring(this IEnumerable<string> values, params string[] needles)
        {
            foreach (var v in values)
                foreach (var n in needles)
                    if (v.Contains(n)) return true;
            return false;
        }
    }
}
