package com.codermuffin.recipeapp

import android.Manifest
import android.annotation.SuppressLint
import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.provider.OpenableColumns
import android.util.Log
import android.webkit.MimeTypeMap
import android.webkit.WebView
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.result.ActivityResultLauncher
import androidx.activity.result.contract.ActivityResultContracts
import androidx.annotation.RequiresApi
import androidx.core.content.FileProvider
import org.json.JSONObject
import java.io.ByteArrayOutputStream
import java.io.File
import java.io.FileOutputStream
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.concurrent.CompletableFuture


class MainActivity : ComponentActivity() {
    private lateinit var wv: WebView
    private val TAG = "CM->MainActivity"
    private lateinit var filePickerResult: CompletableFuture<JSONObject?>
    private lateinit var filePickerPermissionLauncher: ActivityResultLauncher<String>
    private lateinit var filePickerLauncher: ActivityResultLauncher<Intent>
    @SuppressLint("SetJavaScriptEnabled")
    @RequiresApi(Build.VERSION_CODES.N)
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        filePickerPermissionLauncher = registerForActivityResult(ActivityResultContracts.RequestPermission()) { isGranted ->
            if (isGranted) {
                launchFilePicker()
            } else {
                toastLong("Permission denied, cannot upload file")
            }
        }

        filePickerLauncher = registerForActivityResult(ActivityResultContracts.StartActivityForResult()) { result ->
            if (result.resultCode == Activity.RESULT_OK) {
                val intent = result.data
                intent?.data?.let {
                    val json = JSONObject()
                    json.put("name", getFileName(it))
                    json.put("file", MuffinComms.serialize(readFileContent(it)))
                    filePickerResult.complete(json)
                } ?: filePickerResult.complete(null)
            } else {
                filePickerResult.complete(null)
            }
        }

        WebView.setWebContentsDebuggingEnabled(true)
        wv = findViewById(R.id.webView)

        wv.settings.javaScriptEnabled = true
        wv.settings.setSupportZoom(false)
        wv.settings.databaseEnabled = true
        wv.settings.domStorageEnabled = true
        wv.settings.allowFileAccessFromFileURLs = true //i do not care :D

