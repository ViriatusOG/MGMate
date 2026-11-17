package io.github.viriatusog.mgmate

import android.os.Bundle
import android.os.SystemClock
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricManager.Authenticators.BIOMETRIC_WEAK
import androidx.biometric.BiometricManager.Authenticators.DEVICE_CREDENTIAL
import androidx.biometric.BiometricPrompt
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.core.content.ContextCompat
import androidx.fragment.app.FragmentActivity
import io.github.viriatusog.mgmate.ui.AppRoot

/**
 * The one screen of the app. It also keeps the lock: with a saved sign-in, the app opens only
 * after a fingerprint, a face or the phone's own screen lock, and locks again once it has been
 * out of sight for a minute.
 */
class MainActivity : FragmentActivity() {
    private val vm: AppViewModel by viewModels()

    private var locked by mutableStateOf(false)
    private var prompting = false
    // Elapsed-realtime of when the app last left the screen; null until it has.
    private var leftAt: Long? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)
        // A fresh launch starts locked; a rotation keeps whatever the lock was.
        locked = savedInstanceState?.getBoolean(STATE_LOCKED) ?: shouldLock()
        leftAt = savedInstanceState?.getLong(STATE_LEFT_AT, -1L)?.takeIf { it >= 0 }
        setContent {
            AppRoot(vm, locked = locked && vm.signedIn, canLock = canAuthenticate(), onUnlock = ::unlock)
        }
    }

    override fun onStart() {
        super.onStart()
        val away = leftAt?.let { SystemClock.elapsedRealtime() - it }
        if (away != null && away > RELOCK_AFTER_MS && shouldLock()) locked = true
        leftAt = null
        if (locked && vm.signedIn) unlock() else vm.start()
    }

    override fun onStop() {
        super.onStop()
        if (!isChangingConfigurations) {
            leftAt = SystemClock.elapsedRealtime()
            vm.stop()
        }
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        outState.putBoolean(STATE_LOCKED, locked)
        outState.putLong(STATE_LEFT_AT, leftAt ?: -1L)
    }

    private fun canAuthenticate(): Boolean =
        BiometricManager.from(this).canAuthenticate(AUTHENTICATORS) == BiometricManager.BIOMETRIC_SUCCESS

    private fun shouldLock(): Boolean = vm.session.isSignedIn && vm.session.lockEnabled && canAuthenticate()

    private fun unlock() {
        if (prompting) return
        if (!canAuthenticate()) {
            // The phone's screen lock was removed since: there is nothing left to ask.
            opened()
            return
        }
        prompting = true
        val prompt = BiometricPrompt(
            this,
            ContextCompat.getMainExecutor(this),
            object : BiometricPrompt.AuthenticationCallback() {
                override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) {
                    prompting = false
                    opened()
                }

                override fun onAuthenticationError(errorCode: Int, errString: CharSequence) {
                    // Cancelled or failed: stay locked; the Unlock button asks again.
                    prompting = false
                }
            },
        )
        val info = BiometricPrompt.PromptInfo.Builder()
            .setTitle("Unlock MGMate")
            .setSubtitle("Confirm it is you to control the car")
            .setAllowedAuthenticators(AUTHENTICATORS)
            .build()
        prompt.authenticate(info)
    }

    private fun opened() {
        locked = false
        vm.start()
    }

    private companion object {
        // A fingerprint or face, or else the phone's PIN, pattern or password.
        const val AUTHENTICATORS = BIOMETRIC_WEAK or DEVICE_CREDENTIAL
        const val RELOCK_AFTER_MS = 60_000L
        const val STATE_LOCKED = "locked"
        const val STATE_LEFT_AT = "leftAt"
    }
}
