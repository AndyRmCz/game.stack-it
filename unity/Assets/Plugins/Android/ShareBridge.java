package com.stackit.nativeplugin;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;

import androidx.core.content.FileProvider;

import java.io.File;

/**
 * Native share sheet (ACTION_SEND), plus direct shares to one app (H5). Videos are shared through
 * a FileProvider content:// URI. A direct share whose app isn't installed falls back to the sheet.
 */
public final class ShareBridge {
    private static final String INSTAGRAM = "com.instagram.android";
    // TikTok ships under two package names depending on the region.
    private static final String[] TIKTOK = { "com.zhiliaoapp.musically", "com.ss.android.ugc.trill" };

    private ShareBridge() {}

    /** Straight into TikTok's own share flow with the clip and the text (no SDK: no auto hashtag). */
    public static void shareToTikTok(final Activity activity, final String text, final String videoPath) {
        activity.runOnUiThread(() -> {
            Uri uri = videoUri(activity, videoPath);
            for (String pkg : TIKTOK) {
                Intent intent = new Intent(Intent.ACTION_SEND);
                intent.setPackage(pkg);
                intent.putExtra(Intent.EXTRA_TEXT, text);
                attach(intent, uri);
                try {
                    activity.startActivity(intent);
                    return;
                } catch (ActivityNotFoundException ignored) {
                    // try the other package, then the sheet
                }
            }
            share(activity, text, videoPath);
        });
    }

    /**
     * Instagram's official "Sharing to Stories" intent: the clip as the story's background video.
     * Needs the Facebook App ID registered for this app (source_application).
     */
    public static void shareToInstagramStory(final Activity activity, final String facebookAppId, final String text,
                                             final String videoPath) {
        activity.runOnUiThread(() -> {
            Uri uri = videoUri(activity, videoPath);
            if (uri == null || facebookAppId == null || facebookAppId.isEmpty()) {
                share(activity, text, videoPath);
                return;
            }
            Intent intent = new Intent("com.instagram.share.ADD_TO_STORY");
            intent.putExtra("source_application", facebookAppId);
            intent.setDataAndType(uri, "video/mp4");
            intent.setPackage(INSTAGRAM);
            intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            activity.grantUriPermission(INSTAGRAM, uri, Intent.FLAG_GRANT_READ_URI_PERMISSION);
            try {
                activity.startActivity(intent);
            } catch (ActivityNotFoundException e) {
                share(activity, text, videoPath);
            }
        });
    }

    private static Uri videoUri(Activity activity, String videoPath) {
        File video = videoPath == null || videoPath.isEmpty() ? null : new File(videoPath);
        if (video == null || !video.exists()) return null;
        return FileProvider.getUriForFile(activity, activity.getPackageName() + ".stackit.fileprovider", video);
    }

    private static void attach(Intent intent, Uri uri) {
        if (uri == null) {
            intent.setType("text/plain");
            return;
        }
        intent.setType("video/mp4");
        intent.putExtra(Intent.EXTRA_STREAM, uri);
        intent.setClipData(ClipData.newRawUri("", uri));
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
    }

    public static void share(final Activity activity, final String text, final String videoPath) {
        activity.runOnUiThread(() -> {
            Intent intent = new Intent(Intent.ACTION_SEND);
            intent.putExtra(Intent.EXTRA_TEXT, text);
            File video = videoPath == null || videoPath.isEmpty() ? null : new File(videoPath);
            if (video != null && video.exists()) {
                String authority = activity.getPackageName() + ".stackit.fileprovider";
                Uri uri = FileProvider.getUriForFile(activity, authority, video);
                intent.setType("video/mp4");
                intent.putExtra(Intent.EXTRA_STREAM, uri);
                intent.setClipData(ClipData.newRawUri("", uri));
                intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            } else {
                intent.setType("text/plain");
            }
            activity.startActivity(Intent.createChooser(intent, null));
        });
    }
}
