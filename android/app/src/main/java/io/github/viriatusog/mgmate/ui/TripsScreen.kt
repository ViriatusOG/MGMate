package io.github.viriatusog.mgmate.ui

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.ChevronRight
import androidx.compose.material.icons.outlined.NearMe
import androidx.compose.material.icons.outlined.Route
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.viriatusog.mgmate.AppViewModel
import io.github.viriatusog.mgmate.OpenTrip
import io.github.viriatusog.mgmate.data.Place
import io.github.viriatusog.mgmate.data.TripLogEntry
import io.github.viriatusog.mgmate.data.Units
import java.time.Duration
import java.time.LocalDate
import java.util.Locale

/** The Trips tab: where the car is, then the last 30 days of trips; a trip opens on its route. */
@Composable
fun TripsScreen(vm: AppViewModel) {
    val open = vm.openTrip
    if (open != null) {
        BackHandler { vm.closeTrip() }
        TripDetail(open, onBack = vm::closeTrip)
        return
    }

    val status = vm.status
    val trips = vm.trips
    val position = remember(status?.latitude, status?.longitude) {
        val lat = status?.latitude
        val lng = status?.longitude
        if (lat != null && lng != null && (lat != 0.0 || lng != 0.0)) lat to lng else null
    }
    val context = LocalContext.current

    Column(Modifier.fillMaxSize().padding(horizontal = 16.dp).padding(top = 8.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Panel {
            if (position != null) {
                MapPanel(Modifier.fillMaxWidth().height(200.dp), car = position)
            } else {
                Box(Modifier.fillMaxWidth().height(90.dp), contentAlignment = Alignment.Center) {
                    Text("The car has not reported where it is yet.", color = Volt.Ink3, fontSize = 14.sp)
                }
            }
            Row(Modifier.padding(horizontal = 14.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    val driving = status?.engineRunning == true
                    val last = trips?.firstOrNull()
                    val where = if (driving) null else placeName(last?.endPlace)
                    Text(
                        when {
                            driving -> "Driving"
                            where != null -> "Parked at $where"
                            else -> "Parked"
                        },
                        color = Volt.Ink,
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Medium,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                    val since = if (driving) null else Units.parseInstant(last?.endedAt)
                    val facts = listOfNotNull(
                        since?.let { "Since ${Units.dayLabel(Units.day(it)).lowercase(Locale.UK)} ${Units.time(it)}" },
                        when (status?.isLocked) {
                            true -> "locked"
                            false -> "unlocked"
                            null -> null
                        },
                    )
                    if (facts.isNotEmpty()) Text(facts.joinToString(" · "), color = Volt.Ink3, fontSize = 12.5.sp)
                }
                if (position != null) {
                    Button(
                        onClick = { directions(context, position) },
                        shape = RoundedCornerShape(10.dp),
                        contentPadding = PaddingValues(horizontal = 12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = Volt.Accent, contentColor = Volt.AccentInk),
                    ) {
                        Icon(Icons.Outlined.NearMe, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(Modifier.width(6.dp))
                        Text("Directions", fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                    }
                }
            }
        }

        when {
            trips == null -> Text("Loading trips", color = Volt.Ink3, fontSize = 14.sp, modifier = Modifier.padding(4.dp))
            trips.isEmpty() -> Text("No trips in the last 30 days.", color = Volt.Ink3, fontSize = 14.sp, modifier = Modifier.padding(4.dp))
            else -> {
                val days = remember(trips) {
                    trips.groupBy { Units.parseInstant(it.startedAt)?.let(Units::day) ?: LocalDate.MIN }.toList()
                }
                LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp), contentPadding = PaddingValues(bottom = 16.dp)) {
                    items(days, key = { it.first.toString() }) { (day, ofDay) ->
                        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            val total = ofDay.sumOf { tripKm(it) }
                            val count = if (ofDay.size == 1) "1 trip" else "${ofDay.size} trips"
                            SectionTitle(Units.dayLabel(day), "$count · ${Units.milesText(total)}")
                            Panel {
                                ofDay.forEachIndexed { index, trip ->
                                    if (index > 0) RowDivider()
                                    TripRow(trip) { vm.showTrip(trip) }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun TripRow(trip: TripLogEntry, onClick: () -> Unit) {
    Column(Modifier.fillMaxWidth().clickable(role = Role.Button, onClick = onClick).padding(horizontal = 14.dp, vertical = 10.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            Icon(Icons.Outlined.Route, contentDescription = null, tint = Volt.Ink2, modifier = Modifier.size(20.dp))
            Column(Modifier.weight(1f)) {
                Text(timeLine(trip), color = Volt.Ink, fontSize = 15.sp, fontWeight = FontWeight.Medium, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text(placesLine(trip), color = Volt.Ink3, fontSize = 12.5.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
            }
            Column(horizontalAlignment = Alignment.End) {
                Text(Units.milesText(tripKm(trip)), color = Volt.Ink, fontSize = 15.sp, fontWeight = FontWeight.Bold)
                efficiency(trip)?.let { Text(it, color = Volt.Ink3, fontSize = 12.5.sp) }
            }
            Icon(Icons.Outlined.ChevronRight, contentDescription = null, tint = Volt.Ink4, modifier = Modifier.size(18.dp))
        }
        val from = trip.socStartPercent
        val to = trip.socEndPercent
        val flag = routeFlag(trip)
        if ((from != null && to != null) || flag != null) {
            Row(Modifier.padding(start = 32.dp, top = 6.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(9.dp)) {
                if (from != null && to != null) {
                    Meter(to, from, Modifier.width(84.dp), height = 5)
                    Text("${Units.percent(from)} → ${Units.percent(to)}", color = Volt.Ink3, fontSize = 12.5.sp)
                }
                if (flag != null) Text(flag, color = Volt.Warn, fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
            }
        }
    }
}

@Composable
private fun TripDetail(open: OpenTrip, onBack: () -> Unit) {
    val trip = open.entry
    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(horizontal = 16.dp).padding(bottom = 16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Outlined.ArrowBack, contentDescription = "Back to trips", tint = Volt.Ink)
            }
            Column {
                Text(placesLine(trip), color = Volt.Ink, fontSize = 18.sp, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                val day = Units.parseInstant(trip.startedAt)?.let { Units.dayLabel(Units.day(it)) }
                Text(listOfNotNull(day, timeLine(trip)).joinToString(" "), color = Volt.Ink3, fontSize = 13.sp)
            }
        }

        val route = open.route
        val shape = RoundedCornerShape(14.dp)
        when {
            route == null -> Box(Modifier.fillMaxWidth().height(300.dp), contentAlignment = Alignment.Center) {
                Text("Loading the route", color = Volt.Ink3, fontSize = 14.sp)
            }
            route.size >= 2 && trip.leftAfter == null -> MapPanel(Modifier.fillMaxWidth().height(300.dp).clip(shape), route = route)
            else -> Panel {
                Text(
                    "MGMate could not follow this drive, so it has no route. The distance comes from the odometer.",
                    color = Volt.Ink3,
                    fontSize = 14.sp,
                    modifier = Modifier.padding(14.dp),
                )
            }
        }

        val figures = listOfNotNull(
            "Distance" to Units.milesText(tripKm(trip)),
            durationText(trip)?.let { "Time" to it },
            efficiency(trip)?.let { "Efficiency" to it },
            if (trip.socStartPercent != null && trip.socEndPercent != null) {
                "Battery" to "${Units.percent(trip.socStartPercent)} → ${Units.percent(trip.socEndPercent)}"
            } else {
                null
            },
            trip.energyCounterUsed?.takeIf { it > 0 }?.let { "Energy" to String.format(Locale.UK, "%.1f kWh", it) },
            trip.avgMovingSpeedKmh?.let { "Average speed" to "${Units.mph(it)} mph" },
            trip.maxSpeedKmh?.let { "Top speed" to "${Units.mph(it)} mph" },
        )
        for (pair in figures.chunked(2)) {
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                for ((label, value) in pair) {
                    Panel(Modifier.weight(1f)) {
                        Column(Modifier.padding(horizontal = 14.dp, vertical = 10.dp)) {
                            Text(label, color = Volt.Ink3, fontSize = 12.5.sp)
                            Text(value, color = Volt.Ink, fontSize = 19.sp, fontWeight = FontWeight.SemiBold)
                        }
                    }
                }
                if (pair.size == 1) Spacer(Modifier.weight(1f))
            }
        }
    }
}

/** The odometer's figure when the trip has one: GPS fixes cut corners. */
internal fun tripKm(trip: TripLogEntry): Double {
    val start = trip.odometerStartKm
    val end = trip.odometerEndKm
    return if (start != null && end != null && end > start) end - start else trip.distanceKm
}

private fun seconds(trip: TripLogEntry): Long? {
    val start = Units.parseInstant(trip.startedAt) ?: return null
    val end = Units.parseInstant(trip.endedAt) ?: return null
    return Duration.between(start, end).seconds.takeIf { it > 0 }
}

private fun durationText(trip: TripLogEntry): String? =
    if (trip.leftAfter != null) null else seconds(trip)?.let(Units::duration)

private fun timeLine(trip: TripLogEntry): String {
    val end = Units.parseInstant(trip.endedAt)?.let(Units::time) ?: "?"
    // A drive nobody followed is only known by when the car was found again.
    if (trip.leftAfter != null) return "? → $end"
    val start = Units.parseInstant(trip.startedAt)?.let(Units::time) ?: "?"
    return listOfNotNull("$start → $end", durationText(trip)).joinToString(" · ")
}

private fun placeName(place: Place?): String? =
    place?.road?.takeIf { it.isNotBlank() } ?: place?.city?.takeIf { it.isNotBlank() }

private fun placesLine(trip: TripLogEntry): String {
    val from = placeName(trip.startPlace)
    val to = placeName(trip.endPlace)
    return when {
        from != null && to != null -> "$from → $to"
        to != null -> "To $to"
        from != null -> "From $from"
        else -> "Trip"
    }
}

private fun efficiency(trip: TripLogEntry): String? {
    val kwh = trip.energyCounterUsed?.takeIf { it > 0.05 } ?: return null
    val miles = Units.miles(tripKm(trip))
    if (miles < 0.5) return null
    return String.format(Locale.UK, "%.1f mi/kWh", miles / kwh)
}

private fun routeFlag(trip: TripLogEntry): String? = when {
    trip.leftAfter != null -> "No route"
    (trip.unfollowedKm ?: 0.0) > 0 -> "Part route"
    else -> null
}

/** Hands the car's position to the phone's maps app, which offers the way there. */
private fun directions(context: Context, position: Pair<Double, Double>) {
    val (lat, lng) = position
    val uri = Uri.parse("geo:$lat,$lng?q=$lat,$lng(${Uri.encode("My car")})")
    try {
        context.startActivity(Intent.createChooser(Intent(Intent.ACTION_VIEW, uri), "Open in"))
    } catch (_: ActivityNotFoundException) {
        // No maps app installed: nothing to open.
    }
}
