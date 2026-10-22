package com.stackit.nativeplugin;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.util.Log;
import android.view.Surface;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.nio.ByteBuffer;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.List;

/**
 * Replay buffer: RGBA frames from Unity are drawn onto a MediaCodec input surface (the GPU does
 * the colour conversion), encoded to H.264 in real time, and only the last N seconds of
 * compressed samples are kept. export() muxes them into an MP4 without re-encoding.
 * Contract: Assets/_Project/Scripts/Services/Native/RecorderService.cs.
 */
public final class VideoRecorder {
    private static final String TAG = "StackItRecorder";
    private static final Object LOCK = new Object();
    private static final ArrayDeque<Sample> SAMPLES = new ArrayDeque<>();

    private static MediaCodec codec;
    private static Surface input;
    private static MediaFormat outputFormat;
    private static Bitmap frame;
    private static Thread drainer;
    private static volatile boolean running;
    private static long keepUs;

    private static final class Sample {
        final ByteBuffer data;
        final long ptsUs;
        final int flags;

        Sample(ByteBuffer data, long ptsUs, int flags) {
            this.data = data;
            this.ptsUs = ptsUs;
            this.flags = flags;
        }

        boolean isKeyFrame() {
            return (flags & MediaCodec.BUFFER_FLAG_KEY_FRAME) != 0;
        }
    }

    private VideoRecorder() {}

    public static synchronized boolean start(int width, int height, int fps, int bitrate, int keepSeconds) {
        stop();
        try {
            MediaFormat format = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, width, height);
            format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface);
            format.setInteger(MediaFormat.KEY_BIT_RATE, bitrate);
            format.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
            format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);
            codec = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC);
            codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            input = codec.createInputSurface();
            codec.start();
            frame = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
            keepUs = keepSeconds * 1_000_000L;
            running = true;
            drainer = new Thread(VideoRecorder::drain, "StackItRecorderDrain");
            drainer.start();
            return true;
        } catch (Exception e) {
            Log.w(TAG, "start failed", e);
            stop();
            return false;
        }
    }

    /** Called from Unity's main thread with a direct buffer over the GPU readback (valid only during the call). */
    public static void append(ByteBuffer rgba, long ptsUs) {
        if (!running || input == null) return;
        try {
            rgba.rewind();
            frame.copyPixelsFromBuffer(rgba); // ARGB_8888 memory layout is R,G,B,A bytes
            Canvas canvas = input.lockHardwareCanvas();
            try {
                canvas.drawBitmap(frame, 0, 0, null);
            } finally {
                input.unlockCanvasAndPost(canvas);
            }
        } catch (Exception e) {
            Log.w(TAG, "append failed", e);
        }
    }

    private static void drain() {
        MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
        while (running) {
            int index;
            try {
                index = codec.dequeueOutputBuffer(info, 10_000);
            } catch (IllegalStateException e) {
                return;
            }
            if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                synchronized (LOCK) {
                    outputFormat = codec.getOutputFormat();
                }
            } else if (index >= 0) {
                ByteBuffer buffer = codec.getOutputBuffer(index);
                boolean config = (info.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0;
                if (buffer != null && !config && info.size > 0) {
                    ByteBuffer copy = ByteBuffer.allocateDirect(info.size);
                    buffer.position(info.offset);
                    buffer.limit(info.offset + info.size);
                    copy.put(buffer);
                    copy.flip();
                    synchronized (LOCK) {
                        SAMPLES.addLast(new Sample(copy, info.presentationTimeUs, info.flags));
                        trim(SAMPLES, keepUs);
                    }
                }
                codec.releaseOutputBuffer(index, false);
            }
        }
    }

    /** Drops samples older than the window, then leading non-keyframes (a clip must start on one). */
    private static void trim(ArrayDeque<Sample> samples, long windowUs) {
        if (samples.isEmpty()) return;
        long newest = samples.peekLast().ptsUs;
        while (samples.size() > 1 && newest - samples.peekFirst().ptsUs > windowUs) samples.removeFirst();
        while (!samples.isEmpty() && !samples.peekFirst().isKeyFrame()) samples.removeFirst();
    }

    /** Writes the last {@code lastSeconds} to an MP4 and reports the path (or "error:...") to Unity. */
    public static void export(final String path, final double lastSeconds) {
        new Thread(() -> {
            String result;
            try {
                Thread.sleep(300); // let the encoder flush the frames appended just before
                ArrayDeque<Sample> clip;
                MediaFormat format;
                synchronized (LOCK) {
                    clip = new ArrayDeque<>(SAMPLES);
                    format = outputFormat;
                }
                trim(clip, (long) (lastSeconds * 1_000_000L));
                if (clip.isEmpty() || format == null) throw new IllegalStateException("empty");

                new File(path).delete();
                MediaMuxer muxer = new MediaMuxer(path, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
                int track = muxer.addTrack(format);
                muxer.start();
                long base = clip.peekFirst().ptsUs;
                MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
                List<Sample> ordered = new ArrayList<>(clip);
                for (Sample s : ordered) {
                    ByteBuffer data = s.data.duplicate();
                    info.set(0, data.remaining(), s.ptsUs - base, s.flags);
                    muxer.writeSampleData(track, data, info);
                }
                muxer.stop();
                muxer.release();
                result = path;
            } catch (Exception e) {
                Log.w(TAG, "export failed", e);
                result = "error:" + e.getMessage();
            }
            UnityPlayer.UnitySendMessage("StackItNative", "OnClipExported", result);
        }, "StackItRecorderExport").start();
    }

    public static synchronized void stop() {
        running = false;
        if (drainer != null) {
            try {
                drainer.join(500);
            } catch (InterruptedException ignored) {
                Thread.currentThread().interrupt();
            }
            drainer = null;
        }
        if (codec != null) {
            try {
                codec.stop();
            } catch (Exception ignored) {
                // already stopped
            }
            codec.release();
            codec = null;
        }
        if (input != null) {
            input.release();
            input = null;
        }
        synchronized (LOCK) {
            SAMPLES.clear();
            outputFormat = null;
        }
    }
}
