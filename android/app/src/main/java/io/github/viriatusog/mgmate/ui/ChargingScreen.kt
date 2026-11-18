package io.github.viriatusog.mgmate.ui

import android.app.TimePickerDialog
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.BatteryChargingFull
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.outlined.Cable
import androidx.compose.material.icons.outlined.ChevronRight
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.viriatusog.mgmate.AppViewModel
import io.github.viriatusog.mgmate.data.CarStatus
import io.github.viriatusog.mgmate.data.Units
import java.time.Instant
import java.util.Locale
import kotlin.math.abs
import kotlin.math.roundToInt

private const val MODE_UNTIL_TIME = "UNTIL_CONFIGURED_TIME"
private const val MODE_UNTIL_TARGET = "UNTIL_CONFIGURED_SOC"
private const val MODE_OFF = "DISABLED"

/** The Charging tab: the charge under way, then everything that shapes the next one. */
@Composable
fun ChargingScreen(vm: AppViewModel, ask: (Confirm) -> Unit) {
    val status = vm.status
    var editSchedule by remember { mutableStateOf(false) }

    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(horizontal = 16.dp).padding(top = 8.dp, bottom = 16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        ChargeCard(vm, status, ask)

        if (vm.vehicle?.supportsTargetSoc != false) {
            SectionTitle("Charge limit")
            Panel {
                Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    val target = status?.socTargetPercent?.roundToInt()
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text("Stop at", color = Volt.Ink, fontSize = 15.sp, fontWeight = FontWeight.Medium)
                        Spacer(Modifier.weight(1f))
                        rangeAt(status, target)?.let { Text("about $it mi", color = Volt.Ink3, fontSize = 13.sp) }
                    }
                    Segmented(
                        options = listOf(40, 50, 60, 70, 80, 90, 100).map { it to it.toString() },
                        selected = target,
                        modifier = Modifier.fillMaxWidth(),
                        busy = vm.pending["target"] == true,
                    ) { wanted ->
                        vm.send("target", "charge-target", wanted.toString()) { it.socTargetPercent?.roundToInt() == wanted }
                    }
                }
            }
        }

        SectionTitle("Charging current")
        Panel {
            Column(Modifier.padding(12.dp)) {
                Segmented(
                    options = listOf("6A" to "6 A", "8A" to "8 A", "16A" to "16 A", "MAX" to "Max"),
                    selected = status?.chargeCurrentLimit?.uppercase(Locale.ROOT),
                    modifier = Modifier.fillMaxWidth(),
                    busy = vm.pending["current"] == true,
                ) { wanted ->
                    vm.send("current", "charge-limit", wanted) { it.chargeCurrentLimit.equals(wanted, ignoreCase = true) }
                }
            }
        }

        Panel {
            val cable = status?.chargingCableLock == true
            ListRow(Icons.Outlined.Cable, "Lock the cable", "Keeps the cable in the car", on = cable) {
                BusySwitch(cable, vm.pending["cable"] == true) { wanted ->
                    vm.send("cable", "charging-cable-lock", wanted.toString()) { (it.chargingCableLock == true) == wanted }
                }
            }
            RowDivider()
            val heating = status?.batteryHeating == true
            ListRow(Icons.Outlined.BatteryChargingFull, "Battery heating", "Warms the battery for faster charging", on = heating) {
                BusySwitch(heating, vm.pending["heating"] == true) { wanted ->
                    vm.send("heating", "battery-heating", wanted.toString()) { (it.batteryHeating == true) == wanted }
                }
            }
            RowDivider()
            ListRow(
                Icons.Outlined.Schedule,
                "Car schedule",
                scheduleText(status),
                on = scheduleOn(status),
                onClick = { editSchedule = true },
            ) {
                if (vm.pending["schedule"] == true) {
                    CircularProgressIndicator(Modifier.size(22.dp), color = Volt.Info, strokeWidth = 2.5.dp)
                } else {
                    Icon(Icons.Outlined.ChevronRight, contentDescription = "Change", tint = Volt.Ink4)
                }
            }
        }
    }

    if (editSchedule) {
        ScheduleDialog(status, onDismiss = { editSchedule = false }) { mode, start, end ->
            editSchedule = false
            val value = """{"mode":"$mode","startTime":"$start","endTime":"$end"}"""
            vm.send("schedule", "scheduled-charging", value, done = "Schedule sent to the car") {
                it.chargingScheduleMode == mode && (mode == MODE_OFF || (clock(it.chargingScheduleStartTime) == start && clock(it.chargingScheduleEndTime) == end))
            }
        }
    }
}

