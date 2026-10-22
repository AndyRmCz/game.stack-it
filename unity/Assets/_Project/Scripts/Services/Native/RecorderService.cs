using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace StackIt.Services.Native
{
    /// <summary>
    /// Always-on "replay buffer" for the current run. Every 1/30 s the final frame (3D scene + HUD
    /// overlays) is captured on the GPU, scaled to 720p and read back asynchronously, then handed
    /// to the native encoder, which keeps only the last ~15 s of H.264 samples in memory. No
    /// screen-recording permission is involved. On export, an end card is appended and an MP4 is
    /// written to the cache folder.
    /// </summary>
    public sealed class RecorderService : MonoBehaviour, IRecorderService
    {
        public const int Width = 720;
        public const int Height = 1280;
        public const int Fps = 30;
        public const int Bitrate = 4_000_000;
        public const int ClipSeconds = 15;
        public const int EndCardFrames = 2 * Fps;

        [Tooltip("Flip the captured image vertically. Toggle if clips come out upside down on a graphics API.")]
        public bool invertFlip;

        public Func<EndCard, IEndCardRenderer> EndCardFactory;

        private RenderTexture _screen;
        private RenderTexture _video;
        private bool _recording;
        private bool _encoderReady;
        private double _startTime;
        private double _lastPts;
        private IntPtr _androidClass = IntPtr.Zero;
        private IntPtr _androidAppend = IntPtr.Zero;

        public bool IsSupported =>
            SystemInfo.supportsAsyncGPUReadback &&
            (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android);

        public void BeginRun()
        {
            if (!IsSupported) return;
            EnsureTargets();
            _encoderReady = StartEncoder();
            if (!_encoderReady) return;
            _recording = true;
            _startTime = Time.realtimeSinceStartupAsDouble;
            _lastPts = 0;
            StopAllCoroutines();
            StartCoroutine(CaptureLoop());
        }

        public void EndRun()
        {
            _recording = false; // the buffer is kept until the next BeginRun or export
        }

        public async Task<string> ExportClipAsync(EndCard card)
        {
            if (!IsSupported || !_encoderReady) return null;
            _recording = false;
            await AppendEndCard(card);

            string path = Path.Combine(Application.temporaryCachePath, $"stackit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.mp4");
            Task<string> done = NativeCallbacks.Instance.Expect("export");
            // Native side keeps whole GOPs: the clip starts at the first keyframe inside the window.
            double seconds = ClipSeconds + EndCardFrames / (double)Fps;
            if (Application.platform == RuntimePlatform.IPhonePlayer) IosNative._StackItRecExport(path, seconds);
            else AndroidNative.CallStatic("VideoRecorder", "export", path, seconds);

            string result = await done;
            return result != null && result.StartsWith("error", StringComparison.Ordinal) ? null : result;
        }

        private IEnumerator CaptureLoop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            double interval = 1.0 / Fps;
            double next = Time.realtimeSinceStartupAsDouble;
            while (_recording)
            {
                yield return endOfFrame;
                double now = Time.realtimeSinceStartupAsDouble;
                if (now < next) continue;
                next = Math.Max(next + interval, now - interval);

                ScreenCapture.CaptureScreenshotIntoRenderTexture(_screen);
                BlitToVideo(_screen);
                double pts = now - _startTime;
                AsyncGPUReadback.Request(_video, 0, ReadbackFormat, request => OnFrame(request, pts));
            }
        }

        private void BlitToVideo(Texture source)
        {
            // Encoders want the top row first; readback returns rows in texture memory order.
            bool flip = !SystemInfo.graphicsUVStartsAtTop;
            if (invertFlip) flip = !flip;
            if (flip) Graphics.Blit(source, _video, new Vector2(1, -1), new Vector2(0, 1));
            else Graphics.Blit(source, _video);
        }

        private static TextureFormat ReadbackFormat =>
            Application.platform == RuntimePlatform.IPhonePlayer ? TextureFormat.BGRA32 : TextureFormat.RGBA32;

        private void OnFrame(AsyncGPUReadbackRequest request, double pts)
        {
            if (request.hasError || !_encoderReady) return;
            Append(request.GetData<byte>(), pts);
        }

        private unsafe void Append(NativeArray<byte> data, double pts)
        {
            if (pts <= _lastPts && _lastPts > 0) return;
            _lastPts = pts;
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                IosNative._StackItRecAppend((IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data), data.Length, Width * 4, pts);
                return;
            }
            // Android: hand Java a direct ByteBuffer over the readback memory (no 3.7 MB copy per frame).
            IntPtr buffer = AndroidJNI.NewDirectByteBuffer(data);
            var args = new jvalue[2];
            args[0].l = buffer;
            args[1].j = (long)(pts * 1_000_000);
            AndroidJNI.CallStaticVoidMethod(_androidClass, _androidAppend, args);
            AndroidJNI.DeleteLocalRef(buffer);
        }

        private async Task AppendEndCard(EndCard card)
        {
            if (EndCardFactory == null) return;
            using (IEndCardRenderer renderer = EndCardFactory(card))
            {
                // Paced in real time: Android's surface encoder stamps frames on arrival.
                for (int i = 0; i < EndCardFrames; i++)
                {
                    renderer.Render(_video, i / (float)EndCardFrames);
                    AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(_video, 0, ReadbackFormat);
                    request.WaitForCompletion();
                    if (!request.hasError) Append(request.GetData<byte>(), _lastPts + 1.0 / Fps);
                    await Task.Delay(1000 / Fps);
                }
            }
        }

        private bool StartEncoder()
        {
            if (Application.platform == RuntimePlatform.IPhonePlayer)
                return IosNative._StackItRecStart(Width, Height, Fps, Bitrate, ClipSeconds + 3);

            if (_androidClass == IntPtr.Zero)
            {
                IntPtr local = AndroidJNI.FindClass("com/stackit/nativeplugin/VideoRecorder");
                _androidClass = AndroidJNI.NewGlobalRef(local);
                AndroidJNI.DeleteLocalRef(local);
                _androidAppend = AndroidJNI.GetStaticMethodID(_androidClass, "append", "(Ljava/nio/ByteBuffer;J)V");
            }
            return AndroidNative.CallStatic<bool>("VideoRecorder", "start", Width, Height, Fps, Bitrate, ClipSeconds + 3);
        }

        private void EnsureTargets()
        {
            if (_screen == null || _screen.width != Screen.width || _screen.height != Screen.height)
            {
                if (_screen != null) _screen.Release();
                _screen = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            }
            if (_video == null) _video = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        }

        private void OnDestroy()
        {
            _recording = false;
            if (Application.platform == RuntimePlatform.IPhonePlayer) IosNative._StackItRecStop();
            else AndroidNative.CallStatic("VideoRecorder", "stop");
            if (_screen != null) _screen.Release();
            if (_video != null) _video.Release();
        }
    }

    /// <summary>Draws the "Can you beat me?" card into the video target (implemented in UI).</summary>
    public interface IEndCardRenderer : IDisposable
    {
        void Render(RenderTexture target, float progress);
    }
}
