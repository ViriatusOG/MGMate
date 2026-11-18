package io.github.viriatusog.mgmate

import io.github.viriatusog.mgmate.data.decodePolyline
import org.junit.Assert.assertEquals
import org.junit.Test

class PolylineTest {
    // Google's documented example, which at precision 6 reads a tenth of its precision 5 values.
    @Test
    fun decodesTheReferenceLine() {
        val points = decodePolyline("_p~iF~ps|U_ulLnnqC_mqNvxq`@")

        assertEquals(3, points.size)
        assertEquals(3.85, points[0].first, 1e-9)
        assertEquals(-12.02, points[0].second, 1e-9)
        assertEquals(4.07, points[1].first, 1e-9)
        assertEquals(-12.095, points[1].second, 1e-9)
        assertEquals(4.3252, points[2].first, 1e-9)
        assertEquals(-12.6453, points[2].second, 1e-9)
    }

    @Test
    fun anEmptyLineHasNoPoints() {
        assertEquals(0, decodePolyline("").size)
    }
}
