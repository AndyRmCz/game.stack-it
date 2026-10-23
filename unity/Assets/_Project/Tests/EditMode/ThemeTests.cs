using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StackIt.Client;
using StackIt.Client.Theming;

namespace StackIt.Tests
{
    internal static class Themes
    {
        private static ThemeCatalog _catalog;

        public static ThemeCatalog Catalog => _catalog ??= ThemeCatalog.Parse(File.ReadAllText(TestPaths.ThemesJson()));
    }

    public class ThemeTokenTests
    {
        private static IEnumerable<string> TokenConstants() =>
            typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue());

        [Test]
        public void BothThemesDefineTheSameTokens()
        {
            ThemePalette pop = Themes.Catalog.Get(ThemeId.Pop), neon = Themes.Catalog.Get(ThemeId.Neon);
            CollectionAssert.AreEquivalent(pop.Tokens.ToList(), neon.Tokens.ToList());
        }

        [Test]
        public void EveryTokenNameUsedInCodeExistsInBothThemes()
        {
            foreach (ThemePalette theme in Themes.Catalog.All)
            foreach (string token in TokenConstants())
                Assert.IsTrue(theme.Has(token), $"{theme.Id} is missing '{token}'");
        }

        [Test]
        public void EveryTokenValueParses()
        {
            foreach (ThemePalette theme in Themes.Catalog.All)
            foreach (string token in theme.Tokens)
            {
                object raw = theme.Raw(token);
                switch (raw)
                {
                    case string _: theme.Color(token); break;
                    case List<object> _: theme.Colors(token); break;
                    case Dictionary<string, object> _: theme.Glow(token); break;
                    case double _: break;
                    default: Assert.Fail($"{theme.Id} '{token}' has an unexpected value {raw}"); break;
                }
            }
        }

        [Test]
        public void ScriptsUseTokenConstantsNotStringLiterals()
        {
            // A typo in a literal would silently render the wrong colour; T's constants are checked above.
            var names = new HashSet<string>(TokenConstants());
            var literal = new Regex("\"([a-zA-Z0-9]+(\\.[a-zA-Z0-9]+)?)\"");
            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(TestPaths.ScriptsDir(), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "T.cs") continue;
                foreach (Match m in literal.Matches(File.ReadAllText(file)))
                    if (names.Contains(m.Groups[1].Value) && m.Groups[1].Value.Contains("."))
                        offenders.Add($"{Path.GetFileName(file)}: {m.Value}");
            }
            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void TheUnityCopyMatchesTheSharedFile()
        {
            string copy = Path.Combine(TestPaths.ResourcesDir(), "themes.json");
            Assert.AreEqual(File.ReadAllText(TestPaths.ThemesJson()), File.ReadAllText(copy),
                "Resources/StackIt/themes.json is stale: Unity copies shared/theme/themes.json on import (or run Stack IT ▸ Setup Project).");
        }

