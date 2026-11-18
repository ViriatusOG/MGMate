package io.github.viriatusog.mgmate.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import okhttp3.Cookie
import okhttp3.CookieJar
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.IOException
import java.time.Instant
import java.util.concurrent.TimeUnit

/** A failure worth showing: what went wrong, in words for the person holding the phone. */
class ApiException(message: String, val status: Int = 0) : Exception(message)

/**
 * The MGMate server's API, as the web app uses it: a cookie session from POST /api/auth/login,
 * then JSON. A 401 is answered by signing in again once with the saved sign-in, so the session's
 * expiry never reaches the screen.
 */
class Api(private val session: Session) {
    val json = Json {
        ignoreUnknownKeys = true
        explicitNulls = false
        coerceInputValues = true
    }

    private val jsonType = "application/json; charset=utf-8".toMediaType()

    // Only the session cookie matters, and only for the configured server.
    private val cookies = object : CookieJar {
        override fun saveFromResponse(url: HttpUrl, cookies: List<Cookie>) {
            cookies.firstOrNull { it.name == COOKIE_NAME }?.let { cookie ->
                session.cookie = if (cookie.value.isEmpty()) null else "${cookie.name}=${cookie.value}"
            }
        }

        override fun loadForRequest(url: HttpUrl): List<Cookie> {
            val saved = session.cookie ?: return emptyList()
            val base = baseUrl() ?: return emptyList()
            if (url.host != base.host || url.port != base.port) return emptyList()
            val value = saved.substringAfter('=')
            return listOf(Cookie.Builder().name(COOKIE_NAME).value(value).hostOnlyDomain(url.host).build())
        }
    }

    val http: OkHttpClient = OkHttpClient.Builder()
        .cookieJar(cookies)
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .build()

    fun baseUrl(): HttpUrl? = session.serverUrl?.toHttpUrlOrNull()

    /** Signs in and saves the sign-in. Throws [ApiException] with a reason to show. */
    suspend fun signIn(server: String, username: String, password: String) {
        val url = normaliseServer(server) ?: throw ApiException("That does not look like a server address.")
        val previous = session.serverUrl
        session.serverUrl = url
        session.cookie = null
        try {
            login(username, password)
            // Proves the cookie works and that this really is an MGMate server.
            vehicles()
        } catch (e: Exception) {
            session.serverUrl = previous
            session.cookie = null
            throw e
        }
        session.username = username
        session.password = password
    }

    private suspend fun login(username: String, password: String) {
        val body = json.encodeToString(LoginRequest(username, password)).toRequestBody(jsonType)
        val request = Request.Builder().url(url("/api/auth/login")).post(body).build()
        execute(request) { status, text ->
            when (status) {
                in 200..299 -> Unit
                401 -> throw ApiException("Wrong username or password.", status)
                404 -> throw ApiException("This server has password sign-in switched off.", status)
                429 -> throw ApiException("Too many attempts. Wait a minute and try again.", status)
                else -> throw ApiException(problemText(text) ?: "The server answered $status.", status)
            }
        }
    }

    suspend fun vehicles(): List<Vehicle> = get("/api/vehicles") ?: emptyList()

    /** Null while the server has no reading for the car yet. */
    suspend fun status(vin: String): CarStatus? = get("/api/vehicles/$vin/status")

    /** The pressures below and above which the server calls a tyre low or high. */
    suspend fun tyreLimits(): TyreLimits? = get("/api/vehicles/tyre-pressure-thresholds")

    suspend fun tripLog(vin: String, from: Instant, to: Instant): List<TripLogEntry> =
        get("/api/vehicles/$vin/trips/log?from=$from&to=$to") ?: emptyList()

    /** The route of the trip that ran from [from] to [to], or an empty list when it has none. */
    suspend fun tripRoute(vin: String, id: Long, from: Instant, to: Instant): List<Pair<Double, Double>> {
        val trips: List<CompactTrip> = get(
            "/api/vehicles/$vin/trips?format=compact&from=${from.minusSeconds(60)}&to=${to.plusSeconds(60)}",
        ) ?: emptyList()
        val trip = trips.firstOrNull { it.id == id } ?: return emptyList()
        return decodePolyline(trip.line)
    }

    suspend fun command(vin: String, command: String, value: String) {
        val body = json.encodeToString(CommandRequest(value)).toRequestBody(jsonType)
        authorised<Unit>({ Request.Builder().url(url("/api/vehicles/$vin/commands/$command")).post(body).build() }) { }
    }

    private suspend inline fun <reified T> get(path: String): T? =
        authorised({ Request.Builder().url(url(path)).get().build() }) { text ->
            if (text.isBlank()) null else json.decodeFromString<T>(text)
        }

    /** Runs a request, signing in again once when the session has run out. */
    private suspend fun <T> authorised(build: () -> Request, read: (String) -> T?): T? {
        var retried = false
        while (true) {
            var again = false
            val result = execute(build()) { status, text ->
                when {
                    status in 200..299 -> read(text)
                    status == 401 && !retried -> {
                        again = true
                        null
                    }
                    status == 401 -> throw ApiException("Your sign-in no longer works. Sign in again.", status)
                    else -> throw ApiException(problemText(text) ?: "The server answered $status.", status)
                }
            }
            if (!again) return result
            retried = true
            val user = session.username
            val password = session.password
            if (user == null || password == null) throw ApiException("Sign in again.", 401)
            login(user, password)
        }
    }

    private suspend fun <T> execute(request: Request, read: (Int, String) -> T): T = withContext(Dispatchers.IO) {
        try {
            http.newCall(request).execute().use { response ->
                read(response.code, response.body?.string().orEmpty())
            }
        } catch (e: IOException) {
            throw ApiException("Cannot reach the server. Check the address and your connection.")
        } catch (e: kotlinx.serialization.SerializationException) {
            throw ApiException("The server sent an answer this app does not understand.")
        }
    }

    private fun problemText(text: String): String? = try {
        json.decodeFromString<Problem>(text).let { it.detail ?: it.title }
    } catch (_: Exception) {
        null
    }

    private fun url(path: String): HttpUrl {
        val base = baseUrl() ?: throw ApiException("No server address set.")
        return (base.toString().trimEnd('/') + path).toHttpUrlOrNull()
            ?: throw ApiException("No server address set.")
    }

    companion object {
        const val COOKIE_NAME = "garagestack-auth"

        /** "192.168.1.50:4020" and "https://car.example.com/" both become a clean base address. */
        fun normaliseServer(input: String): String? {
            val trimmed = input.trim().trimEnd('/')
            if (trimmed.isEmpty()) return null
            val withScheme = if (trimmed.startsWith("http://") || trimmed.startsWith("https://")) trimmed else "http://$trimmed"
            val parsed = withScheme.toHttpUrlOrNull() ?: return null
            return parsed.newBuilder().encodedPath("/").query(null).fragment(null).build().toString().trimEnd('/')
        }
    }
}
