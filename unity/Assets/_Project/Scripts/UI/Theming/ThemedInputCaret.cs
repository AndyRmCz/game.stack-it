using StackIt.Client.Theming;
using TMPro;

namespace StackIt.UI
{
    /// <summary>The text field's caret and selection in the accent colour.</summary>
    public sealed class ThemedInputCaret : Themed
    {
        public TMP_InputField Field;

        public override void Apply(ThemePalette p)
        {
            if (Field == null) return;
            var accent = Get(p, T.Accent);
            Field.caretColor = accent;
            accent.a = 0.3f;
            Field.selectionColor = accent;
        }
    }
}
