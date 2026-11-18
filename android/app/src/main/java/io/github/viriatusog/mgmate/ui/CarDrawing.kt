package io.github.viriatusog.mgmate.ui

import androidx.compose.foundation.Canvas
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.scale
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.graphics.vector.PathParser
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics

// MG's Camden Grey, the same two tones as the web app's swatch.
private val BodyDark = Color(0xFF383E46)
private val BodyLight = Color(0xFF5D656E)
private val Roof = Color(0xFF20242C)
private val Rear = Color(0xFF2B3038)
private val Lamp = Color(0xFFDDE6C8)
private val Tail = Color(0xFFE5484D)

/** The MG4 from above, nose up, with the charge cable at the rear-left port while plugged in. */
@Composable
fun CarDrawing(modifier: Modifier = Modifier, pluggedIn: Boolean = false, charging: Boolean = false) {
    val shapes = remember {
        CarShapes.paths.map { (part, data) -> part to PathParser().parsePathString(data).toPath() }
    }
    val cable = remember {
        Path().apply {
            moveTo(24f, 308f)
            cubicTo(8f, 308f, -6f, 320f, -12f, 344f)
        }
    }
    Canvas(modifier.semantics { contentDescription = "Your car from above" }) {
        val factor = minOf(size.width / CarShapes.WIDTH, size.height / CarShapes.HEIGHT)
        val left = (size.width - CarShapes.WIDTH * factor) / 2f
        val top = (size.height - CarShapes.HEIGHT * factor) / 2f
        translate(left, top) {
            scale(factor, factor, pivot = Offset.Zero) {
                translate(-CarShapes.MIN_X, 0f) {
                    val body = Brush.horizontalGradient(listOf(BodyDark, BodyLight, BodyDark), startX = 22f, endX = 178f)
                    for ((part, path) in shapes) {
                        when (part) {
                            "mirror" -> drawPath(path, BodyDark)
                            "body" -> drawPath(path, body)
                            "lamp" -> drawPath(path, Lamp)
                            "roof" -> drawPath(path, Roof)
                            "rear" -> drawPath(path, Rear)
                            "tail" -> drawPath(path, Tail)
                        }
                    }
                    if (pluggedIn) {
                        val colour = if (charging) Volt.Accent else Volt.Ink3
                        drawPath(cable, colour, style = Stroke(width = 4f, cap = StrokeCap.Round))
                        drawCircle(colour, radius = 5f, center = Offset(-12f, 344f))
                    }
                }
            }
        }
    }
}
