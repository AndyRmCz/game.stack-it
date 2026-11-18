namespace StackIt.Client
{
    /// <summary>
    /// Which of the app icon's pictures goes into a platform icon slot, by how many layers the slot
    /// takes. Android 8+ slots take two (the adaptive icon: background, then foreground), which the
    /// launcher crops to its own shape, so they get the night and Blocky drawn apart and sized for
    /// that crop (backend/scripts/generate_app_icons.py). One-layer slots get the whole icon.
    /// </summary>
    public sealed class AppIconLayers<T> where T : class
    {
        private readonly T _icon;
        private readonly T _background;
        private readonly T _foreground;

        public AppIconLayers(T icon, T background, T foreground)
        {
            _icon = icon;
            _background = background;
            _foreground = foreground;
        }

        public T[] For(int layers)
        {
            if (layers < 2) return new[] { _icon };
            // The whole icon cropped would cut off Blocky: an empty slot falls back to the legacy icon.
            if (_background == null || _foreground == null) return new T[0];
            return new[] { _background, _foreground };
        }
    }
}
