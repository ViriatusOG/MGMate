package io.github.viriatusog.mgmate.data

/**
 * Decodes an encoded polyline at precision 6, the format the server packs a trip's route in
 * (PolylineCodec on the server, utils/polyline.ts in the web app). Returns latitude, longitude.
 */
fun decodePolyline(line: String): List<Pair<Double, Double>> {
    val points = ArrayList<Pair<Double, Double>>()
    var index = 0
    var lat = 0L
    var lng = 0L

    // One signed number: five bits a character, lowest first, the sign in the lowest bit.
    fun next(): Long? {
        var result = 0L
        var shift = 0
        while (index < line.length) {
            val chunk = line[index++].code - 63
            result = result or ((chunk and 0x1f).toLong() shl shift)
            shift += 5
            if (chunk < 0x20) return if (result and 1L != 0L) (result shr 1).inv() else result shr 1
        }
        return null
    }

    while (index < line.length) {
        lat += next() ?: break
        lng += next() ?: break
        points.add(lat / 1e6 to lng / 1e6)
    }
    return points
}
