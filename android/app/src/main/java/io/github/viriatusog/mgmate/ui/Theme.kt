package io.github.viriatusog.mgmate.ui

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

/** The Volt palette of the MGMate web app's phone pages: dark first, one accent with one meaning. */
object Volt {
    val Bg = Color(0xFF0A0B0E)
    val Elev = Color(0xFF121419)
    val Raise = Color(0xFF191C23)
    val Line = Color(0xFF262A33)
    val LineSoft = Color(0xFF1B1E25)
    val Ink = Color(0xFFF1F3F7)
    val Ink2 = Color(0xFFB8BFCB)
    val Ink3 = Color(0xFF868F9F)
    val Ink4 = Color(0xFF59616F)
    val Accent = Color(0xFFC8F94B)
    val AccentInk = Color(0xFF10160A)
    val AccentWash = Color(0x1FC8F94B)
    val AccentLine = Color(0x57C8F94B)
    val Alert = Color(0xFFFF5D66)
    val AlertWash = Color(0x21FF5D66)
    val AlertLine = Color(0x5CFF5D66)
    val Warn = Color(0xFFFFB03A)
    val WarnWash = Color(0x21FFB03A)
    val Info = Color(0xFF5BC8F2)
}

private val Scheme = darkColorScheme(
    primary = Volt.Accent,
    onPrimary = Volt.AccentInk,
    primaryContainer = Volt.AccentWash,
    onPrimaryContainer = Volt.Accent,
    secondary = Volt.Accent,
    onSecondary = Volt.AccentInk,
    background = Volt.Bg,
    onBackground = Volt.Ink,
    surface = Volt.Bg,
    onSurface = Volt.Ink,
    surfaceVariant = Volt.Raise,
    onSurfaceVariant = Volt.Ink2,
    surfaceContainer = Volt.Elev,
    surfaceContainerHigh = Volt.Elev,
    surfaceContainerHighest = Volt.Raise,
    surfaceContainerLow = Volt.Elev,
    outline = Volt.Line,
    outlineVariant = Volt.LineSoft,
    error = Volt.Alert,
    onError = Color.White,
)

@Composable
fun MGMateTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = Scheme, content = content)
}
