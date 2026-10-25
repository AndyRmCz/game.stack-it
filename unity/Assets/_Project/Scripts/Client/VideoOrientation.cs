namespace StackIt.Client
{
    /// <summary>Where a clip frame comes from before it's scaled into the video texture.</summary>
    public enum VideoSource
    {
        /// <summary>ScreenCapture.CaptureScreenshotIntoRenderTexture: the game as shown.</summary>
        ScreenCapture,

        /// <summary>A camera rendering into a texture (the end card).</summary>
        Camera,
    }

    /// <summary>
    /// The encoders want the top row first, and readback returns rows in texture memory order.
    /// A camera's render texture is stored bottom row first on every graphics API (Unity flips
    /// rendering into textures to match OpenGL), so it's always flipped. A screen capture is
    /// already top row first where UV starts at the top (Vulkan, Metal), so only OpenGL flips it.
    /// <c>invert</c> is the recorder's inspector toggle for a graphics API that captures the
    /// other way round.
    /// </summary>
    public static class VideoOrientation
    {
        public static bool Flip(VideoSource source, bool uvStartsAtTop, bool invert)
        {
            if (source == VideoSource.Camera) return true;
            return uvStartsAtTop == invert;
        }
    }
}
