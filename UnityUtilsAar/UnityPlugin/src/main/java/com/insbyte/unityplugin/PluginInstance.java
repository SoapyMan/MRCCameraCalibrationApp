package com.insbyte.unityplugin;

import android.app.Activity;
import android.content.Intent;
import android.content.UriPermission;
import android.net.Uri;
import android.os.Environment;
import android.provider.DocumentsContract;
import android.util.Log;
import androidx.documentfile.provider.DocumentFile;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.List;

public class PluginInstance
{
    public static Activity mActivity;

    public static void initialize(final Activity activity)
    {
        mActivity = activity;
    }

    public static boolean hasAccessToFolder(String path)
    {
        String checkUri = getContentFromPath(path, true).toString();
        List<UriPermission> permissionList = mActivity.getContentResolver().getPersistedUriPermissions();

        Log.d("PluginInstance", "checkUri LARG: " + checkUri);
        for(UriPermission p : permissionList)
        {
            if (checkUri.equals(p.getUri().toString()))
                return true;
        }
        return false;
    }

    public static void releaseAllPermissions()
    {
        int flags = 0;
        flags |= Intent.FLAG_GRANT_READ_URI_PERMISSION;
        flags |= Intent.FLAG_GRANT_WRITE_URI_PERMISSION;

        List<UriPermission> permissionList = mActivity.getContentResolver().getPersistedUriPermissions();

        for(UriPermission p : permissionList) {
            mActivity.getContentResolver().releasePersistableUriPermission(p.getUri(), flags);
        }
    }

    public static void askForAccess(String path)
    {
        if(DeviceActivity.isOpen) {
            return;
        }

        int flags = Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION;

        if(hasAccessToFolder(path)) {
            // revoke old?
            try {
                mActivity.getContentResolver().releasePersistableUriPermission(getContentFromPath(path, true), flags);
            } catch (Exception e) {
                Log.e("PluginInstance", "askForAccess", e);
            }
        }

        try {
            DeviceActivity.isOpen = true;
            DeviceActivity.selectedPath = getContentFromPath(path, false);
            DeviceActivity.flags = flags;

            mActivity.runOnUiThread(() -> {
                Intent intent = new Intent();
                intent.setAction(Intent.ACTION_SEND);
                intent.setClass(mActivity, DeviceActivity.class);
                mActivity.startActivity(intent);
            });
        } catch (Exception e) {
            Log.e("PluginInstance", "askForAccess failed", e);
        }
    }

    public static void writeFile(String contents, String to)
    {
        try {
            Log.d("PluginInstance", "writeFile to " + to);

            List<UriPermission> permissionList = mActivity.getContentResolver().getPersistedUriPermissions();

            Log.d("PluginInstance", "Perm check before copy:");
            for (UriPermission p : permissionList) {
                Log.d("PluginInstance", " ---  " + p.toString());
            }

            Log.d("PluginInstance", "attempting getOutputStream " + to);
            OutputStream out = getOutputStream(to);

            if (out == null) {
                Log.w("PluginInstance", "writeFile - no output stream");
                return;
            }

            out.write(contents.getBytes(StandardCharsets.UTF_8));

            out.flush();
            out.close();
        } catch (Exception e) {
            Log.e("PluginInstance", "writeFile failed", e);
        }
    }

    public static void copyFile(String from, String to)
    {
        try {
            Log.d("PluginInstance", "copyFile from " + from);
            Log.d("PluginInstance", "copyFile to " + to);

            List<UriPermission> permissionList = mActivity.getContentResolver().getPersistedUriPermissions();

            Log.d("PluginInstance", "Perm check before copy:");
            for (UriPermission p : permissionList) {
                Log.d("PluginInstance", " ---  " + p.toString());
            }

            Log.d("PluginInstance", "attempting getOutputStream " + to);
            OutputStream out = getOutputStream(to);

            if (out == null) {
                Log.w("PluginInstance", "copyFile - no output stream");
                return;
            }

            Log.d("PluginInstance", "attempting openInputStream " + from);
            InputStream in = mActivity.getContentResolver().openInputStream(getContentFromPath(from, true));

            byte[] buffer = new byte[1024];
            int read;
            while ((read = in.read(buffer)) != -1) {
                out.write(buffer, 0, read);
            }
            out.flush();
            out.close();
            in.close();
        } catch (Exception e) {
            Log.e("PluginInstance", "copyFile failed", e);
        }
    }

    private static Uri getContentFromPath(String path, boolean tree)
    {
        String suffix = path;
        String absPath = Environment.getExternalStorageDirectory().getAbsolutePath();
        if(suffix.startsWith(absPath)) {
            suffix = path.substring(absPath.length());
        }
        if (suffix.length() < 1) {
            suffix = "/";
        }

        String documentId = "primary:" + suffix.substring(1);
        if(tree)
            return DocumentsContract.buildTreeDocumentUri("com.android.externalstorage.documents", documentId);
        else
            return DocumentsContract.buildDocumentUri("com.android.externalstorage.documents", documentId);
    }

    private static DocumentFile getAccessToFile(String dir) {
        DocumentFile startDir = DocumentFile.fromTreeUri(mActivity, getContentFromPath(dir, true));

        if(startDir != null) {
            return startDir;
        }

        // try create folder structure
        String diff = dir.replace(dir, "");
        if (diff.startsWith("/")) {
            diff = diff.substring(1);
        }
        String[] dirs = diff.split("/");

        DocumentFile currentDir = startDir;
        for (String dirName : dirs) {
            if(dirName.equals("")) {
                continue;
            }
            if (currentDir.findFile(dirName) == null) {
                currentDir.createDirectory(dirName); // Create directory if it doesn't exist
            }
            currentDir = currentDir.findFile(dirName);
        }
        return currentDir;
    }

    private static OutputStream getOutputStream(String path) {
        DocumentFile directory = getAccessToFile(new File(path).getParent());
        String name = new File(path).getName();

        DocumentFile file = directory.findFile(name);
        if (file == null) {
            try {
                Uri fileUri = DocumentsContract.createDocument(mActivity.getContentResolver(), directory.getUri(), "application/octet-stream", name);
                return mActivity.getContentResolver().openOutputStream(fileUri);
            } catch (FileNotFoundException e) {
                Log.e("PluginInstance", "getOutputStream createNew failed", e);
            }
        } else {
            try {
                Uri fileUri = file.getUri();
                // file.delete()
                OutputStream stream = mActivity.getContentResolver().openOutputStream(fileUri);

                return stream;
            } catch (FileNotFoundException e) {
                Log.e("PluginInstance", "getOutputStream overwrite failed", e);
            }
        }


        return null;
    }
}
