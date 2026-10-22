package com.stackit.nativeplugin;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;

import androidx.core.content.FileProvider;

import java.io.File;

/** Native share sheet (ACTION_SEND). Videos are shared through a FileProvider content:// URI. */
public final class ShareBridge {
    private ShareBridge() {}

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
