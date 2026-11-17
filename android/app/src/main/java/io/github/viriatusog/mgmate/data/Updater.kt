package io.github.viriatusog.mgmate.data

import android.content.Context
import android.content.Intent
import androidx.core.content.FileProvider
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.io.IOException
import java.util.concurrent.TimeUnit

/** The newest published build, as the release's version.json describes it. */
@Serializable
data class PublishedBuild(val versionCode: Int, val versionName: String)

/**
 * Finds, downloads and hands over a newer build of the app. Builds are published on the project's
 * GitHub releases page, on the rolling "android-latest" release, as mgmate.apk with a version.json
 * beside it. This is the one thing in the app that talks to GitHub, and it sends nothing but the
 * request itself: it has its own HTTP client, so the server's session cookie never travels along.
 */
class Updater(private val context: Context) {
    private val json = Json { ignoreUnknownKeys = true }
    private val http = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .build()

    /** The newest published build. Throws [ApiException] with a reason to show. */
    suspend fun latest(): PublishedBuild = withContext(Dispatchers.IO) {
        try {
            http.newCall(Request.Builder().url("$RELEASE/version.json").build()).execute().use { response ->
                if (!response.isSuccessful) throw ApiException("No published build found yet.", response.code)
                json.decodeFromString<PublishedBuild>(response.body?.string().orEmpty())
            }
        } catch (e: IOException) {
            throw ApiException("Cannot reach GitHub to look for an update.")
        } catch (e: kotlinx.serialization.SerializationException) {
            throw ApiException("The update information could not be read.")
        }
    }

    /** Downloads the newest APK, reporting progress from 0 to 1 when the size is known. */
    suspend fun download(onProgress: (Float?) -> Unit): File = withContext(Dispatchers.IO) {
        val folder = File(context.cacheDir, FOLDER).apply { mkdirs() }
        val target = File(folder, "mgmate.apk")
        val partial = File(folder, "mgmate.apk.part")
        try {
            http.newCall(Request.Builder().url("$RELEASE/mgmate.apk").build()).execute().use { response ->
                val body = response.body
                if (!response.isSuccessful || body == null) throw ApiException("The update could not be downloaded.", response.code)
                val total = body.contentLength()
                var done = 0L
                body.byteStream().use { input ->
                    partial.outputStream().use { output ->
                        val buffer = ByteArray(64 * 1024)
                        while (true) {
                            val read = input.read(buffer)
                            if (read < 0) break
                            output.write(buffer, 0, read)
                            done += read
                            onProgress(if (total > 0) done.toFloat() / total else null)
                        }
                    }
                }
            }
            target.delete()
            if (!partial.renameTo(target)) throw ApiException("The update could not be saved.")
            target
        } catch (e: IOException) {
            partial.delete()
            throw ApiException("The download stopped. Check your connection and try again.")
        }
    }

    /** Opens Android's installer on a downloaded APK. Android asks the owner before it installs. */
    fun install(apk: File) {
        val uri = FileProvider.getUriForFile(context, "${context.packageName}.updates", apk)
        val intent = Intent(Intent.ACTION_VIEW)
            .setDataAndType(uri, "application/vnd.android.package-archive")
            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
        context.startActivity(intent)
    }

    companion object {
        const val RELEASES_PAGE = "https://github.com/ViriatusOG/MGMate/releases/tag/android-latest"
        private const val RELEASE = "https://github.com/ViriatusOG/MGMate/releases/download/android-latest"
        private const val FOLDER = "updates"
    }
}
