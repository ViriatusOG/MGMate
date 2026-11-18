package io.github.viriatusog.mgmate.data

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys

/**
 * What the app remembers between launches: the server's address, the sign-in and the session
 * cookie. Everything is encrypted with a key held in the Android Keystore, so it cannot be read
 * off the phone's storage or restored onto another device.
 *
 * The password is kept so the app can sign in again by itself when the server's 30-day session
 * runs out. The fingerprint lock is what guards the app from then on.
 */
/** Whether the app is dark, light, or follows the phone. */
enum class ThemeMode { System, Light, Dark }

class Session(context: Context) {
    private val prefs: SharedPreferences = open(context.applicationContext)

    var serverUrl: String?
        get() = prefs.getString(KEY_SERVER, null)
        set(value) = prefs.edit().putString(KEY_SERVER, value).apply()

    var username: String?
        get() = prefs.getString(KEY_USER, null)
        set(value) = prefs.edit().putString(KEY_USER, value).apply()

    var password: String?
        get() = prefs.getString(KEY_PASSWORD, null)
        set(value) = prefs.edit().putString(KEY_PASSWORD, value).apply()

    /** The session cookie as "name=value", or null when signed out. */
    var cookie: String?
        get() = prefs.getString(KEY_COOKIE, null)
        set(value) = prefs.edit().putString(KEY_COOKIE, value).apply()

    /** Whether opening the app asks for a fingerprint, face or the phone's screen lock. */
    var lockEnabled: Boolean
        get() = prefs.getBoolean(KEY_LOCK, true)
        set(value) = prefs.edit().putBoolean(KEY_LOCK, value).apply()

    /** Dark, light, or whatever the phone is set to. */
    var themeMode: ThemeMode
        get() = ThemeMode.entries.firstOrNull { it.name == prefs.getString(KEY_THEME, null) } ?: ThemeMode.System
        set(value) = prefs.edit().putString(KEY_THEME, value.name).apply()

    val isSignedIn: Boolean
        get() = serverUrl != null && username != null && password != null

    fun signOut() {
        prefs.edit().remove(KEY_USER).remove(KEY_PASSWORD).remove(KEY_COOKIE).apply()
    }

    private companion object {
        const val FILE = "mgmate_session"
        const val KEY_SERVER = "server"
        const val KEY_USER = "user"
        const val KEY_PASSWORD = "password"
        const val KEY_COOKIE = "cookie"
        const val KEY_LOCK = "lock"
        const val KEY_THEME = "theme"

        fun open(context: Context): SharedPreferences {
            fun create(): SharedPreferences {
                val key = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
                return EncryptedSharedPreferences.create(
                    FILE,
                    key,
                    context,
                    EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                    EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
                )
            }
            return try {
                create()
            } catch (_: Exception) {
                // The keystore key is gone (a restored phone, say), so the old file cannot be
                // read by anyone: start again signed out.
                context.deleteSharedPreferences(FILE)
                create()
            }
        }
    }
}
