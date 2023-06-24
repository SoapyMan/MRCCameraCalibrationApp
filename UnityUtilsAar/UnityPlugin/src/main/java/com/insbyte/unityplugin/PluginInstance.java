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

    public static void copyFile(String from, String to)
    {
        try {
            Log.d("PluginInstance", "copyFile from " + from);
            Log.d("PluginInstance", "copyFile to " + to);

            Log.d("PluginInstance", "attempting getOutputStream " + to);
            OutputStream out = getOutputStream(to);

            if (out == null) {
                Log.w("PluginInstance", "copyFile - no output stream");
                return;
            }

            Log.d("PluginInstance", "attempting getInputStream " + from);
            InputStream in = getInputStream(from);

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

    public static void deleteFile(String path)
    {
        try {
            Log.d("PluginInstance", "deleteFile " + path);

            DocumentFile file = getAccessToFile(path, false);
            file.delete();
        } catch (Exception e) {
            Log.e("PluginInstance", "deleteFile failed", e);
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

    private static DocumentFile getAccessToFile(String dir, boolean createFolders) {
        Uri dirUri =  getContentFromPath(dir, true);
        List<UriPermission> permissionList = mActivity.getContentResolver().getPersistedUriPermissions();

        // https://stackoverflow.com/questions/46871458/no-permission-to-subfolders-to-write-files-with-documentfile-after-granting-perm
        for(UriPermission p : permissionList) {
            // start process from URI
            if(p.getUri().equals(dirUri)) {
                return DocumentFile.fromTreeUri(mActivity, dirUri);
            }

            // try to find target directory through structure
            String permUriPath = new File(p.getUri().getPath()).getAbsolutePath();
            String dirUriPath = new File(dirUri.getPath()).getAbsolutePath();
            String[] dirList = dirUriPath.substring(permUriPath.length()).substring(1).split("/");

            Log.w("PluginInstance", "permUriPath: " + permUriPath);
            Log.w("PluginInstance", "dirUriPath: " + permUriPath);
            Log.w("PluginInstance", "sub: " + dirUriPath.substring(permUriPath.length()).substring(1));

            DocumentFile currentDir = DocumentFile.fromTreeUri(mActivity, p.getUri());
            for (String dirName : dirList) {
                if (currentDir.findFile(dirName) == null) {
                    if(!createFolders)
                        return null;

                    currentDir.createDirectory(dirName); // Create directory if it doesn't exist
                }
                currentDir = currentDir.findFile(dirName);
            }
            return currentDir;
        }

        // still return a document file directly
        // but it won't have permissions
        return DocumentFile.fromTreeUri(mActivity, dirUri);
    }

    private static InputStream getInputStream(String path) {
        DocumentFile file = getAccessToFile(path, false);

        if (file != null) {
            try {
                return mActivity.getContentResolver().openInputStream(file.getUri());
            } catch (FileNotFoundException e) {
                Log.e("PluginInstance", "getOutputStream createNew failed", e);
            }
        }

        Log.w("PluginInstance", "file was not found: " + path);

        return null;
    }

    private static OutputStream getOutputStream(String path) {
        DocumentFile directory = getAccessToFile(new File(path).getParent(), true);
        String name = new File(path).getName();

        DocumentFile file = directory.findFile(name);
        if (file != null) {
            file.delete();
        }

        try {
            return mActivity.getContentResolver().openOutputStream(directory.createFile("application/octet-stream", name).getUri());
        } catch (FileNotFoundException e) {
            Log.e("PluginInstance", "getOutputStream createNew failed", e);
        }

        return null;
    }
}
