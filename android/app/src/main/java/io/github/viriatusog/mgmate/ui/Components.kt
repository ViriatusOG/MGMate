package io.github.viriatusog.mgmate.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** A raised panel: the card every block of a page sits on. */
@Composable
fun Panel(modifier: Modifier = Modifier, content: @Composable ColumnScope.() -> Unit) {
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(20.dp),
        color = Volt.Elev,
        border = BorderStroke(1.dp, Volt.LineSoft),
    ) {
        Column(content = content)
    }
}

@Composable
fun SectionTitle(text: String, trailing: String? = null) {
    Row(Modifier.fillMaxWidth().padding(start = 2.dp, end = 2.dp, top = 6.dp), verticalAlignment = Alignment.CenterVertically) {
        Text(text.uppercase(), color = Volt.Ink3, fontSize = 12.sp, fontWeight = FontWeight.SemiBold, letterSpacing = 0.8.sp)
        Spacer(Modifier.weight(1f))
        if (trailing != null) Text(trailing, color = Volt.Ink3, fontSize = 13.sp)
    }
}

enum class Tone { Plain, Accent, Alert, Warn }

@Composable
fun Chip(text: String, tone: Tone = Tone.Plain, icon: ImageVector? = null) {
    val (ink, fill, line) = when (tone) {
        Tone.Accent -> Triple(Volt.Accent, Volt.AccentWash, Volt.AccentLine)
        Tone.Alert -> Triple(Volt.Alert, Volt.AlertWash, Volt.AlertLine)
        Tone.Warn -> Triple(Volt.Warn, Volt.WarnWash, Volt.Warn.copy(alpha = 0.36f))
        Tone.Plain -> Triple(Volt.Ink2, Volt.Raise, Volt.Line)
    }
    Row(
        Modifier.clip(CircleShape).background(fill).border(1.dp, line, CircleShape).padding(horizontal = 10.dp, vertical = 5.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(5.dp),
    ) {
        if (icon != null) Icon(icon, contentDescription = null, tint = ink, modifier = Modifier.size(14.dp))
        Text(text, color = ink, fontSize = 13.sp, fontWeight = FontWeight.Medium, maxLines = 1)
    }
}

/** The charge level as a bar, with a tick where charging will stop. */
@Composable
fun Meter(percent: Double?, target: Double? = null, modifier: Modifier = Modifier, height: Int = 8) {
    Box(modifier.fillMaxWidth().height(height.dp).clip(CircleShape).background(Volt.Raise)) {
        val level = ((percent ?: 0.0) / 100.0).coerceIn(0.0, 1.0).toFloat()
        if (level > 0f) Box(Modifier.fillMaxWidth(level).fillMaxHeight().clip(CircleShape).background(Volt.Accent))
        if (target != null && target in 1.0..99.0) {
            Row(Modifier.fillMaxWidth().fillMaxHeight()) {
                Spacer(Modifier.weight((target / 100.0).toFloat()))
                Box(Modifier.width(2.dp).fillMaxHeight().background(Volt.Ink))
                Spacer(Modifier.weight((1 - target / 100.0).toFloat()))
            }
        }
    }
}

/** One of the big buttons at the top of the Car tab. */
@Composable
fun ControlTile(
    icon: ImageVector,
    title: String,
    subtitle: String,
    modifier: Modifier = Modifier,
    on: Boolean = false,
    busy: Boolean = false,
    enabled: Boolean = true,
    onClick: () -> Unit,
) {
    val shape = RoundedCornerShape(20.dp)
    Row(
        modifier
            .heightIn(min = 64.dp)
            .clip(shape)
            .background(if (on) Volt.AccentWash else Volt.Elev)
            .border(1.dp, if (on) Volt.AccentLine else Volt.LineSoft, shape)
            .clickable(enabled = enabled && !busy, role = Role.Button, onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        if (busy) {
            CircularProgressIndicator(Modifier.size(24.dp), color = Volt.Info, strokeWidth = 2.5.dp)
        } else {
            Icon(icon, contentDescription = null, tint = if (on) Volt.Accent else if (enabled) Volt.Ink else Volt.Ink4, modifier = Modifier.size(24.dp))
        }
        Column(Modifier.weight(1f)) {
            Text(title, color = if (enabled) Volt.Ink else Volt.Ink3, fontSize = 15.sp, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
            Text(
                if (busy) "Waiting for the car" else subtitle,
                color = if (busy) Volt.Info else if (on) Volt.Accent else Volt.Ink3,
                fontSize = 12.5.sp,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
        }
    }
}

/** A row of a settings-style list: an icon tile, a title with an optional line under it, and a control. */
@Composable
fun ListRow(
    icon: ImageVector,
    title: String,
    subtitle: String? = null,
    on: Boolean = false,
    onClick: (() -> Unit)? = null,
    trailing: @Composable () -> Unit = {},
) {
    Row(
        Modifier
            .fillMaxWidth()
            .heightIn(min = 56.dp)
            .then(if (onClick != null) Modifier.clickable(role = Role.Button, onClick = onClick) else Modifier)
            .padding(horizontal = 14.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Box(
            Modifier.size(30.dp).clip(RoundedCornerShape(9.dp)).background(if (on) Volt.AccentWash else Volt.Raise),
            contentAlignment = Alignment.Center,
        ) {
            Icon(icon, contentDescription = null, tint = if (on) Volt.Accent else Volt.Ink2, modifier = Modifier.size(17.dp))
        }
        Column(Modifier.weight(1f)) {
            Text(title, color = Volt.Ink, fontSize = 15.sp, fontWeight = FontWeight.Medium)
            if (subtitle != null) Text(subtitle, color = Volt.Ink3, fontSize = 12.5.sp)
        }
        trailing()
    }
}

@Composable
fun RowDivider() {
    HorizontalDivider(Modifier.padding(start = 56.dp), color = Volt.LineSoft)
}

/** A switch that shows a spinner in its place while its command is on the way to the car. */
@Composable
fun BusySwitch(checked: Boolean, busy: Boolean, enabled: Boolean = true, onChange: (Boolean) -> Unit) {
    if (busy) {
        Box(Modifier.width(52.dp), contentAlignment = Alignment.Center) {
            CircularProgressIndicator(Modifier.size(22.dp), color = Volt.Info, strokeWidth = 2.5.dp)
        }
    } else {
        Switch(
            checked = checked,
            onCheckedChange = onChange,
            enabled = enabled,
            colors = SwitchDefaults.colors(
                checkedThumbColor = Volt.AccentInk,
                checkedTrackColor = Volt.Accent,
                uncheckedThumbColor = Volt.Ink3,
                uncheckedTrackColor = Volt.Raise,
                uncheckedBorderColor = Volt.Line,
            ),
        )
    }
}

/** A row of choices of which one is on, such as a seat's heat or the charge limit. */
@Composable
fun <T> Segmented(
    options: List<Pair<T, String>>,
    selected: T?,
    modifier: Modifier = Modifier,
    busy: Boolean = false,
    enabled: Boolean = true,
    onSelect: (T) -> Unit,
) {
    Row(
        modifier.clip(RoundedCornerShape(12.dp)).background(Volt.Raise).padding(3.dp),
        horizontalArrangement = Arrangement.spacedBy(2.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        for ((value, label) in options) {
            val isOn = value == selected
            Box(
                Modifier
                    .weight(1f)
                    .height(36.dp)
                    .clip(RoundedCornerShape(9.dp))
                    .background(if (isOn) Volt.Accent.copy(alpha = if (busy) 0.5f else 1f) else Color.Transparent)
                    .clickable(enabled = enabled && !busy && !isOn, role = Role.RadioButton) { onSelect(value) },
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    label,
                    color = if (isOn) Volt.AccentInk else if (enabled) Volt.Ink3 else Volt.Ink4,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.SemiBold,
                    maxLines = 1,
                )
            }
        }
    }
}

/** What a confirmation asks: shown as a sheet from the bottom, like the web app's dialogs. */
data class Confirm(
    val title: String,
    val text: String,
    val action: String,
    val danger: Boolean = false,
    val run: () -> Unit,
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ConfirmSheet(confirm: Confirm, onDismiss: () -> Unit) {
    ModalBottomSheet(onDismissRequest = onDismiss, containerColor = Volt.Elev, contentColor = Volt.Ink) {
        Column(Modifier.padding(horizontal = 20.dp).navigationBarsPadding().padding(bottom = 12.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(confirm.title, fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
            Text(confirm.text, color = Volt.Ink2, fontSize = 15.sp, lineHeight = 21.sp)
            Spacer(Modifier.height(4.dp))
            Button(
                onClick = {
                    onDismiss()
                    confirm.run()
                },
                modifier = Modifier.fillMaxWidth().height(50.dp),
                shape = RoundedCornerShape(14.dp),
                colors = if (confirm.danger) {
                    ButtonDefaults.buttonColors(containerColor = Volt.Alert, contentColor = Color.White)
                } else {
                    ButtonDefaults.buttonColors(containerColor = Volt.Accent, contentColor = Volt.AccentInk)
                },
            ) {
                Text(confirm.action, fontSize = 16.sp, fontWeight = FontWeight.SemiBold)
            }
            TextButton(onClick = onDismiss, modifier = Modifier.fillMaxWidth().height(48.dp)) {
                Text("Cancel", color = Volt.Ink2, fontSize = 16.sp)
            }
        }
    }
}
