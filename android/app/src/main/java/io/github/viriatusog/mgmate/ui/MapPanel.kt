package io.github.viriatusog.mgmate.ui

import android.content.Context
import android.graphics.drawable.GradientDrawable
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.viewinterop.AndroidView
import org.osmdroid.config.Configuration
import org.osmdroid.tileprovider.tilesource.TileSourceFactory
import org.osmdroid.util.BoundingBox
import org.osmdroid.util.GeoPoint
import org.osmdroid.views.CustomZoomButtonsController
import org.osmdroid.views.MapView
import org.osmdroid.views.overlay.Marker
import org.osmdroid.views.overlay.Polyline
import org.osmdroid.views.overlay.TilesOverlay

/**
 * An OpenStreetMap map, tinted dark to sit in the app: the car as a dot, and a trip's route as a
 * line when there is one. Tiles come straight from OpenStreetMap, as in the web app's fallback.
 */
@Composable
fun MapPanel(
    modifier: Modifier = Modifier,
    car: Pair<Double, Double>? = null,
    route: List<Pair<Double, Double>> = emptyList(),
) {
    val context = LocalContext.current
    val map = remember {
        Configuration.getInstance().userAgentValue = context.packageName
        MapView(context).apply {
            setTileSource(TileSourceFactory.MAPNIK)
            setMultiTouchControls(true)
            zoomController.setVisibility(CustomZoomButtonsController.Visibility.NEVER)
            // Map tiles are drawn for a 160 dpi screen; unscaled, a phone shows them at a third of
            // the size, which reads as a map zoomed far out with labels too small to read.
            isTilesScaledToDpi = true
            minZoomLevel = 3.0
            controller.setZoom(CAR_ZOOM)
        }
    }

    DisposableEffect(map) {
        map.onResume()
        onDispose {
            map.onPause()
            map.onDetach()
        }
    }

    AndroidView(factory = { map }, modifier = modifier) { view ->
        // The street map is light as drawn; in the dark look its colours are turned over.
        view.overlayManager.tilesOverlay.setColorFilter(if (Volt.Dark) TilesOverlay.INVERT_COLORS else null)
        view.overlays.clear()

        if (route.size >= 2) {
            val points = route.map { GeoPoint(it.first, it.second) }
            view.overlays.add(
                Polyline(view).apply {
                    setPoints(points)
                    outlinePaint.color = Volt.Accent.toArgb()
                    outlinePaint.strokeWidth = 9f
                    outlinePaint.strokeCap = android.graphics.Paint.Cap.ROUND
                    outlinePaint.strokeJoin = android.graphics.Paint.Join.ROUND
                    // A line is not something to tap.
                    setOnClickListener { _, _, _ -> false }
                },
            )
            view.overlays.add(dot(view, context, points.first(), Volt.Ink.toArgb()))
            view.overlays.add(dot(view, context, points.last(), Volt.Accent.toArgb()))
            // The view has no size until it is laid out, and framing needs one.
            view.post { view.zoomToBoundingBox(BoundingBox.fromGeoPoints(points).increaseByScale(1.15f), false) }
        } else if (car != null) {
            val at = GeoPoint(car.first, car.second)
            view.overlays.add(dot(view, context, at, Volt.Accent.toArgb()))
            // Framed on the car whenever it has moved, close enough to tell the street and the
            // side of it; a redraw for anything else leaves the map where it was dragged to.
            if (view.tag != car) {
                view.tag = car
                view.controller.setZoom(CAR_ZOOM)
                view.controller.setCenter(at)
            }
        }
        view.invalidate()
    }
}

// Street level: a few hundred metres across on a phone.
private const val CAR_ZOOM = 17.5

private fun dot(map: MapView, context: Context, at: GeoPoint, colour: Int): Marker {
    val density = context.resources.displayMetrics.density
    val size = (18 * density).toInt()
    val shape = GradientDrawable().apply {
        shape = GradientDrawable.OVAL
        setColor(colour)
        setStroke((3 * density).toInt(), Volt.Bg.toArgb())
        setSize(size, size)
    }
    return Marker(map).apply {
        position = at
        icon = shape
        setAnchor(Marker.ANCHOR_CENTER, Marker.ANCHOR_CENTER)
        setOnMarkerClickListener { _, _ -> true }
    }
}
