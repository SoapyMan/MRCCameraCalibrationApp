package com.insbyte.unityplugin;

import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.DocumentsContract;
import android.util.Log;

import androidx.activity.ComponentActivity;
import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

public class DeviceActivity extends ComponentActivity {
    private static final String LOGTAG = "DeviceActivity";

    public static Uri selectedPath;
    public static int flags;
    public static boolean isOpen = false;

    private final ActivityResultLauncher<Uri> mDirRequest = registerForActivityResult(
            new ActivityResultContracts.OpenDocumentTree() {
                @NonNull
                @Override
                public Intent createIntent (@NonNull Context context, @Nullable Uri input) {
                    super.createIntent(context, input);
                    Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && input != null) {
                        intent.putExtra(DocumentsContract.EXTRA_INITIAL_URI, input);
                    }
                    Log.i(LOGTAG, "createIntent logic");
                    return intent;
                }
            },
            uri -> {
                Log.i(LOGTAG, "onActivityResult: " + uri);
                if (uri != null) {
                    // call this to persist permission across device reboots
                    getContentResolver().takePersistableUriPermission(uri, flags);
                    // do your stuff
                } else {
                    // request denied by user
                }
                isOpen = false;
                finish();
            }
    );

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        // setContentView(R.layout.activity_device);
    }

    @Override
    protected void onPostResume() {
        super.onPostResume();
        Log.i(LOGTAG, "onResume");

        mDirRequest.launch(selectedPath);
        //finish();
    }
}