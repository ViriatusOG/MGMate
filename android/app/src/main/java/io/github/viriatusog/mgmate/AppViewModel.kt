package io.github.viriatusog.mgmate

import android.app.Application
import androidx.biometric.BiometricManager
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import io.github.viriatusog.mgmate.data.Api
import io.github.viriatusog.mgmate.data.ApiException
import io.github.viriatusog.mgmate.data.CarStatus
import io.github.viriatusog.mgmate.data.CommandResult
import io.github.viriatusog.mgmate.data.LiveHub
import io.github.viriatusog.mgmate.data.Session
import io.github.viriatusog.mgmate.data.PublishedBuild
import io.github.viriatusog.mgmate.data.ThemeMode
import io.github.viriatusog.mgmate.data.TripLogEntry
import io.github.viriatusog.mgmate.data.Updater
import io.github.viriatusog.mgmate.data.Vehicle
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull
import java.time.Instant
import java.time.temporal.ChronoUnit

/** A trip opened from the list: its figures at once, its route once it has loaded. */
data class OpenTrip(val entry: TripLogEntry, val route: List<Pair<Double, Double>>? = null)

/** Where the app is with its own updates. */
sealed interface UpdateState {
    data object Unknown : UpdateState
    data object Checking : UpdateState
    data object UpToDate : UpdateState
    data class Available(val build: PublishedBuild) : UpdateState
    /** [progress] runs from 0 to 1, or is null while the size is not known. */
    data class Downloading(val build: PublishedBuild, val progress: Float?) : UpdateState
    data class Failed(val reason: String) : UpdateState
}

/**
 * Everything the screens show and do. The car is only ever contacted when the person asks for
 * something: readings come from the server's own store, and the live connection listens to the
 * server, so an open app costs the car's 12 V battery nothing.
 */
class AppViewModel(app: Application) : AndroidViewModel(app) {
    val session = Session(app)
    private val api = Api(session)
    private val hub = LiveHub(
        api,
        onStatus = { viewModelScope.launch { reloadStatus() } },
        onCommandResult = { result -> viewModelScope.launch { answered(result) } },
        onTripCompleted = { viewModelScope.launch { loadTrips() } },
    )

    var signedIn by mutableStateOf(session.isSignedIn)
        private set
    var signingIn by mutableStateOf(false)
        private set
    var signInError by mutableStateOf<String?>(null)
        private set
    /** True between a fresh sign-in and the answer to "protect the app with your fingerprint?". */
    var lockSetup by mutableStateOf(false)
        private set

    var vehicle by mutableStateOf<Vehicle?>(null)
        private set
    var status by mutableStateOf<CarStatus?>(null)
        private set
    var loading by mutableStateOf(false)
        private set
    /** Why the last load failed, shown until one succeeds. */
    var loadError by mutableStateOf<String?>(null)
        private set
    /** A one-off note for the snackbar: a command the car refused, say. */
    var message by mutableStateOf<String?>(null)

    var trips by mutableStateOf<List<TripLogEntry>?>(null)
        private set
    var openTrip by mutableStateOf<OpenTrip?>(null)
        private set

    var themeMode by mutableStateOf(session.themeMode)
        private set

    fun chooseTheme(mode: ThemeMode) {
        session.themeMode = mode
        themeMode = mode
    }

    private val updater = Updater(app)
    var updateState by mutableStateOf<UpdateState>(UpdateState.Unknown)
        private set
    private var updateChecked = false

    /** The controls with a command on its way, by the control's key. */
    val pending = mutableStateMapOf<String, Boolean>()

    // One waiter per command name: the gateway's answer names the command, not the request.
    private val waiting = HashMap<String, CompletableDeferred<CommandResult>>()
    private var watcher: Job? = null

    fun signIn(server: String, username: String, password: String) {
        if (signingIn) return
        signingIn = true
        signInError = null
        viewModelScope.launch {
            try {
                api.signIn(server, username.trim(), password)
                signedIn = true
                // Asked once, straight after signing in: how the app should open from now on.
                lockSetup = true
                start()
            } catch (e: ApiException) {
                signInError = e.message
            } finally {
                signingIn = false
            }
        }
    }

    /** Whether this phone has a fingerprint, a face or a screen lock to open the app with. */
    fun canLock(): Boolean =
        BiometricManager.from(getApplication()).canAuthenticate(LOCK_AUTHENTICATORS) == BiometricManager.BIOMETRIC_SUCCESS

    /** The answer to the question after signing in. */
    fun chooseLock(enabled: Boolean) {
        session.lockEnabled = enabled
        lockSetup = false
        if (enabled) message = "Done. The app now opens with your fingerprint or PIN."
    }

    fun signOut() {
        stop()
        session.signOut()
        signedIn = false
        vehicle = null
        status = null
        trips = null
        openTrip = null
        loadError = null
    }

    /** Called when the app comes on screen: reads what the server holds and starts listening. */
    fun start() {
        if (!signedIn) return
        refresh()
        if (!updateChecked) {
            updateChecked = true
            checkForUpdate(announce = true)
        }
        watcher?.cancel()
        watcher = viewModelScope.launch {
            while (true) {
                val id = vehicle?.id
                if (id != null && !hub.isOpen) hub.open(id)
                // A safety net under the live connection; the server answers from its database.
                delay(60_000)
                reloadStatus()
            }
        }
    }

