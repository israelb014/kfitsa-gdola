package com.ibfix.kfitsagdola;

import android.view.KeyEvent;
import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerGameActivity;

/** Sends the system Back button to the game instead of closing the app. */
public class GameMain extends UnityPlayerGameActivity {
    @Override
    public boolean dispatchKeyEvent(KeyEvent event) {
        // Caught here because GameActivity's text input swallows the first Back after launch
        if (event.getKeyCode() == KeyEvent.KEYCODE_BACK) {
            if (event.getAction() == KeyEvent.ACTION_UP && !event.isCanceled()) sendBack();
            return true;
        }
        return super.dispatchKeyEvent(event);
    }

    @Override
    public void onBackPressed() {
        sendBack();
    }

    private void sendBack() {
        UnityPlayer.UnitySendMessage("App", "OnSystemBack", "");
    }
}