@Composable
private fun ChargeCard(vm: AppViewModel, status: CarStatus?, ask: (Confirm) -> Unit) {
    val charging = status?.isCharging == true
    val plugged = charging || status?.chargerConnected == true
    val busy = vm.pending["charging"] == true
    Panel {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                LevelRing(status?.evSocPercent, status?.electricRangeKm)
                Spacer(Modifier.width(14.dp))
                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    when {
                        charging -> Chip(listOfNotNull("Charging", status?.chargingType?.takeIf { it.isNotBlank() }).joinToString(" · "), Tone.Accent, Icons.Outlined.Bolt)
                        plugged -> Chip("Plugged in, not charging")
                        else -> Chip("Not plugged in")
                    }
                    val power = chargePower(status)
                    if (charging && power != null) {
                        Row(verticalAlignment = Alignment.Bottom) {
                            Text(String.format(Locale.UK, "%.1f", power), color = Volt.Ink, fontSize = 28.sp, fontWeight = FontWeight.Bold)
                            Text(" kW", color = Volt.Ink3, fontSize = 15.sp, modifier = Modifier.padding(bottom = 4.dp))
                        }
                    }
                    chargeLine(status)?.let { Text(it, color = Volt.Ink3, fontSize = 13.sp) }
                }
            }
            when {
                charging -> Button(
                    onClick = {
                        ask(
                            Confirm("Stop charging?", "The car stops taking power now. You can start it again from here while it stays plugged in.", "Stop charging", danger = true) {
                                vm.send("charging", "charging", "false", done = "Charging stopped") { it.isCharging == false }
                            },
                        )
                    },
                    enabled = !busy,
                    modifier = Modifier.fillMaxWidth().height(48.dp),
                    shape = RoundedCornerShape(14.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = Volt.AlertWash, contentColor = Volt.Alert),
                ) { Text(if (busy) "Waiting for the car" else "Stop charging", fontSize = 15.sp, fontWeight = FontWeight.SemiBold) }

                plugged -> Button(
                    onClick = { vm.send("charging", "charging", "true", done = "Charging started") { it.isCharging == true } },
                    enabled = !busy,
                    modifier = Modifier.fillMaxWidth().height(48.dp),
                    shape = RoundedCornerShape(14.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = Volt.Accent, contentColor = Volt.AccentInk),
                ) { Text(if (busy) "Waiting for the car" else "Start charging", fontSize = 15.sp, fontWeight = FontWeight.SemiBold) }

                else -> Text("Plug the car in to start a charge from here.", color = Volt.Ink3, fontSize = 13.sp)
            }
        }
    }
}

@Composable
private fun LevelRing(percent: Double?, rangeKm: Double?) {
    Box(Modifier.size(96.dp), contentAlignment = Alignment.Center) {
        Canvas(Modifier.size(96.dp)) {
            val width = 9.dp.toPx()
            val inset = width / 2
            val arc = Size(size.width - width, size.height - width)
            drawArc(Volt.Raise, 0f, 360f, false, topLeft = Offset(inset, inset), size = arc, style = Stroke(width))
            val sweep = (((percent ?: 0.0) / 100.0).coerceIn(0.0, 1.0) * 360.0).toFloat()
            if (sweep > 0f) {
                drawArc(Volt.Accent, -90f, sweep, false, topLeft = Offset(inset, inset), size = arc, style = Stroke(width, cap = StrokeCap.Round))
            }
        }
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Text(Units.percent(percent) ?: "--", color = Volt.Ink, fontSize = 24.sp, fontWeight = FontWeight.Bold)
            Units.wholeMiles(rangeKm)?.let { Text("$it mi", color = Volt.Ink3, fontSize = 11.sp) }
        }
    }
}

