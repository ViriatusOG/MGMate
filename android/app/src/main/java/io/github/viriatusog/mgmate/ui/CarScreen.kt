package io.github.viriatusog.mgmate.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.AcUnit
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Adjust
import androidx.compose.material.icons.outlined.Air
import androidx.compose.material.icons.outlined.EventSeat
import androidx.compose.material.icons.outlined.Inventory2
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.LockOpen
import androidx.compose.material.icons.outlined.NearMe
import androidx.compose.material.icons.outlined.Remove
import androidx.compose.material.icons.outlined.Thermostat
import androidx.compose.material.icons.outlined.Waves
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.viriatusog.mgmate.AppViewModel
import io.github.viriatusog.mgmate.data.CarStatus
import io.github.viriatusog.mgmate.data.Units
import kotlinx.coroutines.delay
import kotlin.math.roundToInt

private const val MIN_TEMPERATURE = 16
private const val MAX_TEMPERATURE = 28
private const val DEFAULT_TEMPERATURE = 21

/** The Car tab: what the car is doing, the four things done most, then comfort. */
@Composable
fun CarScreen(vm: AppViewModel, ask: (Confirm) -> Unit) {
    val status = vm.status
    Column(
        Modifier.verticalScroll(rememberScrollState()).padding(horizontal = 16.dp).padding(top = 8.dp, bottom = 16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        StatusCard(status)

        val locked = status?.isLocked == true
        val climateOn = status?.climateOn == true
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            ControlTile(
                icon = if (locked) Icons.Outlined.Lock else Icons.Outlined.LockOpen,
                title = when (status?.isLocked) {
                    true -> "Locked"
                    false -> "Unlocked"
                    null -> "Locks"
                },
                subtitle = if (locked) "Tap to unlock" else "Tap to lock",
                modifier = Modifier.weight(1f),
                busy = vm.pending["lock"] == true,
            ) {
                if (locked) {
                    ask(
                        Confirm("Unlock the car?", "All doors will unlock. Lock it again from here when you are done.", "Unlock") {
                            vm.send("lock", "lock", "False", done = "Unlocked") { it.isLocked == false }
                        },
                    )
                } else {
                    vm.send("lock", "lock", "True", done = "Locked") { it.isLocked == true }
                }
            }
            ControlTile(
                icon = Icons.Outlined.AcUnit,
                title = if (climateOn) "Climate on" else "Climate",
                subtitle = if (climateOn) "Tap to stop" else "Tap to start",
                modifier = Modifier.weight(1f),
                on = climateOn,
                busy = vm.pending["climate"] == true,
            ) {
                if (climateOn) {
                    vm.send("climate", "climate", "off") { it.climateOn == false }
                } else {
                    vm.send("climate", "climate", "on") { it.climateOn == true }
                }
            }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            ControlTile(
                icon = Icons.Outlined.Inventory2,
                title = if (status?.trunkOpen == true) "Boot open" else "Boot",
                subtitle = "Tap to open",
                modifier = Modifier.weight(1f),
                busy = vm.pending["boot"] == true,
            ) {
                ask(
                    Confirm("Open the boot?", "The boot will unlatch. It cannot be closed from the app.", "Open the boot") {
                        vm.send("boot", "boot", "false", done = "Boot opened")
                    },
                )
            }
            ControlTile(
                icon = Icons.Outlined.NearMe,
                title = "Find my car",
                subtitle = "Horn and lights",
                modifier = Modifier.weight(1f),
                busy = vm.pending["find"] == true,
            ) {
                vm.send("find", "find-my-car", "activate", done = "Listen and look for your car")
            }
        }

        SectionTitle("Comfort")
        Panel {
            TemperatureRow(vm, status)
            RowDivider()
            val front = status?.climateMode == "front"
            ListRow(Icons.Outlined.Air, "Front defrost", on = front) {
                BusySwitch(front, vm.pending["front"] == true) { wanted ->
                    vm.send("front", "climate", if (wanted) "front" else "off") { (it.climateMode == "front") == wanted }
                }
            }
            RowDivider()
            val rear = status?.rearWindowDefroster == true
            ListRow(Icons.Outlined.Waves, "Rear window heating", on = rear) {
                BusySwitch(rear, vm.pending["rear"] == true) { wanted ->
                    vm.send("rear", "rear-defroster", if (wanted) "on" else "off") { (it.rearWindowDefroster == true) == wanted }
                }
            }
            RowDivider()
            val wheel = status?.steeringWheelHeating == true
            ListRow(Icons.Outlined.Adjust, "Heated steering wheel", on = wheel) {
                BusySwitch(wheel, vm.pending["wheel"] == true) { wanted ->
                    vm.send("wheel", "steering-wheel-heat", if (wanted) "on" else "off") { (it.steeringWheelHeating == true) == wanted }
                }
            }
            RowDivider()
            SeatRow(vm, "Driver seat", "seat-left", status?.heatedSeatFrontLeft) { it.heatedSeatFrontLeft }
            RowDivider()
            SeatRow(vm, "Passenger seat", "seat-right", status?.heatedSeatFrontRight) { it.heatedSeatFrontRight }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun StatusCard(status: CarStatus?) {
    Panel {
        Row(Modifier.padding(horizontal = 14.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
            CarDrawing(
                Modifier.width(74.dp).height(112.dp),
                pluggedIn = status?.chargerConnected == true || status?.isCharging == true,
                charging = status?.isCharging == true,
            )
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f)) {
                Row(verticalAlignment = Alignment.Bottom) {
                    Text(Units.wholeMiles(status?.electricRangeKm) ?: "--", color = Volt.Ink, fontSize = 36.sp, fontWeight = FontWeight.Bold)
                    Text(" mi", color = Volt.Ink3, fontSize = 16.sp, modifier = Modifier.padding(bottom = 6.dp))
                    Spacer(Modifier.weight(1f))
                    Text(Units.percent(status?.evSocPercent) ?: "--", color = Volt.Ink, fontSize = 20.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.padding(bottom = 4.dp))
                }
                Meter(status?.evSocPercent, status?.socTargetPercent, Modifier.padding(top = 4.dp))
                FlowRow(
                    Modifier.padding(top = 10.dp),
                    horizontalArrangement = Arrangement.spacedBy(6.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp),
                ) {
                    when (status?.isLocked) {
                        true -> Chip("Locked", icon = Icons.Outlined.Lock)
                        false -> Chip("Unlocked", Tone.Warn, Icons.Outlined.LockOpen)
                        null -> Unit
                    }
                    when {
                        status?.engineRunning == true -> Chip("Driving", Tone.Accent)
                        status?.isCharging == true -> Chip(chargingText(status), Tone.Accent)
                        status?.chargerConnected == true -> Chip("Plugged in")
                    }
                    for (open in openThings(status)) Chip(open, Tone.Alert)
                }
            }
        }
    }
}

internal fun chargingText(status: CarStatus): String {
    val left = status.remainingChargingTime?.takeIf { it > 0 }?.toLong()
    return if (left != null) "Charging · ${Units.duration(left)} left" else "Charging"
}

/** Whatever stands open, in the words of the web app's car card. */
private fun openThings(status: CarStatus?): List<String> {
    if (status == null) return emptyList()
    val open = ArrayList<String>()
    if (status.driverDoorOpen == true) open += "Driver door open"
    if (status.passengerDoorOpen == true) open += "Passenger door open"
    if (status.rearLeftDoorOpen == true) open += "Rear left door open"
    if (status.rearRightDoorOpen == true) open += "Rear right door open"
    if (status.trunkOpen == true) open += "Boot open"
    if (status.bonnetOpen == true) open += "Bonnet open"
    val windows = listOf(status.driverWindowOpen, status.passengerWindowOpen, status.rearLeftWindowOpen, status.rearRightWindowOpen).count { it == true }
    if (windows == 1) open += "1 window open"
    if (windows > 1) open += "$windows windows open"
    return open
}

/**
 * The cabin temperature the climate aims for. Taps are collected for a moment and sent as one
 * command, since every command wakes the car and takes it up to half a minute to answer.
 */
@Composable
private fun TemperatureRow(vm: AppViewModel, status: CarStatus?) {
    val reported = status?.remoteTemperature?.roundToInt()?.coerceIn(MIN_TEMPERATURE, MAX_TEMPERATURE)
    var wanted by remember { mutableStateOf<Int?>(null) }
    val busy = vm.pending["temperature"] == true
    val shown = wanted ?: reported ?: DEFAULT_TEMPERATURE

    LaunchedEffect(wanted) {
        val target = wanted ?: return@LaunchedEffect
        delay(1_200)
        if (target != reported) {
            vm.send("temperature", "climate-temperature", target.toString()) { it.remoteTemperature?.roundToInt() == target }
        }
        wanted = null
    }

    val inside = status?.interiorTemperature?.let { "Inside now ${it.roundToInt()} °C" }
    ListRow(Icons.Outlined.Thermostat, "Cabin temperature", inside, on = status?.climateOn == true) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            StepButton(Icons.Outlined.Remove, "Cooler", enabled = !busy && shown > MIN_TEMPERATURE) { wanted = shown - 1 }
            Text(
                "$shown°",
                color = if (busy) Volt.Info else Volt.Ink,
                fontSize = 16.sp,
                fontWeight = FontWeight.SemiBold,
                modifier = Modifier.widthIn(min = 36.dp),
                textAlign = androidx.compose.ui.text.style.TextAlign.Center,
            )
            StepButton(Icons.Outlined.Add, "Warmer", enabled = !busy && shown < MAX_TEMPERATURE) { wanted = shown + 1 }
        }
    }
}

@Composable
private fun StepButton(icon: ImageVector, label: String, enabled: Boolean, onClick: () -> Unit) {
    val shape = RoundedCornerShape(10.dp)
    Box(
        Modifier
            .size(40.dp)
            .clip(shape)
            .background(Volt.Raise)
            .border(1.dp, Volt.Line, shape)
            .clickable(enabled = enabled, role = Role.Button, onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        Icon(icon, contentDescription = label, tint = if (enabled) Volt.Ink else Volt.Ink4, modifier = Modifier.size(18.dp))
    }
}

@Composable
private fun SeatRow(vm: AppViewModel, title: String, command: String, level: Int?, read: (CarStatus) -> Int?) {
    val on = (level ?: 0) > 0
    ListRow(Icons.Outlined.EventSeat, title, "Heating", on = on) {
        Segmented(
            options = listOf(0 to "Off", 1 to "1", 2 to "2", 3 to "3"),
            selected = level ?: 0,
            modifier = Modifier.width(168.dp),
            busy = vm.pending[command] == true,
        ) { wanted ->
            vm.send(command, command, wanted.toString()) { (read(it) ?: 0) == wanted }
        }
    }
}
