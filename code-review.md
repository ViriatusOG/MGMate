# Code review: real bugs

Reviewed at `main` a5d6dff (includes PR #34 to PR #38), 2026-11-10. No code was changed.

## Summary

**One serious issue was found, in the trip code merged today.** Two trips can be given exactly the
same start time, the database refuses the second one, and from then on no trip is saved at all
until someone removes a row by hand. Nothing is lost while that lasts (the telemetry is all kept),
but it does not recover by itself. It needs a particular kind of drive, described below.

Besides that there are four low issues. None of them loses data and none lets in someone who is
not signed in.

| # | Severity | Confidence it is real | Issue |
|---|----------|-----------------------|-------|
| 1 | High | Medium | A "No route" trip and the next trip can get the same start time, which stops all trip saving for good |
| 2 | Low | High | "Edit route" on a "No route" or "Part route" trip replaces its odometer distance, and "Reset" does not bring it back |
| 3 | Low | Medium | A map layer that was refused once stays empty for that area until the map is opened again |
| 4 | Low | Medium | A connection drop at the wrong moment leaves "Live updates paused" showing until the page is reloaded |
| 5 | Low | High (harmless in size) | The live-update debounce leaks a small object when two updates arrive back to back |

Line numbers refer to the commit above.

## What was covered

- **Read closely, because it is new today:** "No route" trips (PR #34), the live check when a
  cached status read sees the engine on (PR #35), the fixes of the last review (PR #36), the
  gateway update and the steering wheel heater (PR #37), and "Part route" trips (PR #38). That is
  the trip recorder, the missed-drive recorder, the trip anchor and segmenter, the telemetry
  handler, the parked-car checks, sign-in and session handling, and the trip repository.
- **Read properly this time, after being skimmed last time:** the web front end's logic (API
  client, live-update connection, stores, command handling, push subscription, service worker,
  trip pages, map layers and popups, timers and listeners), the map, place-lookup and
  road-snapping services with their rate limiting, the data export, and the RDW sync.
- **Not read again:** the SAIC gateway's own source. PR #37 moves it from 0.14.0-rc1 to
  0.14.0-rc2 and only the wiring on MGMate's side was checked. Styling and translations were not
  reviewed, by design.
- **Left out on purpose:** what earlier rounds fixed (PR #16, PR #23), and what was declined or
  deferred (multi-vehicle, retention in Settings, backup container, metrics, languages,
  compaction). Home Assistant was removed on purpose and the app is English only on purpose.

## The nine findings of the last review

All nine were fixed in PR #36 and PR #38. Each fix was read again for this round, and none of them
was found to be wrong or to have brought a new bug with it, with one exception: finding 6 was fixed
by "Part route" trips, and the code that works out when such a trip left its parking spot is the
same code behind issue 1 below. The weakness was there before PR #38; "No route" trips (PR #34)
are what turned it into a failure.

Notes on three of the fixes, none of which is a bug:

- **Password change signs devices out (finding 8).** Works as intended. Because the login falls
  back to the MG account when `AUTH_USERNAME` and `AUTH_PASSWORD` are empty, changing the MG
  password also signs every device out of MGMate. That is the safe behaviour.
- **Re-parking after an unseen drive (finding 2).** Correct. A car moved without the odometer
  counting (tow truck, ferry) still raises the alert.
- **Follow request cooldown (finding 7).** Correct. One follow request per car per five minutes.

---

## High

### 1. Two trips with the same start time stop all trip saving

**Where**

- `src/MGMate.Core/Helpers/TripAnchor.cs:119-127` (`DepartureOver`), line 126: when the estimated
  departure is earlier than the parked fix, the departure becomes exactly the parked fix's time.
- `src/MGMate.Core/Helpers/MissedDrive.cs:60`: a "No route" trip starts exactly at the time of the
  fix that found the car at its new spot.
- `src/MGMate.Data/AppDbContext.cs:103`: a vehicle may not have two trips with the same start time
  (unique index).
- `src/MGMate.Data/Repositories/TripRepository.cs:165-206` (`SaveRecordedAsync`), called from
  `src/MGMate.Worker/Services/TripRecorder.cs:60`: nothing catches the refusal.

**What goes wrong**

A "No route" trip takes the time of the fix that found the car parked at the new spot. A trip that
is followed afterwards and anchored back to that same fix takes the departure time worked out by
`DepartureOver`. That is normally an estimate somewhere after the parked fix, but it is clamped so
that it is never earlier than the parked fix, and when the clamp applies it is exactly the parked
fix's time. Both trips then have the same `StartedAt`, to the microsecond, because both read it
from the same telemetry row.

The database refuses the second one. The trip recorder saves the trips and moves its recording
line in one transaction, so the line does not move either. Five minutes later it cuts the same
trip from the same fixes, works out the same start, and is refused again. This repeats on every
run. No trip is saved from that moment on, including the "No route" pass that runs after it.

**The scenario that triggers it**

Two drives in a row that MG does not announce, with a short stop between them:

1. The car is driven from A to B without being followed. A cached status read finds it parked at
   B at time T. That is the only fix at B. Within about five minutes a "No route" trip from A to B
   is saved, starting at T.
2. A few minutes later the car leaves B, again unannounced. A cached status read catches the
   engine on part-way, and the first fix of the followed trip is some distance from B.
3. The trip is anchored to the fix at B. The estimate is "first fix, minus the gap at 25 km/h".
   If the car covered the gap faster than 25 km/h counted over the whole time since T, the stop
   included, the estimate falls before T and is clamped to T.

Example: found parked at 08:00:00, leaves at 08:03, first followed fix at 08:07 and 3 km away in
a straight line. 3 km at 25 km/h is 7 minutes 12 seconds, so the estimate is 07:59:48, which is
before 08:00:00, and the trip is given 08:00:00. A school run or a quick stop at a shop on a
morning when MG stays silent fits this.

A second way in, without a stop: the first cached read that catches an unannounced drive finds
the car standing still (a junction, traffic lights) at least 1 km from where it was parked, and
the live poll that follows a moment later finds it moving again. The standing-still fix becomes
both the end of a "No route" trip and the anchor of the followed trip.

The "Part route" path (`ApplyUnfollowedStart`, lines 200-227) uses the same clamp and can collide
the same way.

**What it looks like**

- The Worker log shows the trip recorder failing every five minutes with a duplicate key error on
  the trips table.
- New drives still appear on the map and the Overview, because those are cut live from the fixes
  past the recording line. They never reach the trip log, and they have no anchoring, no "No
  route" or "Part route" handling and no saved energy figures.
- It stays that way across restarts. It ends only when the earlier "No route" row is removed from
  the database, or the code is fixed.

**Severity: High.** Trip saving stops completely and does not recover without manual work in the
database. No telemetry is lost, so every trip can still be saved once it is unblocked.

**Confidence: Medium.** The mechanism is certain from the code: both start times come from the
same row, the index is unique, and nothing handles the refusal. It was not reproduced, because the
.NET tests cannot be run in the review environment. What is uncertain is how often MG reports the
car in the pattern needed. This car does have unannounced morning drives, so it is not a remote
case.

---

## Low

### 2. "Edit route" changes the distance of a "No route" or "Part route" trip, and "Reset" does not restore it

**Where**

- `src/MGMate.Data/Repositories/TripRepository.cs:415-432` (`ApplyPointsAsync`), used by
  `SetPointsAsync` (line 377) and `ResetPointsAsync` (line 396).
- `src/MGMate.Core/Helpers/TripEdit.cs:77` (`Summarize`).
- `frontend/src/components/TripLogDetail.vue:822-827`: the "Edit route" button is shown for every
  trip.

**What goes wrong**

Both kinds of trip carry a distance that does not come from their points: a "No route" trip has
the odometer distance over two points, and a "Part route" trip adds the odometer distance of the
stretch nobody followed. Saving an edit, and resetting one, recompute the trip from its points
alone. So:

- A "Part route" trip loses the difference between the odometer distance of the unfollowed
  stretch and the straight line across it. "Reset" runs the same computation on the original
  points, so it does not bring the recorded distance back.
- A "No route" trip gets the straight-line distance between its two spots, and its start time
  becomes the time the car was last seen at the old spot, which can be hours earlier. It stays
  marked "No route". "Reset" leaves it like that too.

**Scenario.** Open a "No route" trip, choose "Edit route" to draw the route by hand, save. Or open
a "Part route" trip, move one point, then press "Reset".

**Effect.** The trip log itself mostly hides it, because it shows the odometer distance when both
odometer readings exist. The statistics and reports use the stored distance and show the smaller
figure. Only trips someone edits are affected.

**Severity: Low. Confidence: High.**

### 3. A map layer that was refused once stays empty for that area

**Where**

- `frontend/src/composables/poiTileLayer.ts:258`: the tile under the map centre is marked as
  loaded whatever the answer was.
- `frontend/src/composables/poiTileLayer.ts:262-272`: another round is only started when a visible
  tile is not marked as loaded.
- `src/MGMate.Core/Helpers/PoiTileFetcher.cs:12` and `src/MGMate.Api/Services/PoiService.cs`: the
  server answers "has more" with no items when Overpass is busy or backing off.

**What goes wrong**

When the place data service (Overpass) is busy or rate limited, the API answers at once with
nothing and says there is more to come. The page still marks the centre tile as loaded. A tile is
half a degree wide, so a zoomed-in view is usually one tile, and then every visible tile counts as
loaded: no retry is scheduled, and later pans inside that tile ask nothing. The comment at
`usePoiLayers.ts:229-232` says the page should back off and try again; in this case it never does.

**Scenario.** Open the map on an area that is not cached yet with fuel stations, service areas and
speed cameras all on. The three layers ask at the same moment and share one Overpass gate, which
spaces requests five seconds apart and makes a browser request wait five seconds at most, so the
last in line is refused. Those layers stay empty until the layer is switched off
and on, or the map page is opened again.

**Effect.** Missing markers only. The Worker pre-caches everything within 100 km of the car, so
this shows on a new install and when looking at places far from the car.

**Severity: Low. Confidence: Medium** (read from the code, not tried against a rate-limited
Overpass).

### 4. A connection drop between connecting and joining leaves "live updates down" switched on

**Where:** `frontend/src/composables/useSignalR.ts:165-188` (`openConnection`).

**What goes wrong**

`openConnection` starts the connection and then joins the vehicle's group. If the connection
drops between those two steps, the join fails and a restart is scheduled. But the connection
object is still there and reconnects by itself, and that path joins the group and marks the page
connected. The scheduled restart then calls `start()` on a connection that is already up, which
always fails, so the page marks itself disconnected and schedules the next restart. This repeats
every 30 seconds for as long as the page is open.

**Scenario.** The page opens on a flaky mobile connection that drops within a fraction of a second
of connecting.

**Effect.** Live updates still arrive, but the sidebar shows "Live updates paused" and the page
also fetches the stored status over HTTP once a minute, until it is reloaded. A wrong warning and
some wasted requests; the data on screen is right, and the car is never woken.

**Severity: Low. Confidence: Medium** (the start-while-connected failure is certain; the trigger
window is narrow).

### 5. The live-update debounce leaks a small object on back-to-back updates

**Where:** `src/MGMate.Api/Services/TelemetryNotificationService.cs:122-155` (`ScheduleBroadcast`),
line 154.

**What goes wrong**

Each update creates a cancellation source linked to the service's shutdown token and hands the
same token to `Task.Run`. The source is disposed in the task's `finally`. When the next update
cancels it before the task has started, the task is cancelled without ever running, the `finally`
never runs, and the source stays registered with the shutdown token until the API stops. This is
the code changed for finding 3 of the last review; the crash that finding described is fixed, and
this small leak was there before as well.

**Scenario.** Two telemetry notifications for the same car within the same instant, which a poll
produces routinely since one poll is several messages.

**Effect.** A couple of hundred bytes each time, freed when the API restarts. At a few hundred
occurrences a day that is well under a megabyte a week. It will not cause a problem between
updates of the container.

**Severity: Low. Confidence: High** that it leaks, and equally high that it does not matter in
practice. Listed because resource leaks were asked for.

---

## Looked at and found sound

Checked with a specific failure in mind, and nothing found:

- **"No route" trips counted twice.** A drive saved without a route is not saved again on a later
  pass, and a trip that is followed or anchored over the same stretch covers it.
- **The engine-on live check and the 12V battery.** The request is made once per observed start
  and at most once per five minutes. As noted last time, one wake of a car that has just parked
  remains possible.
- **Session handling.** Fixed-time comparison, the credential fingerprint, rate limits on login,
  the read-only rule for API tokens (no export, no logs, no changes), the redirect guard on
  sign-in, and the origin check.
- **Export.** Built in a temporary file that is removed when the response is sent or fails;
  session only; rate limited; values are quoted.
- **RDW sync.** Off unless `RDW_ENABLED` is set, which it is not on a UK install. With it on, a
  failed lookup is recorded and retried, and a recall is pushed once.
- **Place lookups and road snapping.** Input sizes are capped, upstream calls go through one gate
  with a back-off, and "could not ask" is never cached as "nothing there".
- **Map popups.** Every value that comes from OpenStreetMap or Open Charge Map is escaped before
  it reaches the page.
- **Front end timers and listeners.** Every interval, observer and listener that a page sets up
  is removed when the page is left.
- **Shutdown.** PostgreSQL now stops quickly, and the documented `docker run` commands give the
  container 60 seconds.
