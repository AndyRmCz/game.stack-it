package com.stackit.nativeplugin;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;

import androidx.core.content.FileProvider;

import java.io.File;
import java.util.Locale;

/**
 * Native share sheet (ACTION_SEND), plus direct shares to one app (H5). The attached file (the
 * challenge card, a .png) is shared through a FileProvider content:// URI, typed by its extension.
 * A direct share whose app isn't installed falls back to the sheet.
 */
public final class ShareBridge {
    private static final String INSTAGRAM = "com.instagram.android";
    // TikTok ships under two package names depending on the region.
    private static final String[] TIKTOK = { "com.zhiliaoapp.musically", "com.ss.android.ugc.trill" };

    private ShareBridge() {}

    /** Straight into TikTok's own share flow with the card and the text (no SDK: no auto hashtag). */
    public static void shareToTikTok(final Activity activity, final String text, final String filePath) {
        activity.runOnUiThread(() -> {
            Uri uri = fileUri(activity, filePath);
            for (String pkg : TIKTOK) {
                Intent intent = new Intent(Intent.ACTION_SEND);
                intent.setPackage(pkg);
                intent.putExtra(Intent.EXTRA_TEXT, text);
                attach(intent, uri, filePath);
                try {
                    activity.startActivity(intent);
                    return;
                } catch (ActivityNotFoundException ignored) {
                    // try the other package, then the sheet
                }
            }
            share(activity, text, filePath);
        });
    }

    /**
     * Instagram's official "Sharing to Stories" intent: the card as the story's background.
     * Needs the Facebook App ID registered for this app (source_application).
     */
    public static void shareToInstagramStory(final Activity activity, final String facebookAppId, final String text,
                                             final String filePath) {
        activity.runOnUiThread(() -> {
            Uri uri = fileUri(activity, filePath);
            if (uri == null || facebookAppId == null || facebookAppId.isEmpty()) {
                share(activity, text, filePath);
                return;
            }
            Intent intent = new Intent("com.instagram.share.ADD_TO_STORY");
            intent.putExtra("source_application", facebookAppId);
            intent.setDataAndType(uri, mimeType(filePath));
            intent.setPackage(INSTAGRAM);
            intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            activity.grantUriPermission(INSTAGRAM, uri, Intent.FLAG_GRANT_READ_URI_PERMISSION);
            try {
                activity.startActivity(intent);
            } catch (ActivityNotFoundException e) {
                share(activity, text, filePath);
            }
        });
    }

    private static Uri fileUri(Activity activity, String filePath) {
        File file = filePath == null || filePath.isEmpty() ? null : new File(filePath);
        if (file == null || !file.exists()) return null;
        return FileProvider.getUriForFile(activity, activity.getPackageName() + ".stackit.fileprovider", file);
    }

    /** image/png for the challenge card; anything else is sent untyped. */
    static String mimeType(String path) {
        return path != null && path.toLowerCase(Locale.ROOT).endsWith(".png") ? "image/png" : "application/octet-stream";
    }

    private static void attach(Intent intent, Uri uri, String path) {
        if (uri == null) {
            intent.setType("text/plain");
            return;
        }
        intent.setType(mimeType(path));
        intent.putExtra(Intent.EXTRA_STREAM, uri);
        intent.setClipData(ClipData.newRawUri("", uri));
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
    }

    public static void share(final Activity activity, final String text, final String filePath) {
        activity.runOnUiThread(() -> {
            Intent intent = new Intent(Intent.ACTION_SEND);
            intent.putExtra(Intent.EXTRA_TEXT, text);
            File file = filePath == null || filePath.isEmpty() ? null : new File(filePath);
            if (file != null && file.exists()) {
                String authority = activity.getPackageName() + ".stackit.fileprovider";
                Uri uri = FileProvider.getUriForFile(activity, authority, file);
                intent.setType(mimeType(filePath));
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
