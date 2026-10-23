using TMPro;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// TMP font assets baked by Stack IT ▸ Setup Project (or automatically on editor load) into
    /// Resources/StackIt/Fonts: Fredoka for the logo, numbers, titles and primary buttons, Nunito
    /// for everything else, Material Symbols Rounded for icons (outline set, plus a filled set for
    /// primary buttons). The text fonts fall back to the outline icons, so an icon codepoint can sit
    /// inside a label. Missing assets fall back to TMP's default font (no icons), with a warning.
    /// </summary>
    public static class Fonts
    {
        public const string ResourceDir = "StackIt/Fonts/";
        public const string DisplayName = "Fredoka Bold SDF";
        public const string DisplaySemiName = "Fredoka SemiBold SDF";
        public const string BodyName = "Nunito Bold SDF";
        public const string BodyHeavyName = "Nunito ExtraBold SDF";
        public const string IconsName = "Icons Outline SDF";
        public const string IconsFilledName = "Icons Filled SDF";

        private static TMP_FontAsset _display, _displaySemi, _body, _bodyHeavy, _icons, _iconsFilled;

        /// <summary>Fredoka 700.</summary>
        public static TMP_FontAsset Display => Load(ref _display, DisplayName);
        /// <summary>Fredoka 600 (coin counter).</summary>
        public static TMP_FontAsset DisplaySemi => Load(ref _displaySemi, DisplaySemiName);
        /// <summary>Nunito 700.</summary>
        public static TMP_FontAsset Body => Load(ref _body, BodyName);
        /// <summary>Nunito 800.</summary>
        public static TMP_FontAsset BodyHeavy => Load(ref _bodyHeavy, BodyHeavyName);
        public static TMP_FontAsset Icons => Load(ref _icons, IconsName);
        public static TMP_FontAsset IconsFilled => Load(ref _iconsFilled, IconsFilledName);

        private static bool _warned;

        private static TMP_FontAsset Load(ref TMP_FontAsset cache, string name)
        {
            if (cache == null) cache = Resources.Load<TMP_FontAsset>(ResourceDir + name);
            if (cache != null) return cache;
            if (!_warned)
            {
                // Icons render as empty boxes in the fallback font, so say why.
                Debug.LogWarning($"Missing Resources/{ResourceDir}{name}.asset; run Stack IT ▸ Setup Project. Using TMP's default font (no icons).");
                _warned = true;
            }
            return TMP_Settings.defaultFontAsset;
        }
    }

    /// <summary>
    /// Material Symbols Rounded codepoints (same in the outline and filled fonts). Keep in sync with
    /// the subset in Fonts/MaterialSymbolsRounded-*.ttf (docs/THEMING.md has the subsetting command).
    /// </summary>
    public static class Icons
    {
        public const string Friends = "";     // group
        public const string Ranks = "";       // leaderboard
        public const string Shop = "";        // shopping_bag
        public const string Settings = "";    // settings
        public const string Gift = "";        // redeem
        public const string Back = "";        // arrow_back_ios_new
        public const string Close = "";       // close
        public const string Play = "";        // play_arrow
        public const string Replay = "";      // replay
        public const string Home = "";        // home
        public const string Share = "";       // ios_share
        public const string AddFriend = "";   // person_add
        public const string Add = "";         // add
        public const string Check = "";       // check
        public const string Lock = "";        // lock
        public const string Trophy = "";      // emoji_events
        public const string Bolt = "";        // bolt
        public const string Sound = "";       // volume_up
        public const string Haptics = "";     // vibration
        public const string Language = "";    // language
        public const string Link = "";        // link
        public const string Delete = "";      // delete
        public const string Star = "";        // star
        public const string Copy = "";        // content_copy
        public const string Refresh = "";     // refresh
        public const string Coin = "";        // paid
        public const string Ticket = "";      // confirmation_number
        public const string Pause = "";       // pause
        public const string VideoAd = "";     // smart_display
        public const string Music = "";       // music_note
        public const string Look = "";        // palette
        public const string Motion = "";      // animation
        public const string Offline = "";     // wifi_off
        public const string Error = "";       // error
        public const string Invite = "";      // group_add
        public const string Challenge = "";   // swords
        public const string Person = "";      // person
        public const string Timer = "";       // timer
        public const string Chevron = "";     // chevron_right
        public const string Privacy = "";     // shield
        public const string Terms = "";       // description
        public const string Dark = "";        // dark_mode
        public const string Light = "";       // light_mode
        public const string Hourglass = "";   // hourglass_top
        public const string CloudOff = "";    // cloud_off

        /// <summary>Every glyph above: what Setup Project bakes into the icon atlases.</summary>
        public const string All = Friends + Ranks + Shop + Settings + Gift + Back + Close + Play + Replay + Home
            + Share + AddFriend + Add + Check + Lock + Trophy + Bolt + Sound + Haptics + Language + Link + Delete
            + Star + Copy + Refresh + Coin + Ticket + Pause + VideoAd + Music + Look + Motion + Offline + Error
            + Invite + Challenge + Person + Timer + Chevron + Privacy + Terms + Dark + Light + Hourglass + CloudOff;
    }
}
