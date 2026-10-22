using TMPro;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// TMP font assets baked by Stack IT ▸ Setup Project into Resources/StackIt/Fonts: Fredoka for
    /// numbers and titles, Nunito Sans for text, Material Symbols Rounded for icons (also a fallback
    /// of the text fonts, so an icon codepoint can sit inside any label). Missing assets fall back to
    /// TMP's default font so the game still runs before setup.
    /// </summary>
    public static class Fonts
    {
        public const string ResourceDir = "StackIt/Fonts/";
        public const string DisplayName = "Display SDF";
        public const string BodyName = "Body SDF";
        public const string BodyBoldName = "BodyBold SDF";
        public const string IconsName = "Icons SDF";

        private static TMP_FontAsset _display, _body, _bodyBold, _icons;

        public static TMP_FontAsset Display => Load(ref _display, DisplayName);
        public static TMP_FontAsset Body => Load(ref _body, BodyName);
        public static TMP_FontAsset BodyBold => Load(ref _bodyBold, BodyBoldName);
        public static TMP_FontAsset Icons => Load(ref _icons, IconsName);

        private static TMP_FontAsset Load(ref TMP_FontAsset cache, string name)
        {
            if (cache == null) cache = Resources.Load<TMP_FontAsset>(ResourceDir + name);
            return cache != null ? cache : TMP_Settings.defaultFontAsset;
        }
    }

    /// <summary>Material Symbols Rounded codepoints. Keep in sync with the subset in Fonts/MaterialSymbolsRounded.ttf.</summary>
    public static class Icons
    {
        public const string Friends = "";     // group
        public const string Ranks = "";       // leaderboard
        public const string Shop = "";        // storefront
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

        /// <summary>Every glyph above: what Setup Project bakes into the icon atlas.</summary>
        public const string All = Friends + Ranks + Shop + Settings + Gift + Back + Close + Play + Replay + Home
            + Share + AddFriend + Add + Check + Lock + Trophy + Bolt + Sound + Haptics + Language + Link + Delete
            + Star + Copy + Refresh + Coin;
    }
}
