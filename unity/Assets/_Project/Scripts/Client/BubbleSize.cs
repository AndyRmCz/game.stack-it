using System;

namespace StackIt.Client
{
    /// <summary>
    /// How big Blocky's speech bubble is for a line: hugging the text as measured, never wider than
    /// the maximum (longer lines wrap). TextMeshPro measures 0 x 0 until it has woken up; then the
    /// bubble takes the full width and enough lines for the text, instead of shrinking to its
    /// padding with the words stacked one letter a line.
    /// </summary>
    public sealed class BubbleSize
    {
        private const float CharacterWidth = 0.55f, LineHeight = 1.3f; // of the font size, roughly

        private readonly float _maxWidth, _padX, _padY, _fontSize;

        public BubbleSize(float maxWidth, float padX, float padY, float fontSize)
        {
            _maxWidth = maxWidth;
            _padX = padX;
            _padY = padY;
            _fontSize = fontSize;
        }

        /// <param name="measuredWidth">The text's preferred size, wrapped at the inner width.</param>
        public (float width, float height) Fit(string line, float measuredWidth, float measuredHeight)
        {
            float inner = _maxWidth - 2f * _padX;
            if (measuredWidth <= 1f || measuredHeight <= 1f)
            {
                float textWidth = (line?.Length ?? 0) * _fontSize * CharacterWidth;
                int lines = Math.Max(1, (int)Math.Ceiling(textWidth / inner));
                return (_maxWidth, lines * _fontSize * LineHeight + 2f * _padY);
            }
            return (Math.Min(measuredWidth + 1f, inner) + 2f * _padX, measuredHeight + 2f * _padY);
        }
    }
}