    /**
     * Looks on GitHub for a newer build. Done once each time the app is started, and whenever the
     * owner asks under Settings; [announce] says so in a note when one is found.
     */
    fun checkForUpdate(announce: Boolean = false) {
        if (updateState is UpdateState.Checking || updateState is UpdateState.Downloading) return
        updateState = UpdateState.Checking
        viewModelScope.launch {
            updateState = try {
                val build = updater.latest()
                if (build.versionCode > BuildConfig.VERSION_CODE) {
                    if (announce) message = "Version ${build.versionName} is ready. Update under Settings."
                    UpdateState.Available(build)
                } else {
                    UpdateState.UpToDate
                }
            } catch (e: ApiException) {
                UpdateState.Failed(e.message ?: "Could not look for an update.")
            }
        }
    }

    /** Downloads the newer build and opens Android's installer on it. */
    fun installUpdate() {
        val build = (updateState as? UpdateState.Available)?.build ?: return
        updateState = UpdateState.Downloading(build, 0f)
        viewModelScope.launch {
            try {
                val apk = updater.download { progress ->
                    viewModelScope.launch { if (updateState is UpdateState.Downloading) updateState = UpdateState.Downloading(build, progress) }
                }
                updateState = UpdateState.Available(build)
                updater.install(apk)
            } catch (e: ApiException) {
                updateState = UpdateState.Failed(e.message ?: "The update could not be downloaded.")
            } catch (e: Exception) {
                updateState = UpdateState.Failed("Android could not open the installer.")
            }
        }
    }

    /** Called when the app leaves the screen. */
    fun stop() {
        watcher?.cancel()
        watcher = null
        hub.close()
    }

    /** Reads the server's newest data again. This does not wake the car. */
    fun refresh() {
        if (!signedIn || loading) return
        loading = true
        viewModelScope.launch {
            try {
                if (vehicle == null) vehicle = api.vehicles().firstOrNull()
                val car = vehicle
                if (car == null) {
                    loadError = "The server has no car yet."
                } else {
                    status = api.status(car.vin)
                    loadError = null
                    if (!hub.isOpen) hub.open(car.id)
                    loadTrips()
                }
            } catch (e: ApiException) {
                failed(e)
            } finally {
                loading = false
            }
        }
    }

    private suspend fun reloadStatus() {
        val car = vehicle ?: return
        try {
            status = api.status(car.vin) ?: status
            loadError = null
        } catch (e: ApiException) {
            failed(e)
        }
    }

    private suspend fun loadTrips() {
        val car = vehicle ?: return
        try {
            val now = Instant.now()
            trips = api.tripLog(car.vin, now.minus(30, ChronoUnit.DAYS), now.plus(1, ChronoUnit.DAYS))
                .filter { !it.isHidden }
                .sortedByDescending { it.startedAt }
        } catch (e: ApiException) {
            if (trips == null) trips = emptyList()
            failed(e)
        }
    }

    private fun failed(e: ApiException) {
        if (e.status == 401) signOut() else loadError = e.message
    }

    fun showTrip(entry: TripLogEntry) {
        openTrip = OpenTrip(entry)
        val car = vehicle ?: return
        viewModelScope.launch {
            val route = try {
                val from = Instant.parse(normalise(entry.startedAt))
                val to = Instant.parse(normalise(entry.endedAt))
                api.tripRoute(car.vin, entry.id, from, to)
            } catch (_: Exception) {
                emptyList()
            }
            if (openTrip?.entry?.id == entry.id) openTrip = OpenTrip(entry, route)
        }
    }

    fun closeTrip() {
        openTrip = null
    }

    private fun normalise(time: String): String =
        io.github.viriatusog.mgmate.data.Units.parseInstant(time)?.toString() ?: time

    /**
     * Sends one command and waits for the car. [key] names the control showing the wait;
     * [confirmed] says when a reading shows the command took effect, for controls whose state the
     * car reports. Without it the gateway's own "done" is the end.
     */
    fun send(
        key: String,
        command: String,
        value: String,
        done: String? = null,
        confirmed: ((CarStatus) -> Boolean)? = null,
    ) {
        val car = vehicle ?: return
        if (pending[key] == true) return
        pending[key] = true
        viewModelScope.launch {
            try {
                val answer = CompletableDeferred<CommandResult>()
                waiting[command] = answer
                api.command(car.vin, command, value)

                // The gateway polls MG for up to 30 s before it answers.
                val result = withTimeoutOrNull(ANSWER_TIMEOUT_MS) { answer.await() }
                when {
                    result == null -> {
                        // No answer heard: the live connection may have dropped, so look.
                        reloadStatus()
                        val current = status
                        if (confirmed != null && current != null && confirmed(current)) {
                            done?.let { message = it }
                        } else {
                            message = "The car has not answered yet. Check again in a moment."
                        }
                    }
                    !result.success -> message = result.detail?.let { "The car refused: $it" } ?: "The car refused the command."
                    else -> {
                        if (confirmed != null) awaitReading(confirmed)
                        done?.let { message = it }
                    }
                }
            } catch (e: ApiException) {
                message = e.message
            } finally {
                waiting.remove(command)
                pending.remove(key)
            }
        }
    }

    /** After the car said yes: reads the server a few times until the new state shows. */
    private suspend fun awaitReading(confirmed: (CarStatus) -> Boolean) {
        repeat(6) {
            reloadStatus()
            val current = status
            if (current != null && confirmed(current)) return
            delay(4_000)
        }
    }

    private fun answered(result: CommandResult) {
        waiting[result.command]?.complete(result)
    }

    override fun onCleared() {
        hub.close()
    }

    companion object {
        /** A fingerprint or face, or else the phone's PIN, pattern or password. */
        const val LOCK_AUTHENTICATORS = BiometricManager.Authenticators.BIOMETRIC_WEAK or BiometricManager.Authenticators.DEVICE_CREDENTIAL

        // Matches the web app, which gives the gateway 45 s.
        private const val ANSWER_TIMEOUT_MS = 45_000L
    }
}
