namespace StackIt.Client.Music
{
    /// <summary>
    /// Every soundtrack. The ids are shop item ids (the backend's `skins` rows of kind `music`);
    /// Classic is the free default.
    /// </summary>
    public static class MusicStyles
    {
        public const string Default = ClassicStyle.StyleId;

        public static readonly string[] Ids =
        {
            ClassicStyle.StyleId,
            SynthwaveStyle.StyleId,
            LofiStyle.StyleId,
            ReggaetonStyle.StyleId,
            KpopStyle.StyleId,
            MetalStyle.StyleId,
        };

        /// <summary>A new instance of the style (unknown or missing ids get Classic).</summary>
        public static MusicStyle Create(string id)
        {
            switch (id)
            {
                case SynthwaveStyle.StyleId: return new SynthwaveStyle();
                case LofiStyle.StyleId: return new LofiStyle();
                case ReggaetonStyle.StyleId: return new ReggaetonStyle();
                case KpopStyle.StyleId: return new KpopStyle();
                case MetalStyle.StyleId: return new MetalStyle();
                default: return new ClassicStyle();
            }
        }
    }
}