        WebView.setWebContentsDebuggingEnabled(true)
        addComms()
        //load file
        wv.loadUrl("file:///android_asset/index.html")
    }

    @RequiresApi(Build.VERSION_CODES.N)
    private fun addComms() {
        val comms = MuffinComms(wv)

        comms.addCallback("shareFile") {
            CompletableFuture.supplyAsync {
                try {
                    it as JSONObject
                    val data = MuffinComms.deserializeBytes(it, "bytes")
                    val mimeType = it.getString("ct")

                    val file = downloadFile(data, mimeType, this.cacheDir)

                    val intent = Intent(Intent.ACTION_SEND)
                    val uri = FileProvider.getUriForFile(this, this.packageName + ".fileprovider", file)
                    intent.type = mimeType
                    intent.putExtra(Intent.EXTRA_STREAM, uri)
                    intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)

                    try {
                        startActivity(Intent.createChooser(intent, "Share File"))
                    } catch (e: ActivityNotFoundException) {
                        toast("No application found to share the file")
                    }
                } catch (e: Exception) {
                    e.printStackTrace()
                    toast("Error sharing file")
                }
                MuffinComms.CommsData.empty()
            }
        }

        comms.addCallback("saveFile") {
            CompletableFuture.supplyAsync {
                try {
                    it as JSONObject
                    val data = MuffinComms.deserializeBytes(it, "bytes")
                    val mimeType = it.getString("ct")

                    val file = downloadFile(data, mimeType, Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS))
                    toast("File downloaded successfully")
                    openDownloadedFile(file, mimeType)
                } catch (e: Exception) {
                    e.printStackTrace()
                    toast("Error downloading file")
                }
                MuffinComms.CommsData.empty()
            }
        }

        comms.addCallback("inputFile") {
            try {
                filePickerResult = CompletableFuture()
                if (checkSelfPermission(Manifest.permission.READ_EXTERNAL_STORAGE)
                    == PackageManager.PERMISSION_GRANTED
                ) {
                    launchFilePicker()
                } else {
                    filePickerPermissionLauncher.launch(Manifest.permission.READ_EXTERNAL_STORAGE)
                }
                filePickerResult.thenApply {
                    if (it == null) MuffinComms.CommsData.empty() else MuffinComms.CommsData.some(it)
                }
            } catch (e: Exception) {
                e.printStackTrace()
                toast("Error fetching file")
                CompletableFuture.completedFuture(MuffinComms.CommsData.empty())
            }
        }

        comms.addCallback("camera") {
            val future = CompletableFuture<MuffinComms.CommsData>()
            try {
                val imageUri = FileProvider.getUriForFile(
                    this,
                    "$packageName.fileprovider",
                    File.createTempFile("camera_image", ".jpg", cacheDir)
                )
                val takePicture = registerForActivityResult(ActivityResultContracts.TakePicture()) { success ->
                    if (success) {
                        future.complete(MuffinComms.CommsData.some(readFileContent(imageUri)))
                    } else {
                        toast("Cancelled")
                        future.complete(MuffinComms.CommsData.empty())
                    }
                }

                takePicture.launch(imageUri)
            } catch (e: Exception) {
                e.printStackTrace()
                toast("Error acquiring image")
                future.complete(MuffinComms.CommsData.empty())
            }
            future
        }

        comms.addCallback("echo") {
            CompletableFuture.completedFuture(MuffinComms.CommsData.some(it as String))
        }
    }
    private fun toast(text: String) {
        runOnUiThread {
            Toast.makeText(this, text, Toast.LENGTH_SHORT).show()
        }
    }
    private fun toastLong(text: String) {
        runOnUiThread {
            Toast.makeText(this, text, Toast.LENGTH_LONG).show()
        }
    }
    private fun readFileContent(uri: Uri): ByteArray {
        val inputStream = contentResolver.openInputStream(uri)
        val outputStream = ByteArrayOutputStream()

        inputStream?.use { input ->
            outputStream.use { output ->
                input.copyTo(output)
            }
        }

        return outputStream.toByteArray()
    }
    private fun getFileName(uri: Uri): String {
        contentResolver.query(uri, null, null, null, null)?.use { cursor ->
            val nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
            cursor.moveToFirst()
            return cursor.getString(nameIndex)
        }
        return ""
    }
    private fun launchFilePicker() {
        val intent = Intent(Intent.ACTION_GET_CONTENT)
        intent.type = "*/*"
        filePickerLauncher.launch(intent)
    }
    private fun downloadFile(data: ByteArray, mimeType: String, destinationFolder: File): File {
        val extension = getExtensionFromMimeType(mimeType)

        val dateFormat = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault())
        val timestamp = dateFormat.format(Date())
        val filename = "download_" + timestamp + if (extension != null) ".$extension" else ""

        val file = File(destinationFolder, filename)
        val fos = FileOutputStream(file)
        fos.write(data)
        fos.close()

        return file
    }
    private fun openDownloadedFile(file: File, mimeType: String) {
        val intent = Intent(Intent.ACTION_VIEW)
        val uri = FileProvider.getUriForFile(
            this,
            this.packageName + ".fileprovider",
            file
        )
        intent.setDataAndType(uri, mimeType)
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        try {
            this.startActivity(intent)
        } catch (e: ActivityNotFoundException) {
            Log.w("CM->WAI", "No app found to open the file")
        }
    }

    private fun getExtensionFromMimeType(mimeType: String?): String? {
        if (mimeType == null) {
            return null
        }
        return MimeTypeMap.getSingleton().getExtensionFromMimeType(mimeType)?.lowercase(Locale.getDefault())
    }
}