/** The power going into the battery, as the web app reads it: the pack's own figure, else the charger's. */
private fun chargePower(status: CarStatus?): Double? {
    val pack = status?.hvPower?.let { abs(it) }?.takeIf { it > 0.05 }
    return pack ?: status?.obcPowerThreePhase?.takeIf { it > 0.05 } ?: status?.obcPowerSinglePhase?.takeIf { it > 0.05 }
}

private fun chargeLine(status: CarStatus?): String? {
    if (status?.isCharging != true) return null
    val left = status.remainingChargingTime?.takeIf { it > 0 }?.toLong() ?: return null
    val target = status.socTargetPercent?.roundToInt() ?: 100
    val read = Units.parseInstant(status.recordedAt) ?: Instant.now()
    return "$target% at ${Units.time(read.plusSeconds(left))} · ${Units.duration(left)} left"
}

/** How far the car goes at [percent], scaled from the range at today's level. */
private fun rangeAt(status: CarStatus?, percent: Int?): String? {
    val km = status?.electricRangeKm ?: return null
    val level = status.evSocPercent?.takeIf { it >= 5 } ?: return null
    if (percent == null) return null
    return Units.wholeMiles(km / level * percent)
}

private fun clock(time: String?): String? = time?.takeIf { it.length >= 5 }?.substring(0, 5)

private fun scheduleOn(status: CarStatus?): Boolean =
    status?.chargingScheduleMode == MODE_UNTIL_TIME || status?.chargingScheduleMode == MODE_UNTIL_TARGET

private fun scheduleText(status: CarStatus?): String {
    val start = clock(status?.chargingScheduleStartTime)
    val end = clock(status?.chargingScheduleEndTime)
    return when (status?.chargingScheduleMode) {
        MODE_UNTIL_TIME -> if (start != null && end != null) "$start to $end every day" else "On"
        MODE_UNTIL_TARGET -> if (start != null) "From $start until the charge limit" else "On"
        MODE_OFF -> "Off: charges as soon as it is plugged in"
        else -> "Not reported by the car yet"
    }
}

/** The car keeps one charging window, the same every day, and runs it itself. */
@Composable
private fun ScheduleDialog(status: CarStatus?, onDismiss: () -> Unit, onSave: (mode: String, start: String, end: String) -> Unit) {
    val context = LocalContext.current
    var on by remember { mutableStateOf(scheduleOn(status)) }
    var start by remember { mutableStateOf(clock(status?.chargingScheduleStartTime) ?: "00:30") }
    var end by remember { mutableStateOf(clock(status?.chargingScheduleEndTime) ?: "04:30") }

    fun pick(current: String, set: (String) -> Unit) {
        val hour = current.substringBefore(':').toIntOrNull() ?: 0
        val minute = current.substringAfter(':').toIntOrNull() ?: 0
        TimePickerDialog(context, { _, h, m -> set(String.format(Locale.ROOT, "%02d:%02d", h, m)) }, hour, minute, true).show()
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = Volt.Elev,
        title = { Text("Car schedule") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Text("The car charges in this window every day, by itself.", color = Volt.Ink2, fontSize = 14.sp)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("Use a schedule", color = Volt.Ink, fontSize = 15.sp, modifier = Modifier.weight(1f))
                    BusySwitch(on, busy = false) { on = it }
                }
                if (on) {
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        OutlinedButton(onClick = { pick(start) { start = it } }, modifier = Modifier.weight(1f)) {
                            Text("From $start", color = Volt.Ink)
                        }
                        OutlinedButton(onClick = { pick(end) { end = it } }, modifier = Modifier.weight(1f)) {
                            Text("Until $end", color = Volt.Ink)
                        }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = { onSave(if (on) MODE_UNTIL_TIME else MODE_OFF, start, end) }) { Text("Send to the car") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel", color = Volt.Ink2) } },
    )
}
