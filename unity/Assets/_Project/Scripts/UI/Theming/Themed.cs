using StackIt.Client.Theming;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// Base of the themed components: applies the current look when enabled and whenever it
    /// changes. Builders call <see cref="Refresh"/> right away too, so freshly built (still
    /// inactive) screens are coloured before they first show.
    /// </summary>
    public abstract class Themed : MonoBehaviour
    {
        protected virtual void OnEnable()
        {
            Look.Changed += Apply;
            Apply(Look.P);
        }

        protected virtual void OnDisable() => Look.Changed -= Apply;

        public void Refresh() => Apply(Look.P);

        public abstract void Apply(ThemePalette palette);

        protected static Color Get(ThemePalette p, string token) => Look.ToColor(p.Color(token));
    }
}
