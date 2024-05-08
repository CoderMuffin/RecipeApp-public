package com.codermuffin.recipeapp;

import android.os.Build;
import android.util.Base64;
import android.util.Log;
import android.webkit.JavascriptInterface;
import android.webkit.WebView;

import androidx.annotation.Nullable;
import androidx.annotation.RequiresApi;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CompletableFuture;

public class MuffinComms {
    public static final String TAG = "MuffinComms";
    public interface CommsCallback {
        CompletableFuture<CommsData> apply(Object data) throws CommsError;
    }
    private static class CommsResult {
        final int id;
        @Nullable
        final String data;

        private CommsResult(int id, @Nullable String data) {
            this.id = id;
            this.data = data;
        }
    }

    public static class CommsData {
        private final byte[] bytes;
        private CommsData(byte[] bytes) {
            this.bytes = bytes;
        }
        public static CommsData some(JSONObject json) {
            return new CommsData(json.toString().getBytes(StandardCharsets.UTF_8));
        }
        public static CommsData some(String string) {
            return new CommsData(string.getBytes(StandardCharsets.UTF_8));
        }
        public static CommsData some(byte[] bytes) {
            return new CommsData(bytes);
        }
        public static CommsData empty() {
            return new CommsData(null);
        }
        byte[] data() {
            return bytes;
        }
        boolean has() {
            return bytes != null;
        }
    }

    private static class CommsError extends Exception {
        final String message;

        public CommsError(String message) {
            this.message = message;
        }
    }

    private final Map<String, CommsCallback> callbacks = new HashMap<>();
    private final WebView webView;

    public MuffinComms(WebView webView) {
        this.webView = webView;
        webView.addJavascriptInterface(this, "androidMuffinComms");
    }

    public void addCallback(String message, CommsCallback callback) {
        if (!callbacks.containsKey(message)) {
            callbacks.put(message, callback);
        } else {
            Log.w(TAG, "Duplicate registration of callback for message '" + message + "'. Second handler not registered");
        }
    }

    private void dispatchError(String message) {
        Log.e(TAG, "Error dispatching callback: " + message);
        if (webView != null) {
            String base64Message = Base64.encodeToString(message.getBytes(), Base64.NO_WRAP);
            String script = "console.error('[MuffinComms] Error dispatching callback: ' + MuffinComms._decode('" + base64Message + "'))";
            webView.post(() -> webView.evaluateJavascript(script, s -> {}));
        }
    }

    @RequiresApi(api = Build.VERSION_CODES.N)
    @JavascriptInterface
    public void _dispatch(String message) {
        try {
            JSONObject jsonObject = new JSONObject(message);
            dispatchCallback(jsonObject).thenAcceptAsync(result -> {
                String script = result.data == null ? "MuffinComms._dispatchCallback(" + result.id + ", null)" : "MuffinComms._dispatchCallback(" + result.id + ", MuffinComms._decode('" + result.data + "'))";
                webView.post(() -> webView.evaluateJavascript(script, s -> {}));
            });
        } catch (JSONException e) {
            dispatchError("Malformed message body");
        } catch (CommsError e) {
            dispatchError(e.message);
        }
    }

    @RequiresApi(api = Build.VERSION_CODES.N)
    private CompletableFuture<CommsResult> dispatchCallback(JSONObject json) throws CommsError {
        try {
            if (!json.has("message") || !json.has("data") || !json.has("id")) {
                throw new CommsError("Malformed request");
            }

            String jsonMessage = json.getString("message");
            Object jsonData = json.get("data");
            int jsonId = json.getInt("id");

            CommsCallback callback = callbacks.get(jsonMessage);
            if (callback == null) {
                throw new CommsError("Missing callback '" + jsonMessage + "'");
            }

            CompletableFuture<CommsData> commsResult = callback.apply(jsonData);

            return commsResult.thenApply(commsData -> new CommsResult(
                    jsonId,
                    commsData.has() ? Base64.encodeToString(commsData.data(), Base64.NO_WRAP) : null
            ));
        } catch (JSONException e) {
            throw new CommsError("Could not parse JSON '" + json + "'");
        }
    }

    public static byte[] deserializeBytes(JSONObject json, String field) throws JSONException {
        return Base64.decode(json.getString(field), Base64.DEFAULT);
    }
    public static byte[] deserializeBytes(JSONArray json, int index) throws JSONException {
        return Base64.decode(json.getString(index), Base64.DEFAULT);
    }
    public static String serialize(byte[] bytes) {
        return Base64.encodeToString(bytes, Base64.NO_WRAP);
    }
}
