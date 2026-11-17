package io.github.viriatusog.mgmate.ui

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat
import io.github.viriatusog.mgmate.data.ThemeMode

/** One set of the Volt colours: the dark one or the light one. */
class Palette(
    val dark: Boolean,
    val bg: Color,
    val elev: Color,
    val raise: Color,
    val line: Color,
    val lineSoft: Color,
    val ink: Color,
    val ink2: Color,
    val ink3: Color,
    val ink4: Color,
    val accent: Color,
    val accentInk: Color,
    val accentWash: Color,
    val accentLine: Color,
    val alert: Color,
    val alertWash: Color,
    val alertLine: Color,
    val warn: Color,
    val warnWash: Color,
    val info: Color,
)

private val DarkPalette = Palette(
    dark = true,
    bg = Color(0xFF0A0B0E),
    elev = Color(0xFF121419),
    raise = Color(0xFF191C23),
    line = Color(0xFF262A33),
    lineSoft = Color(0xFF1B1E25),
    ink = Color(0xFFF1F3F7),
    ink2 = Color(0xFFB8BFCB),
    ink3 = Color(0xFF868F9F),
    ink4 = Color(0xFF59616F),
    accent = Color(0xFFC8F94B),
    accentInk = Color(0xFF10160A),
    accentWash = Color(0x1FC8F94B),
    accentLine = Color(0x57C8F94B),
    alert = Color(0xFFFF5D66),
    alertWash = Color(0x21FF5D66),
    alertLine = Color(0x5CFF5D66),
    warn = Color(0xFFFFB03A),
    warnWash = Color(0x21FFB03A),
    info = Color(0xFF5BC8F2),
)

// The web app's light colours. The green is darker so it stays readable on white.
private val LightPalette = Palette(
    dark = false,
    bg = Color(0xFFF4F5F7),
    elev = Color(0xFFFFFFFF),
    raise = Color(0xFFEEF0F3),
    line = Color(0xFFDCDFE5),
    lineSoft = Color(0xFFE7E9ED),
    ink = Color(0xFF10131A),
    ink2 = Color(0xFF3F4652),
    ink3 = Color(0xFF6A7382),
    ink4 = Color(0xFF98A0AD),
    accent = Color(0xFF6FA90C),
    accentInk = Color(0xFFFFFFFF),
    accentWash = Color(0x1F6FA90C),
    accentLine = Color(0x666FA90C),
    alert = Color(0xFFD3252F),
    alertWash = Color(0x1AD3252F),
    alertLine = Color(0x57D3252F),
    warn = Color(0xFF9A6208),
    warnWash = Color(0x1F9A6208),
    info = Color(0xFF1A7FA8),
)

/**
 * The Volt palette of the MGMate web app's phone pages: one accent with one meaning. The colours
 * follow whichever palette is in use, and every screen that reads one redraws when it changes.
 */
object Volt {
    var palette by mutableStateOf(DarkPalette)
        internal set

    val Dark get() = palette.dark
    val Bg get() = palette.bg
    val Elev get() = palette.elev
    val Raise get() = palette.raise
    val Line get() = palette.line
    val LineSoft get() = palette.lineSoft
    val Ink get() = palette.ink
    val Ink2 get() = palette.ink2
    val Ink3 get() = palette.ink3
    val Ink4 get() = palette.ink4
    val Accent get() = palette.accent
    val AccentInk get() = palette.accentInk
    val AccentWash get() = palette.accentWash
    val AccentLine get() = palette.accentLine
    val Alert get() = palette.alert
    val AlertWash get() = palette.alertWash
    val AlertLine get() = palette.alertLine
    val Warn get() = palette.warn
    val WarnWash get() = palette.warnWash
    val Info get() = palette.info
}

private fun scheme(p: Palette) = if (p.dark) {
    darkColorScheme(
        primary = p.accent, onPrimary = p.accentInk, primaryContainer = p.accentWash, onPrimaryContainer = p.accent,
        secondary = p.accent, onSecondary = p.accentInk,
        background = p.bg, onBackground = p.ink, surface = p.bg, onSurface = p.ink,
        surfaceVariant = p.raise, onSurfaceVariant = p.ink2,
        surfaceContainer = p.elev, surfaceContainerHigh = p.elev, surfaceContainerHighest = p.raise, surfaceContainerLow = p.elev,
        outline = p.line, outlineVariant = p.lineSoft, error = p.alert, onError = Color.White,
    )
} else {
    lightColorScheme(
        primary = p.accent, onPrimary = p.accentInk, primaryContainer = p.accentWash, onPrimaryContainer = p.accent,
        secondary = p.accent, onSecondary = p.accentInk,
        background = p.bg, onBackground = p.ink, surface = p.bg, onSurface = p.ink,
        surfaceVariant = p.raise, onSurfaceVariant = p.ink2,
        surfaceContainer = p.elev, surfaceContainerHigh = p.elev, surfaceContainerHighest = p.raise, surfaceContainerLow = p.elev,
        outline = p.line, outlineVariant = p.lineSoft, error = p.alert, onError = Color.White,
    )
}

/** Dark or light as [mode] says, which by default is whatever the phone is set to. */
@Composable
fun MGMateTheme(mode: ThemeMode = ThemeMode.System, content: @Composable () -> Unit) {
    val dark = when (mode) {
        ThemeMode.System -> isSystemInDarkTheme()
        ThemeMode.Light -> false
        ThemeMode.Dark -> true
    }
    val palette = if (dark) DarkPalette else LightPalette
    if (Volt.palette !== palette) Volt.palette = palette

    // The clock and icons in the phone's own bars have to contrast with the app behind them.
    val view = LocalView.current
    SideEffect {
        val window = (view.context as? Activity)?.window ?: return@SideEffect
        val bars = WindowCompat.getInsetsController(window, view)
        bars.isAppearanceLightStatusBars = !dark
        bars.isAppearanceLightNavigationBars = !dark
    }

    MaterialTheme(colorScheme = scheme(palette), content = content)
}
