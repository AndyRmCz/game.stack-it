using StackIt.Client.Theming;
using UnityEngine.UI;

namespace StackIt.UI
{
    /// <summary>Colours an Image, a TMP label or any other Graphic with one token.</summary>
    public sealed class ThemedGraphic : Themed
    {
        public Graphic Target;
        public string Token;
        public float Alpha = 1f;

        public static ThemedGraphic Bind(Graphic target, string token, float alpha = 1f)
        {
            var themed = target.GetComponent<ThemedGraphic>();
            if (themed == null) themed = target.gameObject.AddComponent<ThemedGraphic>();
            themed.Target = target;
            themed.Token = token;
            themed.Alpha = alpha;
            themed.Refresh();
            return themed;
        }

        public override void Apply(ThemePalette palette)
        {
            if (Target == null || string.IsNullOrEmpty(Token)) return; // "" = unset (Unity serializes null strings as "")
            var c = Get(palette, Token);
            c.a *= Alpha;
            Target.color = c;
        }
    }
}
