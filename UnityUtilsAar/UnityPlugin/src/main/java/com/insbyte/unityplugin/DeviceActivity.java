package com.insbyte.unityplugin;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.util.Log;

public class DeviceActivity extends Activity {
    public static final int OPEN_DIRECTORY_REQUEST_CODE = 1; // Arbitrary request code
    private static final String LOGTAG = "DeviceActivity";

    public static Uri selectedPath;
    public static int flags;
    public static boolean isOpen = false;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        // setContentView(R.layout.activity_device);
    }

    @Override
    protected void onPostResume() {
        super.onPostResume();
        Log.i(LOGTAG, "onResume");

        try
        {
            Log.i(LOGTAG, "making sub intent");
            Intent subIntent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
            subIntent.putExtra("android.provider.extra.INITIAL_URI", selectedPath);

            Log.i(LOGTAG, "starting sub intent request");
            startActivityForResult(subIntent, OPEN_DIRECTORY_REQUEST_CODE);

        }
        catch (Exception e)
        {
            Log.i(LOGTAG,"error: " + e.getLocalizedMessage());
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (resultCode == RESULT_OK && requestCode == OPEN_DIRECTORY_REQUEST_CODE && data != null) {
            Log.i(LOGTAG,"onActivityResult");
            try {
                Log.d("PluginInstance", "grantUriPermission check");
                PluginInstance.mActivity.grantUriPermission(PluginInstance.mActivity.getPackageName(), data.getData(), flags);

            } catch (Exception e) {
                Log.w(LOGTAG, "onActivityResult", e);
            }
            try {
                PluginInstance.mActivity.getContentResolver().takePersistableUriPermission(data.getData(), flags);

                Log.d("PluginInstance", "grantUriPermission check 2");
                PluginInstance.mActivity.grantUriPermission(PluginInstance.mActivity.getPackageName(), data.getData(), flags);

            } catch (Exception e) {
                Log.e(LOGTAG, "onActivityResult", e);
            }
        }

        isOpen = false;

        super.onActivityResult(requestCode, resultCode, data);

        finish();
    }
}