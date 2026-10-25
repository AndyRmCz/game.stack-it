using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class VideoOrientationTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void CameraRenderedFramesAreAlwaysFlipped(bool uvStartsAtTop)
        {
            // The end card is drawn by a camera into a texture: bottom row first on every graphics
            // API, so it came out upside down on Vulkan, where screen captures need no flip.
            Assert.IsTrue(VideoOrientation.Flip(VideoSource.Camera, uvStartsAtTop, invert: false));
            Assert.IsTrue(VideoOrientation.Flip(VideoSource.Camera, uvStartsAtTop, invert: true), "the capture toggle doesn't apply");
        }

        [Test]
        public void ScreenCapturesAreFlippedOnlyOnOpenGl()
        {
            Assert.IsFalse(VideoOrientation.Flip(VideoSource.ScreenCapture, uvStartsAtTop: true, invert: false), "Vulkan, Metal");
            Assert.IsTrue(VideoOrientation.Flip(VideoSource.ScreenCapture, uvStartsAtTop: false, invert: false), "OpenGL ES");
        }

        [Test]
        public void TheInvertToggleFlipsScreenCaptures()
        {
            Assert.IsTrue(VideoOrientation.Flip(VideoSource.ScreenCapture, uvStartsAtTop: true, invert: true));
            Assert.IsFalse(VideoOrientation.Flip(VideoSource.ScreenCapture, uvStartsAtTop: false, invert: true));
        }
    }
}
