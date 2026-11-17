package io.github.viriatusog.mgmate.data

import java.time.Duration
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale
import kotlin.math.roundToInt

/** UK units throughout: the server stores km, the app shows miles. */
object Units {
    private const val MILES_PER_KM = 0.621371

    fun miles(km: Double): Double = km * MILES_PER_KM

    fun wholeMiles(km: Double?): String? = km?.let { miles(it).roundToInt().toString() }

    fun milesText(km: Double): String = String.format(Locale.UK, "%.1f mi", miles(km))

    fun mph(kmh: Double): Int = miles(kmh).roundToInt()

    fun percent(value: Double?): String? = value?.let { "${it.roundToInt()}%" }

    /** "45 min" or "5 h 51 min". */
    fun duration(seconds: Long): String {
        val minutes = ((seconds + 30) / 60).coerceAtLeast(0)
        return if (minutes < 60) "$minutes min" else "${minutes / 60} h ${minutes % 60} min"
    }

    fun parseInstant(text: String?): Instant? = try {
        when {
            text == null -> null
            // The server writes UTC times, some without the trailing Z.
            text.endsWith("Z") || text.contains('+') || text.lastIndexOf('-') > 10 ->
                java.time.OffsetDateTime.parse(text).toInstant()
            else -> Instant.parse(text + "Z")
        }
    } catch (_: Exception) {
        null
    }

    private val clock = DateTimeFormatter.ofPattern("HH:mm", Locale.UK)
    private val dayName = DateTimeFormatter.ofPattern("EEE d MMM", Locale.UK)

    fun time(instant: Instant): String = clock.format(instant.atZone(ZoneId.systemDefault()))

    fun day(instant: Instant): LocalDate = instant.atZone(ZoneId.systemDefault()).toLocalDate()

    fun dayLabel(day: LocalDate, today: LocalDate = LocalDate.now()): String = when (day) {
        today -> "Today"
        today.minusDays(1) -> "Yesterday"
        else -> dayName.format(day)
    }

    /** "just now", "5 min ago", "3 h ago", "2 d ago". */
    fun ago(instant: Instant, now: Instant = Instant.now()): String {
        val minutes = Duration.between(instant, now).toMinutes()
        return when {
            minutes < 1 -> "just now"
            minutes < 60 -> "$minutes min ago"
            minutes < 48 * 60 -> "${minutes / 60} h ago"
            else -> "${minutes / (24 * 60)} d ago"
        }
    }
}
