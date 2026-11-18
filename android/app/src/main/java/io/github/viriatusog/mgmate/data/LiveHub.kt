package io.github.viriatusog.mgmate.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.buildJsonArray
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.decodeFromJsonElement
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener

/**
 * The server's live connection (SignalR hub /hubs/telemetry, JSON protocol over a WebSocket), which
 * pushes each new reading and the car's answer to a command. Only open while the app is on screen.
 * It talks to the server alone, so listening never wakes the car.
 */
class LiveHub(
    private val api: Api,
    private val onStatus: (CarStatus) -> Unit,
    private val onCommandResult: (CommandResult) -> Unit,
    private val onTripCompleted: () -> Unit,
) {
    private var socket: WebSocket? = null
    private val json: Json get() = api.json

    val isOpen: Boolean get() = socket != null

    /** Opens the connection for one car. Returns false when it could not be opened. */
    suspend fun open(vehicleId: Int): Boolean = withContext(Dispatchers.IO) {
        close()
        val base = api.baseUrl() ?: return@withContext false
        try {
            val negotiate = Request.Builder()
                .url(base.newBuilder().addPathSegments("hubs/telemetry/negotiate").addQueryParameter("negotiateVersion", "1").build())
                .post(ByteArray(0).toRequestBody())
                .build()
            val token = api.http.newCall(negotiate).execute().use { response ->
                if (!response.isSuccessful) return@withContext false
                val body = json.parseToJsonElement(response.body?.string().orEmpty()).jsonObject
                (body["connectionToken"] ?: body["connectionId"])?.jsonPrimitive?.content
            } ?: return@withContext false

            val url = base.newBuilder().addPathSegments("hubs/telemetry").addQueryParameter("id", token).build()
            socket = api.http.newWebSocket(Request.Builder().url(url).build(), Listener(vehicleId))
            true
        } catch (_: Exception) {
            false
        }
    }

    fun close() {
        socket?.close(1000, null)
        socket = null
    }

    private inner class Listener(private val vehicleId: Int) : WebSocketListener() {
        override fun onOpen(webSocket: WebSocket, response: Response) {
            webSocket.send("""{"protocol":"json","version":1}$RECORD_END""")
            val join = buildJsonObject {
                put("type", 1)
                put("invocationId", "1")
                put("target", "JoinVehicle")
                put("arguments", buildJsonArray { add(kotlinx.serialization.json.JsonPrimitive(vehicleId)) })
            }
            webSocket.send(join.toString() + RECORD_END)
        }

        override fun onMessage(webSocket: WebSocket, text: String) {
            for (record in text.split(RECORD_END)) {
                if (record.isBlank()) continue
                val message = try {
                    json.parseToJsonElement(record).jsonObject
                } catch (_: Exception) {
                    continue
                }
                when (message["type"]?.jsonPrimitive?.int) {
                    1 -> invocation(message)
                    // The server pings every 15 s and expects one back within its timeout.
                    6 -> webSocket.send("""{"type":6}$RECORD_END""")
                    7 -> dropped(webSocket)
                }
            }
        }

        private fun invocation(message: JsonObject) {
            val arguments = message["arguments"]?.jsonArray ?: return
            try {
                when (message["target"]?.jsonPrimitive?.content) {
                    "telemetryUpdated" -> onStatus(json.decodeFromJsonElement<CarStatus>(arguments[0]))
                    "commandResult" -> onCommandResult(json.decodeFromJsonElement<CommandResult>(arguments[0]))
                    "tripCompleted" -> onTripCompleted()
                }
            } catch (_: Exception) {
                // A message this version does not understand is not worth losing the connection over.
            }
        }

        override fun onClosed(webSocket: WebSocket, code: Int, reason: String) = dropped(webSocket)

        override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) = dropped(webSocket)

        private fun dropped(webSocket: WebSocket) {
            if (socket === webSocket) socket = null
        }
    }

    private companion object {
        // SignalR ends every JSON message with the ASCII record separator.
        const val RECORD_END = "\u001e"
    }
}
