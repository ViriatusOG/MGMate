# Code review: real bugs

Reviewed at `main` 210f1cb (includes PR #35), 2026-11-10. No code was changed.

## Summary

**Nothing critical or high was found.** There are two medium issues and seven low ones. All of
them need a specific, fairly narrow situation to show up; none loses stored data and none opens
the app to someone who is not signed in.

Scope, so the result is not read as more than it is:

- Read closely: the whole Worker (MQTT consumer and handlers, trip, charge and missed-drive
  recorders, climate schedules, push checks, heartbeat, housekeeping), the API's background
  services, command path, authentication, rate limiting and health checks, and the all-in-one
  container's entrypoint and supervisord setup.
- Read for the parts MGMate depends on: the bundled SAIC gateway (upstream 0.14.0-rc1), to check
  what a cached status read publishes and how `refresh/mode/set = force` behaves.
- Skimmed only: the web front end (the live-update connection and the command composable were
  read; the pages, charts and stores were not), the map, geocoding and POI services, the data
  export and the RDW sync.
- Left out on purpose: what PR #16 and PR #23 already fixed, and the items declined or deferred
  earlier (multi-vehicle, retention in Settings, backup container, metrics, languages,
  compaction).

Line numbers refer to the commit above.

## Status of the fixes

Fixed in the same pull request that added this file, each with a test where one can be written:

| # | Status | What changed |
|---|--------|--------------|
| 1 | Fixed | A failed claim releases the car again, so the next pass picks the schedule up. |
| 2 | Fixed | When the odometer shows the car was driven to its new spot, no "moved while parked" alert goes out and the car is parked anew there. A move without the odometer counting (tow truck, ferry) still alerts. |
| 3 | Fixed | The debounce only cancels the old timer and tolerates one that was just disposed. |
| 4 | Fixed | Push requests time out after 10 seconds instead of 100, and the gateway is asked to follow a drive before the engine-start push goes out. Push delivery itself still runs in the receive path. |
| 5 | Fixed | A row takes messages for at most 30 seconds after its first one. |
| 6 | Fixed | The unfollowed start is added to the trip from the odometer, and the trip is marked "Part route" (`TripAnchor.ApplyUnfollowedStart`). |
| 7 | Fixed | At most one follow request per car every 5 minutes. A single wake of a car that has just parked is still possible. |
| 8 | Fixed | A password session carries a fingerprint of the credentials it was started with and is refused once they change. Sessions from before this fix carry none and run out on their own. |
| 9 | Partly | PostgreSQL now gets a fast shutdown (SIGINT) and the `docker run` examples pass `--stop-timeout 60`. An existing container keeps Docker's 10 seconds until it is recreated with a longer stop timeout. |

| # | Severity | Confidence it is real | Issue |
|---|----------|-----------------------|-------|
| 1 | Medium | High (rare trigger) | A database error while claiming a climate schedule stops all schedules for that car until the Worker restarts |
| 2 | Medium | Medium | A drive the gateway never followed raises a false "Car moved while parked" alert |
| 3 | Low | High (very rare trigger) | A timing race in the live-update debounce can stop the API process |
| 4 | Low | High (rare trigger) | Push delivery runs inside the MQTT receive path, so a slow push service holds up all telemetry |
| 5 | Low | Medium | The 15-second merge window slides without a cap, so closely spaced polls can collapse into one row |
| 6 | Low | Medium | The first part of a phone-key drive is dropped when the trip cannot be anchored to the parking spot |
| 7 | Low | Low | The new "follow the drive" request can wake a car that has already parked |
| 8 | Low | High | Changing the login password does not end sessions that are already signed in |
| 9 | Low | Medium | The all-in-one container has no stop grace period, so a slow shutdown kills PostgreSQL and Mosquitto mid-stop |

---

## Medium

### 1. A failed claim leaves the car marked busy, and its climate schedules stop running

- **Where:** `src/MGMate.Worker/Services/ClimateScheduleService.cs:58-62` (`RunPassAsync`)
- **What goes wrong:** the pass adds the vehicle to `_busyVehicles` (line 58) and then awaits
  `TryClaimAsync` (line 62). The vehicle is only removed again on the "not claimed or missed"
  path (line 66) or in `RunInBackground`'s `finally`. If `TryClaimAsync` throws anything other
  than `DbUpdateConcurrencyException` (which the repository catches), the exception leaves
  `RunPassAsync` with the vehicle still in the set. `PeriodicBackgroundService` logs the failed
  pass and carries on, but every later pass hits `TryAdd` returning false and skips the car.
- **Trigger:** a schedule becomes due at a moment the database call fails: PostgreSQL restarting,
  a dropped connection, a command timeout. The schedule is still due and still enabled, yet it
  never runs, and neither does any other schedule for that car, until the Worker is restarted.
  Nothing is logged after the first error, and the schedule's "last run" on the page does not
  change, so it fails silently. After more than ten minutes the eventual restart records it as
  missed.
- **Severity:** medium. The car is simply not pre-heated, with no notice, for as long as the
  Worker stays up (which can be weeks).
- **Confidence:** high that the defect is real (there is no `try`/`finally` around the claim);
  the trigger itself is rare because it needs a database error in a 15-second window around a
  due time.

### 2. A drive nobody followed is reported as "Car moved while parked"

- **Where:** `src/MGMate.Worker/Services/PushNotificationCheckService.cs:131` and `:158-168`
  (`CheckParkedAsync`, `CheckMovedWhileParked`); the parking spot is only written at
  `src/MGMate.Worker/Mqtt/TelemetryHandler.cs:139` and `:175`.
- **What goes wrong:** the parked position (`ParkedLatitude`/`ParkedLongitude`) is stored only
  when the Worker sees the engine go from on to off. The moved-while-parked check then compares
  that spot with the newest position whenever the newest engine reading is "off". A drive during
  which the Worker never saw the engine on (the same drives PR #34 saves as "No route" trips)
  ends with the car somewhere else, engine off, and the old parking spot still stored. If that
  is more than 1.5 km away the check fires: "Your car is X km from where it was parked, with the
  engine off."
- **Trigger:** a start with the phone's digital key where MG sends no "vehicle started" message
  and none of the cached status reads during the drive shows the engine on (MG's cached copy
  only changes when the car reports in, so a short drive can pass between two reports). The next
  cached read at the destination then delivers the new position with the engine off. The alert
  goes out once per such drive (it is held back by `onceSince: vehicle.LastParkedAt` afterwards).
  The stored parking spot also stays wrong until the next drive that is followed, so the
  10-minute "just parked" grace for open doors and windows does not apply at the new spot.
- **Severity:** medium. It reads like a theft or tow alert for an ordinary drive.
- **Confidence:** medium. The code path is certain; how often MG's cached status skips the
  engine-on state on a real drive is not something the code can tell. PR #35's 2-minute read
  interval makes it less likely than before, not impossible.

---

## Low

### 3. Cancelling a debounce timer that has just been disposed can stop the API

- **Where:** `src/MGMate.Api/Services/TelemetryNotificationService.cs:124-127` and `:143-144`
  (`ScheduleBroadcast`)
- **What goes wrong:** the reader loop removes the pending `CancellationTokenSource` for a
  vehicle and then calls `old.Cancel()`. The background task that owns that source disposes it in
  its `finally`. If the task reaches `cts.Dispose()` between the loop's `TryRemove` and its
  `Cancel()`, `Cancel()` throws `ObjectDisposedException`. That exception escapes the
  `await foreach` in `ExecuteAsync`, the hosted service faults, and .NET's default behaviour for a
  faulted `BackgroundService` is to stop the host.
- **Trigger:** a `telemetry_updated` notification arrives in the same instant the previous
  broadcast for that vehicle finishes (2 seconds after the last message of a poll). The window is
  microseconds wide.
- **Effect:** the API process exits; supervisord or Docker restarts it within seconds, and the
  page reconnects on its own.
- **Severity:** low. **Confidence:** high that the race exists, low that it is ever hit in
  practice.

### 4. Push delivery is awaited inside the MQTT receive path

- **Where:** `src/MGMate.Worker/Mqtt/TelemetryHandler.cs:261` (engine-start push, awaited before
  the follow request on line 125), `src/MGMate.Worker/Mqtt/VehicleMessageHandler.cs` (MG app
  messages), `src/MGMate.Worker/Mqtt/AccountStatusHandler.cs` (sign-in alert) and
  `src/MGMate.Worker/Mqtt/PlaceAlerts.cs`; the delivery itself is
  `src/MGMate.Worker/Services/PushSenderService.cs:116`. The HTTP client is registered at
  `src/MGMate.Worker/Program.cs:34` with no timeout of its own, so the default of 100 seconds
  applies.
- **What goes wrong:** MQTT messages are handled one at a time, and these handlers wait for
  `SendToAllAsync`, which waits for every subscribed device's push request. A push service that
  accepts the connection but does not answer holds the whole receive path for up to 100 seconds.
  Telemetry is stamped with the time it is processed (`TelemetryHandler.cs:117`), so every
  message queued behind it gets a late timestamp, several polls fold into one row, and the
  `refresh/mode/set = force` request that makes the gateway follow a phone-key drive is sent
  that much later.
- **Trigger:** an engine start (or MG app message, or arrival/departure alert) at a moment one
  device's push service (Google, Mozilla, Apple) is slow or unreachable.
- **Severity:** low. **Confidence:** high that it blocks as described; the trigger depends on an
  outside service being slow.

### 5. The merge window has no upper limit

- **Where:** `src/MGMate.Worker/Mqtt/TelemetryHandler.cs:195-218` (`MergeOrAddTelemetryAsync`),
  line 206 in particular
- **What goes wrong:** a message merges into the current row when it arrives within 15 seconds of
  the previous *message*, and each merge moves that reference time forward. The row keeps the
  timestamp of its first message. As long as messages keep arriving less than 15 seconds apart,
  they all land in the same row, last value wins, however long that goes on.
- **Trigger:** two polls closer together than 15 seconds end to end: the extra live poll PR #35
  now requests right behind a cached read or a regular poll, a command's follow-up refresh, or
  the backlog after the stall in issue 4. Two positions then become one fix and the earlier one
  is lost from the route; a value can also land in a row stamped up to a poll earlier than it was
  read. Late additions to an old row can fall behind the trip recorder's one-minute settle time
  (`TripRecorder.SettleTime`) and be missed by it.
- **Severity:** low (a lost fix or a slightly wrong timestamp, not lost trips).
- **Confidence:** medium. In steady 30-second polling the gap between polls is long enough; the
  overlap cases above are occasional.

### 6. The unfollowed start of a drive is dropped when the trip cannot be anchored

- **Where:** `src/MGMate.Core/Helpers/MissedDrive.cs:34` (pairs with a moving fix are skipped) and
  `src/MGMate.Core/Helpers/TripAnchor.cs:20` and `:122-128` (`MaxGapKm`, `OdometerAgrees`)
- **What goes wrong:** since PR #35 a phone-key drive is followed from the moment a status read
  shows the engine on, so its first fix is usually a moving one some way down the road. The
  stretch from the parking spot to that fix is only recovered by `TripAnchor`, which refuses when
  the straight-line gap is over 30 km or the odometer moved more than three times the gap (a
  detour, or a winding road). `MissedDrive.Candidates` cannot pick the stretch up either, because
  it only pairs two standing-still fixes. The distance and energy of that first stretch then
  appear in no trip.
- **Trigger:** a phone-key drive first noticed late (MG's cached status did not change for a
  while) or on an indirect route.
- **Severity:** low (totals understate; the odometer readings on the trip still show the truth).
- **Confidence:** medium. The gap in the logic is certain; how often a drive is noticed that late
  with 2-minute reads is not.

### 7. The follow request can wake a car that is already parked

- **Where:** `src/MGMate.Worker/Mqtt/TelemetryHandler.cs:124-125` and `:228-240`
  (`FollowTheDriveAsync`)
- **What goes wrong:** any off-to-on change of `drivetrain/running` makes the Worker publish
  `refresh/mode/set = force`, and the gateway answers a forced refresh with a live poll, which
  wakes the car. The change can come from a cached read, which reports what the car last told MG,
  not what it is doing now. There is no cooldown on the request (the engine-start push has one,
  the request does not).
- **Trigger:** a short drive: the car reports "engine on" to MG, parks and goes to sleep, and the
  next cached read (up to 2 minutes later) delivers that last report. The Worker asks for a live
  poll and the sleeping car is woken once. One wake per such drive, not a loop: the gateway only
  publishes a cached status that is newer than its last live one.
- **Severity:** low (one extra wake of the 12V system per occurrence).
- **Confidence:** low. It depends on the car's last report before sleeping still saying "engine
  on", which the code cannot show either way.

### 8. A password change leaves existing sessions signed in

- **Where:** `src/MGMate.Api/Endpoints/AuthEndpoints.cs:103-144` (login) and
  `src/MGMate.Api/Authentication/AuthenticationSetup.cs:118-132` (`OnValidatePrincipal`)
- **What goes wrong:** a session cookie is checked against the revocation list only, and a session
  gets on that list only through its own logout. Nothing ties a session to the password it was
  created with. After `AUTH_PASSWORD` (or the MG account password it falls back to) is changed and
  the container restarted, every cookie issued before stays valid until it expires: up to 30 days
  with "remember me". The key ring is persisted on purpose, so a restart does not end them either.
- **Trigger:** changing the password because a device was lost or the password leaked. The lost
  device stays signed in and can still unlock the car and read its position.
- **Severity:** low for a single-user install on a LAN, but it is the one case where the password
  change is expected to lock someone out. **Confidence:** high.
- **Workaround today:** deleting the files in the data-protection key folder
  (`/data/dataprotection`) and restarting signs every device out.

### 9. No stop grace period for the all-in-one container

- **Where:** `docker/all-in-one/supervisord.conf:30` (`stopwaitsecs=60` for PostgreSQL) against
  the compose files, none of which sets `stop_grace_period` (`docker-compose.yml`, and the ZimaOS
  compose file used on the NAS)
- **What goes wrong:** `docker stop` (and the recreate behind every update) gives the container
  10 seconds before it kills everything. supervisord stops its programs one after another in
  reverse start order: nginx, Worker, API, gateway, Mosquitto, and PostgreSQL last. If the ones
  in front take more than about 10 seconds together (a .NET host can take up to 30 seconds to
  drain; the Worker may be in the middle of a push delivery or a database write), PostgreSQL and
  Mosquitto are killed without a clean stop.
- **Effect:** PostgreSQL recovers from its write-ahead log on the next start, so stored data is
  safe, but the start is slower and the log shows a crash recovery. Mosquitto only writes its
  retained messages and sessions to disk every 30 minutes or on a clean stop, so a kill loses the
  newest retained values until the gateway publishes them again. A climate schedule that was
  mid-run is recorded as unconfirmed.
- **Severity:** low. **Confidence:** medium: the 10 seconds against a sequential stop of six
  programs is certain; whether a given stop overruns depends on what the services are doing.

---

## Checked and found sound

These are the areas the request asked about where no bug was found, so their absence above is a
result and not an omission:

- **MQTT reconnect and shutdown** in the Worker and the API's two clients: the disconnect handler
  is added and removed per attempt, a failed connect retries, and cancellation ends the loops
  cleanly.
- **PostgreSQL LISTEN loop** in the API: reconnects after an error; a lost `command_result` falls
  back to the gate's 45-second hold, and a lost telemetry push is covered by the page's catch-up
  read.
- **Vehicle command gate**: released on publish failure, on the gateway's answer, and on timeout;
  a schedule's request cannot be published after the Worker has given up on it.
- **Trip and charge recorders**: trips and the recording line are saved in one transaction; a
  restart mid-run resumes from the line without saving a trip twice. PR #35's change to the
  missed-drive overlap check does what it says for both directions.
- **Climate schedule times across clock changes**: a time in the skipped hour and one in the
  repeated hour both resolve to a single instant.
- **Authentication**: fixed-time comparison of credentials, login rate limit with a per-device
  backstop, session revocation on logout, read-only API tokens kept away from the export, the
  logs and token management.
- **Push endpoints**: only public addresses are connected to, re-checked on every connection.
- **Photo upload**: size capped while reading, type detected from the content.
- **Debug page commands**: only commands the gateway itself announced, with values checked
  against the announced limits, through the same gate as every other command.
- **Front end live updates**: timers and the connection are released on stop and on unmount;
  a stale start cannot resurrect a stopped connection.