        [Test]
        public void TheDefaultLookIsNeon() => Assert.AreEqual(ThemeId.Neon, Themes.Catalog.Default);
    }

    public class ThemeContrastTests
    {
        private const double Body = 4.5;  // WCAG AA, normal text
        private const double Large = 3.0; // text at 24 pt bold and up

        private static void AtLeast(double ratio, Rgba text, Rgba under, string what)
        {
            double c = ColorMath.Contrast(text, under);
            Assert.GreaterOrEqual(c, ratio, $"{what}: {text} on {under} is {c:0.00}:1");
        }

        [Test]
        public void TextMeetsContrastOnEveryBackgroundAndSurface()
        {
            foreach (ThemePalette t in Themes.Catalog.All)
            foreach (Rgba bg in t.Colors(T.BgGradient))
            {
                string at = $"{t.Id} over {bg}";
                Rgba surface = ColorMath.Over(t.Color(T.Surface), bg);
                AtLeast(Body, t.Color(T.TextPrimary), bg, at + " text.primary");
                AtLeast(Body, t.Color(T.TextSecondary), bg, at + " text.secondary");
                AtLeast(Body, t.Color(T.TextPrimary), surface, at + " text.primary on surface");
                AtLeast(Body, t.Color(T.TextSecondary), surface, at + " text.secondary on surface");
                AtLeast(Body, t.Color(T.SecondaryText), ColorMath.Over(t.Color(T.SecondaryFill), bg), at + " secondary.text on secondary.fill");
                AtLeast(Body, t.Color(T.NavLabel), ColorMath.Over(t.Color(T.SurfaceNav), bg), at + " nav.label on surface.nav");
                AtLeast(Body, t.Color(T.Danger), surface, at + " danger text on surface");
                Rgba raised = ColorMath.Over(t.Color(T.SurfaceRaised), bg);
                AtLeast(Body, t.Color(T.TextPrimary), raised, at + " text.primary on surface.raised");
                AtLeast(Body, t.Color(T.TextSecondary), raised, at + " text.secondary on surface.raised");
                AtLeast(Body, t.Color(T.SecondaryText), ColorMath.Over(t.Color(T.SecondaryFill), raised), at + " secondary.text on a raised sheet");
            }
        }

        [Test]
        public void ButtonLabelsMeetContrast()
        {
            foreach (ThemePalette t in Themes.Catalog.All)
            {
                AtLeast(Body, t.Color(T.AccentOn), t.Color(T.Accent), $"{t.Id} accent.onAccent on accent");
                AtLeast(Large, t.Color(T.DangerOn), t.Color(T.Danger), $"{t.Id} danger.onDanger on danger");
                AtLeast(Body, t.Color(T.ToastText), t.Color(T.ToastFill), $"{t.Id} toast");
                AtLeast(Body, t.Color(T.Accent2On), t.Color(T.Accent2), $"{t.Id} accent2.onAccent2 on accent2");
                foreach (string medal in new[] { T.MedalGold, T.MedalSilver, T.MedalBronze })
                    AtLeast(Large, t.Color(T.MedalInk), t.Color(medal), $"{t.Id} medal.ink on {medal}");
            }
        }
    }

    public class HeightRampTests
    {
        private static readonly Rgba[] Stops =
        {
            new Rgba(1, 0, 0), new Rgba(0, 1, 0), new Rgba(0, 0, 1),
        };

        [Test]
        public void ReturnsTheExactStopColourAtEachStop()
        {
            for (int i = 0; i < Stops.Length; i++)
                Assert.AreEqual(Stops[i], HeightRamp.At(Stops, i * HeightRamp.BlocksPerStop));
        }

        [Test]
        public void BlendsHalfwayBetweenStops()
        {
            Rgba mid = HeightRamp.At(Stops, HeightRamp.BlocksPerStop * 1.5f);
            Assert.AreEqual(0.5f, mid.G, 1e-5f);
            Assert.AreEqual(0.5f, mid.B, 1e-5f);
        }

        [Test]
        public void LoopsBackThroughTheFirstStop()
        {
            int cycle = Stops.Length * HeightRamp.BlocksPerStop;
            Assert.AreEqual(Stops[0], HeightRamp.At(Stops, cycle));
            Assert.AreEqual(Stops[1], HeightRamp.At(Stops, cycle + HeightRamp.BlocksPerStop));
            // Between the last stop and the loop: blue blends back to red.
            Rgba wrap = HeightRamp.At(Stops, cycle - HeightRamp.BlocksPerStop / 2f);
            Assert.AreEqual(0.5f, wrap.R, 1e-5f);
            Assert.AreEqual(0.5f, wrap.B, 1e-5f);
        }

        [Test]
        public void ThemeRampsStartAtTheBaseBlock()
        {
            foreach (ThemePalette t in Themes.Catalog.All)
                Assert.AreEqual(t.Colors(T.BlockBase)[0], t.Colors(T.BlockRamp)[0], t.Id.ToString());
        }

        [Test]
        public void SideFacesAreTheTopShaded()
        {
            HeightRamp.Faces(new Rgba(1f, 0.5f, 0.2f), out Rgba left, out Rgba right);
            Assert.AreEqual(0.85f, left.R, 1e-5f);
            Assert.AreEqual(0.35f, right.G, 1e-5f);
        }
    }

    public class ThemeServiceTests
    {
        private sealed class MemoryStore : IKeyValueStore
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public string GetString(string key) => Values.TryGetValue(key, out string v) ? v : null;
            public void SetString(string key, string value) => Values[key] = value;
            public void Delete(string key) => Values.Remove(key);
        }

        [Test]
        public void FirstLaunchUsesNeon()
        {
            var service = new ThemeService(Themes.Catalog, new MemoryStore());
            Assert.AreEqual(ThemeId.Neon, service.Current);
            Assert.AreEqual(ThemeId.Neon, service.Palette.Id);
        }

        [Test]
        public void TheChoiceIsSavedAndRestored()
        {
            var store = new MemoryStore();
            new ThemeService(Themes.Catalog, store).Set(ThemeId.Pop);
            Assert.AreEqual(ThemeId.Pop, new ThemeService(Themes.Catalog, store).Current);
        }

        [Test]
        public void AnUnknownSavedValueFallsBackToTheDefault()
        {
            var store = new MemoryStore();
            store.SetString(ThemeService.StoreKey, "sepia");
            Assert.AreEqual(ThemeId.Neon, new ThemeService(Themes.Catalog, store).Current);
        }

        [Test]
        public void ChangedIsRaisedOncePerActualChange()
        {
            var service = new ThemeService(Themes.Catalog, new MemoryStore());
            var raised = new List<ThemeId>();
            service.Changed += p => raised.Add(p.Id);

            service.Set(ThemeId.Neon); // already current
            service.Set(ThemeId.Pop);
            service.Set(ThemeId.Pop);
            service.Set(ThemeId.Neon);

            CollectionAssert.AreEqual(new[] { ThemeId.Pop, ThemeId.Neon }, raised);
        }
    }

    public class MiniJsonTests
    {
        [Test]
        public void ParsesTheShapesThemesJsonUses()
        {
            var root = (Dictionary<string, object>)MiniJson.Parse("{\"a\": [\"#FFF\", 1.5, -2e1], \"b\": {\"c\": true, \"d\": null}, \"e\": \"x\\\"\\u00e9\"}");
            var a = (List<object>)root["a"];
            Assert.AreEqual("#FFF", a[0]);
            Assert.AreEqual(1.5, a[1]);
            Assert.AreEqual(-20.0, a[2]);
            Assert.AreEqual(true, ((Dictionary<string, object>)root["b"])["c"]);
            Assert.IsNull(((Dictionary<string, object>)root["b"])["d"]);
            Assert.AreEqual("x\"é", root["e"]);
        }

        [Test]
        public void RejectsMalformedInput()
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\": 1,}"));
            Assert.Throws<FormatException>(() => MiniJson.Parse("[1] 2"));
        }
    }
}
