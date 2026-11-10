# MGMate

**A self-hosted dashboard, trip and charge log, and reports for your MG.**

[![Latest release](https://img.shields.io/github/v/release/ViriatusOG/MGMate)](https://github.com/ViriatusOG/MGMate/releases/latest)
[![Docker image build](https://github.com/ViriatusOG/MGMate/actions/workflows/docker-publish.yml/badge.svg?branch=main)](https://github.com/ViriatusOG/MGMate/actions/workflows/docker-publish.yml)
[![MIT licence](https://img.shields.io/github/license/ViriatusOG/MGMate)](LICENSE)

![The MGMate overview: the battery with its range and a 24-hour charge level chart, the car on the road with its tyre pressures and speed, the car status listed under it, the last charge with its power curve, where the car is and the latest trips](documentation/screenshots/desktop-dashboard.webp)

MGMate is a free, open-source web app for **modern MG cars**, the ones built by SAIC Motor such as the MG4, MG5, ZS EV and HS PHEV. It signs in to the SAIC iSmart API, the same backend the official MG iSmart app uses, records everything the car reports, and turns it into a logbook you can actually use, on a desktop or a phone:

- **See the car at a glance.** Battery, range and the last 24 hours of charge level, the last charge with its power curve, and one Overview card with a drawing of the car from above: the tyre pressures at each wheel, which doors, windows, boot or bonnet are open, whether it is locked and whether the cable is in, and the same in a list underneath. While the car charges, the battery card turns into a live charging card with the power, voltage, current and target.
- **Every trip, logged.** Trips are saved automatically with the energy they used, mi/kWh, driving and stopped time, the outside temperature and the battery they started and ended on, drawn on the map along the roads you took. A drive MG never announced (a start with the phone's digital key, for one) is picked up as soon as a status read sees the engine on and followed from there; the start it missed is added from the odometer, as a **Part route** trip; one that ends before that is still logged from the odometer and the battery, as a trip without a route.
- **Every charge, logged.** Each plug-in becomes a charge session: energy added, AC or DC, peak power, how long it took, where it was, and what it cost at your electricity prices, with what came from the wall next to what reached the battery.
- **Reports, month by month.** Distance, trips, driving time, consumption, energy charged and charging cost for each month, against the month before, with a 12-month chart and a yearly table.
- **Know your battery.** Battery health works out the usable capacity from your charges, compares it with the pack when new, and shows the trend over time. It waits for a charge to 100 % first, because that is when the car knows its charge level exactly. The 12V battery, temperatures, distance and range get charts of their own on Statistics.
- **Keep a trip log for the taxman.** Mark trips as business, commute or private, add notes, search them, and export a month or a year as a spreadsheet.
- **Control it from anywhere.** Pre-condition the cabin, lock the car or flash its lights to find it, and see whether the car actually carried the command out, without waking the car more than it needs.
- **See everything the car reports.** Switch on Advanced in Settings and a Debug page lists every sensor and control the MG gateway announces, with its latest value and whether MGMate keeps its history.
- **Hear about it when something is off.** Push notifications for low tyre pressure, a car left unlocked, a window left open, charging complete, maintenance coming due and more.
- **Keep it yours.** Everything runs in Docker on your own hardware, as one all-in-one container or a Compose stack. No subscription, and no account needed beyond your MG login.

> **Note:** MGMate only works with the current MG brand owned by SAIC Motor. It is **not** compatible with classic British-built MG cars (MGB, Midget, MGF, etc.) produced before SAIC's acquisition of the brand. If your car does not use the MG iSmart app, MGMate will not work with it.

## Screenshots

The screenshots use the demo's sample car and trips.

### While charging

The battery card becomes a live charging card, and the Overview card shows the cable in, an open boot and an open window, on the drawing and in its list:

![The overview while the car charges at home: 16.2 kW at 386 V and 42 A towards an 80 % target, the car from above with the charging cable, and the list under it naming the open window and boot](documentation/screenshots/desktop-dashboard-charging.webp)

### Trips

Every trip of the month, grouped by day, with a calendar to jump around, search, and totals for what you picked:

![The Trips page: totals for distance, trips, consumption, energy and cost, a month calendar with the days driven, and each trip with its places, times, battery used and mi/kWh](documentation/screenshots/desktop-trips.webp)

### Charges

Every charge, with the energy added, how long it took, AC or DC, and where:

![The Charges page: totals for energy added, average duration and cost, a month calendar with the charging days, and each charge with its times, battery before and after, peak power and kWh added](documentation/screenshots/desktop-charges.webp)

### Reports

![The Reports page: the month's distance, trips, driving time, days driven, consumption, energy, charges and charging cost against the month before, a 12-month distance chart and a table of the year](documentation/screenshots/desktop-reports.webp)

### Battery health

![The Battery health page: a gauge at 100 % health, the usable capacity of 64.1 kWh against a 64 kWh pack with a note that it reads above new, the charges it is based on, the trend, and a chart of the capacity readings over time](documentation/screenshots/desktop-battery-health.webp)

### Settings

Settings is a page of its own, with a section list on the left. Home location sets where you charge at home, with a radius you drag:

![The Settings page: appearance, region and units, and the home location on a map with a 250 m radius slider](documentation/screenshots/desktop-settings.webp)

### Debug

With **Settings > Advanced** switched on, the Debug page lists everything the MG gateway announces for the car, like a device page in a smart-home app: what it is, its latest value, whether MGMate saves it, and the controls, each of which asks before it sends:

![The Debug page: the car's model and gateway version, how many values are saved on every poll, what MG cannot do (open or close windows), and the controls and sensors with their latest values](documentation/screenshots/desktop-debug.webp)

### On a phone

<!-- markdownlint-disable MD033 -->

| Overview | Charging | Trips | Charges |
| -------- | -------- | ----- | ------- |
| <img src="documentation/screenshots/mobile-dashboard.webp" alt="The overview on a phone" width="200"> | <img src="documentation/screenshots/mobile-dashboard-charging.webp" alt="The live charging card and the car from above on a phone" width="200"> | <img src="documentation/screenshots/mobile-trips.webp" alt="Trips on a phone, with a week strip and the day's trips" width="200"> | <img src="documentation/screenshots/mobile-charges.webp" alt="Charges on a phone" width="200"> |
| **Reports** | **Battery health** | **Settings** | **Debug** |
| <img src="documentation/screenshots/mobile-reports.webp" alt="Reports on a phone" width="200"> | <img src="documentation/screenshots/mobile-battery-health.webp" alt="Battery health on a phone" width="200"> | <img src="documentation/screenshots/mobile-settings.webp" alt="Settings on a phone" width="200"> | <img src="documentation/screenshots/mobile-debug.webp" alt="The Debug page on a phone" width="200"> |

<!-- markdownlint-enable MD033 -->

The map, statistics, maintenance, climate schedules and the light theme are in the [screenshot gallery](documentation/screenshots/README.md).

## Where MGMate comes from

MGMate is based on [GarageStack](https://github.com/joszz/GarageStack) by Jos Nienhuis, released under the MIT licence, and keeps its live dashboard, map, trip log, climate schedules, maintenance and notifications. On top of that MGMate adds:

- **Charge sessions and costs**, with your electricity prices, AC and DC, and energy from the wall against energy into the battery
- **Trips and Charges pages** with month calendars, a week strip on phones, day-grouped lists, search and totals
- **Reports** per month and per year
- **Battery health** from your charges, against the pack when new
- **Activity**, a 24-hour strip and timeline of driving, charging and parking
- **Statistics** for the 12V battery, distance, range and temperatures, and consumption against outside temperature
- **Trip detail** with energy, driving and stopped time, median speed, the battery range used and a GPX download
- **A car photo** on the overview, and **battery-friendly polling** that only wakes the car when you ask it to
- **A redesigned overview** with a live charging card, a car status drawing traced from an MG4 seen from above, and small charge level and charging power charts
- **Warranty tracking** on the Maintenance page, counted from the first registration
- **Settings as a page**, with your **home location** and its radius, and an **Advanced** switch for a **Debug** page of everything the gateway reports
- **A clear warning when the MG sign-in fails**, in the sidebar and as a banner, instead of data that silently stops
- **A redesigned interface** with a grouped sidebar, a phone tab bar, the version and Refresh in the sidebar, and a new icon
- **English only**, and **no Home Assistant integration** (see [What was removed](#what-was-removed))

Thanks to Jos for the foundation.

### What was removed

- **Home Assistant.** MGMate no longer publishes sensors to Home Assistant, and the `HA_MQTT_*` settings and the external MQTT port (1883) are gone. If you used them, remove those settings and the port mapping; nothing else needs changing. The MG gateway still sends its own discovery messages (the Home Assistant MQTT discovery format) inside the stack, and MGMate still reads them to tell which kind of car it is and to fill the Debug page, so don't switch them off in the gateway.
- **Dutch.** The interface, notifications and exports are English only. A fork can add a language again; see [Adding a language](.github/CONTRIBUTING.md#adding-a-language).

**Want to try it?** Read [MG iSmart account and session limits](#mg-ismart-account-and-session-limits) first, because MGMate and the MG app cannot be signed in to the same account at once, then pick an [installation option](#installation). A single `docker run` is enough to get going.

## Contents

- [Where MGMate comes from](#where-mgmate-comes-from) and [Features](#features)
- [MG iSmart account and session limits](#mg-ismart-account-and-session-limits)
- [Prerequisites](#prerequisites) and [Installation](#installation)
- [Updating](#updating), [Monitoring](#monitoring) and [Backup and restore](#backup-and-restore)
- [Push notifications](#push-notifications)
- [Trip log](#trip-log), [Climate schedules](#climate-schedules), [Maintenance](#maintenance) and [Warranty](#warranty)
- [Dutch registration (RDW)](#dutch-registration-rdw)
- [Homepage dashboard widget](#homepage-dashboard-widget) and [Read-only API tokens](#read-only-api-tokens)
- [Map overlays](#map-overlays)
- [Security defaults](#security-defaults), [HTTPS](#https) and [What leaves your network](#what-leaves-your-network)
- [Documentation](#documentation)

## Features

- **Charges** -- Every plug-in is saved as a charge session with the energy added, AC or DC, peak power, duration, place, the battery before and after, and the cost at the electricity prices you set (Charges > Prices). On AC the energy drawn from the wall is shown next to what reached the battery. The peak power of an AC charge leaves out the short spike when the car pre-heats or cools the cabin just after charging stops. A charge that ends at your [home location](#features) is labelled home.
- **Reports** -- A page per month with distance, trips, driving time, days driven, consumption, energy used, charges, energy charged and charging cost, each against the month before, plus a 12-month chart and a table of the year.
- **Battery health** -- The usable capacity worked out from charges that covered at least 20 % of the battery, compared with the pack when new (51, 64 or 77 kWh, or your own), with the trend and every reading on a chart. The car only knows its exact charge level after a full charge (LFP packs drift the most), so health shows *Calibrating* until a charge reaches 100 %; from then on only readings with a full charge in the 30 days before them count, and the earlier ones are drawn hollow. Health never shows above 100 %: a pack that reads above new says so in a note, and fewer than five readings are marked as an early estimate.
- **Activity** -- A 24-hour strip and a timeline of when the car drove, charged, slept or went quiet, read from the car's own reports.
- **Overview** -- The battery with its range and a 24-hour charge level chart, which starts at the level the car last reported, so a car that sat parked for hours without a reading still gets a line across the whole day; one **Overview card** with the car from above, traced from a real MG4 in the car colour from Settings, with the tyre pressures at each wheel, open doors, windows, boot and bonnet, the lock on the roof and the charging cable, on the road with its speed while driving (or your own photo of it while parked), and the car status in a list underneath (locks, doors, windows, boot, bonnet, sunroof when the car reports one, lights and cable); the last charge with the energy added, where, what came from the wall, the miles since, and its charging power curve with the peak marked; where the car is; and the latest trips. Hiding the Overview card under Edit layout brings back a separate Car status card. While the car charges, the battery card becomes a **live charging card** with the power, voltage, current, the charge target and the range so far. The charts have labelled axes, units and a legend. Values arrive live over a WebSocket; while that connection is down, the page asks for the latest status every minute so it never sits on stale numbers.
- **Live dashboard cards** -- Behind **Edit layout**, the classic wall of status cards (see [Dashboard cards](#dashboard-cards)) can be switched on, reordered and toggled. Cards are shown or hidden based on your vehicle type (HEV, PHEV, BEV). The car drawing takes your car's colour (Settings > Your car), and the vehicle type can be set by hand there when detection gets it wrong.
- **MG sign-in status** -- When the gateway cannot sign in to your MG account, the sidebar's car status turns red and a banner says "MG sign-in failed", so you know why the data stopped. The sidebar also shows when data was last fetched (tap it to refresh, which asks first because it wakes the car) and the version, under Log out.
- **Settings page** -- Settings is a page (`/settings`) with a section list: appearance, region and units, dashboard, map, your car (colour, vehicle type, licence plate), **home location**, notifications, API tokens, your data, and Advanced. Home location is a point on a map with a radius from 50 m to 1 km: a charge that ends inside it counts as a home charge and is priced with your home tariff; anywhere else is public. The home place set under Trip log > Home and work labels home charges too.
- **Debug page** -- Switch on **Settings > Advanced** and a Debug page appears in the sidebar. It lists every sensor and control the MG gateway announces for the car, grouped like a smart-home device page, with the latest value, when it changed, and whether MGMate saves it on every poll, keeps only the newest value, or shows it live only. Controls ask before they send, and only commands the gateway offers can be sent. It also says what MG's API cannot do: it reports whether each window is open but cannot open or close them.
- **Tyre history** -- Tap a tyre's pressure on the dashboard's car to see a month of daily pressures for all four tyres, and how each one moved against the other three over the last two weeks: a tyre falling behind the others while they hold is a slow puncture, which also sends a notification.
- **Trip history** -- Browse past journeys on an interactive map, each drawn on the roads it took and coloured by speed or by the speed limit, with a heatmap of the roads you drive most. Each trip is saved to the database a few minutes after the car parks. After upgrading from a version that did not save trips, the existing history is saved in the background on the Worker's first start.
- **Trip log** -- Mark each trip as business, commute or private (commutes between your home and work are marked automatically), add notes, hide a GPS glitch, join two trips a fuel stop split, and export a month or a year as a spreadsheet with the addresses and odometer readings a tax trip log (such as the Dutch *rittenregistratie*) asks for. See [Trip log](#trip-log).
- **Warranty** -- On the Maintenance page, every part of the manufacturer's warranty (MG UK's covers to start with, all editable), counted from the first registration, with the time and miles left on each, marked as it nears its end. See [Warranty](#warranty).
- **Maintenance** -- Keep the car's service items (oil, tyres, the yearly inspection, ...) with a distance interval, a time interval or both. MGMate works out from the odometer and your service log when each one is due (a new car counts from its first registration), shows the most urgent on the dashboard, and sends a notification when an item reaches 90 % of its interval. Log each service with its date, odometer reading and notes.
- **Dutch registration (RDW)** -- For a car with a Dutch licence plate, a dashboard card with the date the APK runs out and the days left, open recalls with what can go wrong and what the dealer does about it, and whether the car is insured, from the [RDW's open data](https://opendata.rdw.nl). Notifications come for a new recall, the APK a month and a week ahead, and a car with no insurance on record. Off until `RDW_ENABLED=true`; the plate, entered under Settings, is the only thing sent. See [Dutch registration (RDW)](#dutch-registration-rdw).
- **Energy statistics** -- Track daily energy consumption, efficiency (Wh/km on a plug-in car, L/100 km on a hybrid), fuel use, electric share, average driving speed, and more, for any period from the last 24 hours to several years.
- **Remote commands** -- Trigger climate pre-conditioning (normal climate, fan only or front defrost), lock or unlock the car, open the boot, and activate the horn and lights remotely from the dashboard. On a plug-in car also start or stop charging, lock or unlock the charging cable, switch battery heating on or off, set the charging and battery heating schedules, and set the charge current limit and, where the car takes one, the charge target. Opening the boot and unlocking the cable ask for a second tap. Each command reports whether the car carried it out, and if it refused, the reason the MG servers gave.
- **Climate schedules** -- Switch climate on at a set time on chosen weekdays, or once. A schedule sets the temperature or mode, seat heating and the rear defroster, and can skip mild days by the car's outside temperature. Schedules run on the server, so no browser needs to be open, and each run sends a push notification saying whether the car started climate.
- **Push notifications** -- Browser and in-app alerts for key events: engine started, low or high tyre pressure, a slow puncture, low EV battery, low fuel, a weak 12V battery, car left unlocked, doors or windows left open, the car moving while parked, arriving at or leaving home or work, a charge that finished or stopped early, maintenance coming due, the APK running out or a recall on a Dutch plate, a climate schedule that ran, the messages the official MG app receives, and data that stopped arriving because the MG sign-in failed or the car went quiet. Each device chooses which ones it gets.
- **Homepage widget** -- A read-only API endpoint for the [gethomepage.dev](https://gethomepage.dev) Custom API widget, exposing key vehicle stats at a glance, along with the APK days left, open recalls, the maintenance due next and the last trip.
- **Log viewer** -- The Logs page shows the entries of the API's and the Worker's log files, newest first and older ones loading as you scroll, filtered by service, level and text, with exceptions folded open on request and a live mode that adds what was written every ten seconds. The files themselves stay on the server, one per day (`LOG_RETAINED_FILES`).
- **Full data export** -- Settings > Your data downloads everything MGMate keeps as one zip: per car its trips, maintenance, schedules, places and every telemetry reading (as CSV), and your notifications and settings.
- **Read-only API tokens** -- Make a token under Settings > API tokens and let a script, a dashboard or a watch read what the app shows, without signing in. A token can only read: it cannot send commands or change anything, and the full data export and the logs stay with a signed-in session.
- **Progressive Web App (PWA)** -- Installable on mobile or desktop for a native app-like experience, complete with a home screen icon and push notification support. Where the browser allows it, MGMate offers to install itself, and the offer can be turned down for good. Installing and push need HTTPS, see [HTTPS](#https).
- **Charging stations** -- Overlay nearby EV charging stations on the map, sourced from the [Open Charge Map](https://openchargemap.org) database. Station data is cached in the database for 7 days; on page load the map immediately shows all stations within 100 km of your car that are already cached. Markers show operational status; clicking a marker displays the station name, operator, address, and available connector types with power ratings. Requires a free OCM API key (`OPENCHARGEMAP_API_KEY`). Unlike fuel stations and service areas, charging station tiles are loaded on demand as you browse the map and are not pre-populated by the background Worker.
- **Fuel stations** -- Overlay nearby petrol and diesel stations on the map (HEV and PHEV only; not shown for BEV). Sourced from OpenStreetMap via the Overpass API -- no API key required. POI data is cached in the database for 7 days and pre-populated by the Worker for a 100 km radius around the car's last known position so the overlay is instant on first view.
- **Motorway service areas** -- Overlay motorway service areas and rest stops on the map (all vehicle types). Same DB-backed cache and Worker pre-cache as fuel stations; useful for BEV drivers who often find fast chargers at service areas.
- **Speed cameras** -- Overlay the speed cameras mapped in OpenStreetMap (`highway=speed_camera`), with the limit each one enforces and the kind of camera it is where OSM records them. Same DB-backed cache and Worker pre-cache as fuel stations, off until you switch it on in the map's layer panel, and removable from the deployment with `SPEEDCAMERAS__ENABLED=false` -- see the note on jurisdictions in the Map overlays section.
- **Place names** -- Trips are listed by where they went ("Zwolle to Deventer") instead of by date alone, and the dashboard's location card names the street the car is parked in. Sourced from OpenStreetMap via [Nominatim](https://nominatim.openstreetmap.org) -- no API key required, answers are cached in the database for 90 days, and the whole feature can be switched off under Settings > Map, or for the deployment with `GEOCODING__ENABLED=false`.
- **Snapped trip lines** -- A selected trip is drawn along the roads it was driven on rather than in straight lines between GPS fixes, which also gives a truer distance than the fixes alone. Matched against OpenStreetMap by [Valhalla](https://valhalla1.openstreetmap.de) -- no API key required, snapped trips are cached in the database for 30 days, and it can be switched off in the map's filter panel or for the deployment with `MAPMATCHING__ENABLED=false`.
- **Speed limits** -- The selected trip can be coloured against the limits signposted along it, green within and red above, with how far over it went and over how much of the trip a limit was known. Read from OpenStreetMap's `maxspeed` tags by the same match that snapped the trip, so it costs no extra request and needs no configuration.
- **Themed vector basemap** -- Every map is drawn from OpenStreetMap vector tiles by MapLibre GL, in a dark or light style that follows the interface theme (or in full colour, if you prefer) and with labels in the interface language. Served by [OpenFreeMap](https://openfreemap.org) without an API key, point it at your own tile server if you prefer, and it falls back to raster tiles where WebGL is unavailable.
- **Single sign-on** -- Sign in through your own identity provider (Authentik, Authelia, Keycloak, Pocket ID, Google, and anything else speaking OpenID Connect), with optional auto-login and group or email based access restrictions. A built-in username/password login remains available for installs without a provider. See [`AUTHENTICATION.md`](documentation/AUTHENTICATION.md).
- **English interface** -- MGMate ships in English only. The translation structure is kept, so a fork can add its own language; see [Adding a language](.github/CONTRIBUTING.md#adding-a-language).
- **Region and units** -- Kilometres or miles, Celsius or Fahrenheit, bar, psi or kPa for tyres, L/100 km or miles per UK or US gallon for fuel, and a 24-hour or 12-hour clock. Each one follows the region the browser is in (taken from its time zone, so an English browser in the Netherlands still gets kilometres and 14:05), and can be set by hand under Settings > Region and units, as can the region itself, which also sets the date order. Everything is stored in metric and converted only for display, so switching back and forth loses nothing; the trip log's CSV export follows the chosen distance unit and clock. The homepage widget and the `TYRE_PRESSURE_*_BAR` thresholds stay metric, and notifications use a 24-hour clock.
- **Settings follow you** -- Theme, units, the dashboard layout and the map's layers and filters are saved to your account, so every phone, tablet and computer you sign in on looks the same. Each browser keeps a copy too, so pages open instantly and the login page keeps your theme. After upgrading, the first device to sign in brings its settings along.
- **Self-hosted** -- Runs entirely on your own infrastructure via Docker (all-in-one container or Docker Compose). No cloud account or subscription required beyond the SAIC iSmart API.

### Dashboard cards

The overview above is what you see by default. The classic status cards below are switched on under **Edit layout > Show status cards**. They are shown or hidden automatically based on vehicle type (HEV / PHEV / BEV), and you can reorder and toggle each one in the edit mode.

| Card | Description | Vehicle types |
| ---- | ----------- | ------------- |
| Fuel Level | Tank level as a percentage | HEV, PHEV |
| Fuel Range | Estimated remaining range | HEV, PHEV |
| EV Battery | State of charge (%) | PHEV, BEV |
| Electric Range | Distance the car estimates it can drive on the battery alone | PHEV, BEV |
| Charging | Charging indicator | PHEV, BEV |
| Odometer | Total distance driven | All |
| 12V Battery | Auxiliary battery voltage | All |
| Doors | Lock status and door states; lock, unlock and open the boot | All |
| Windows | Window and sunroof states | All |
| Sunroof | Sunroof open/closed | All (off by default) |
| Climate | Temperature, seat heating, defroster | All |
| HV Battery | State of charge, voltage, current, power (kWh too, with `HV_BATTERY_CAPACITY_KWH` set); on a plug-in car also the charge current limit and the charge target, both settable | All |
| Find My Car | Horn + lights to locate the car | All |
| Lights | Main beam, low beam, sidelights | All |
| Daily Distance | Distance driven today | All |
| Daily Energy | Energy used today (kWh), or fuel burned today (L) on an HEV | All |
| Since Charge | Distance since last charge session | PHEV, BEV |
| Efficiency | Energy per km (Wh/km), or fuel consumption (L/100 km) on an HEV | All |
| Speed | Current vehicle speed | All (off by default) |
| Top Speed | Highest speed recorded in the most recent completed trip | All |
| Active Trip | Distance covered in the current trip | All |
| Charge Time | Estimated minutes remaining to charge limit | PHEV, BEV |
| Charging Session | OBC power, cable lock, charging type, last start and end; start or stop charging, lock or unlock the cable, set the charging schedule | PHEV, BEV |
| Battery Heating | Pre-heating status and schedule; switch it on or off, set the schedule | PHEV, BEV |
| Maintenance | The most urgent state of your maintenance items, linking to the Maintenance page | All |
| APK & recalls | The APK expiry date and days left, open recalls with their risk and repair, and the insurance on record, from the RDW | All (only with `RDW_ENABLED` and a plate set) |
| Climate Schedule | When a climate schedule starts climate next | All (off by default) |

### Statistics insights

The Statistics view shows insight cards and charts for a period of its own: the last 24 hours, the last 7, 30 or 90 days, this or last month, this or last year, or custom dates. Charts show a point per day up to about three months, per week up to a year, and per month beyond that. A period longer than a month also gets a table of trips, distance and energy per month. Cards are draggable and individually toggleable.

| Insight | Description |
| ------- | ----------- |
| Distance in period | Total distance across all trips in the selected window |
| Avg trip length | Average distance per trip |
| Remote preconditioning | Percentage of snapshots with remote climate active |
| Peak drive time | Hour of day with the most trip starts |
| 12V trend | Change in average 12V battery voltage over the period |
| Parking locations | Number of distinct parking spots (rounded GPS) |
| Electric share today | Estimated share of today's driving on electric power (PHEV only) |
| Avg speed | Average moving speed across all GPS points in the period, excluding stopped moments |

---

## MG iSmart account and session limits

> **Important:** The MG iSmart API only allows one active session per account at a time. Logging in anywhere else with the same credentials -- including the official MG app -- will immediately invalidate MGMate's session, causing telemetry to stop until MGMate reconnects.

MGMate needs the vehicle **owner account**: shared or secondary accounts lack the write permissions required to register alarm switches and will fail with error 1100003. So instead of moving MGMate off the owner account, set up a secondary account for the official MG app to use, and keep the owner account free for MGMate:

1. Open the MG app and go to **Settings > Account management > Add secondary account** (exact wording varies by region and app version).
2. Invite a second email address and accept the invite on that account.
3. Grant the secondary account access to your vehicle.
4. Sign in to the official MG app with the secondary account, and use the owner account's credentials for `SAIC_USER` / `SAIC_PASSWORD` in MGMate.

This way the official app runs independently on the secondary account and MGMate keeps its own session on the owner account.

---

## Prerequisites

Before installing, make sure the following are available on your host:

- **Docker** 23.0 or later (or Podman with Docker Compose compatibility); the quick start's relative `./mgmate-data` mount needs 23.0
- **Docker Compose** 2.20 or later (bundled with Docker Desktop; on Linux install the `docker-compose-plugin` package) -- required for Option B only
- An **MG iSmart account** with your vehicle already linked in the official app
- Outbound internet access on the host, for the MG servers and the map and lookup services listed under [What leaves your network](#what-leaves-your-network)

---

## Installation

Choose the method that fits your environment.

---

### Option A: All-in-one container (Unraid / homelab)

A single Docker image that bundles every service -- nginx, the .NET API + worker, PostgreSQL, Mosquitto, and the SAIC gateway. No Compose file or external database needed. Ideal for Unraid and similar NAS environments where running multiple containers is inconvenient.

#### Quick start

```bash
docker run -d \
  --name mgmate \
  --stop-timeout 60 \
  -p 8080:80 \
  -v ./mgmate-data:/data \
  -e SAIC_USER=your@email.com \
  -e SAIC_PASSWORD=yourpassword \
  -e SAIC_REGION=eu \
  -e CORS_ORIGIN=http://192.168.1.100:8080 \
  ghcr.io/viriatusog/mgmate:latest
```

> `POSTGRES_PASSWORD` is omitted -- a strong random password is auto-generated on first start and saved to `/data/.postgres_password`. Pass `-e POSTGRES_PASSWORD=yourpassword` explicitly if you need a known value (e.g. to connect with an external DB tool).
> **HTTPS proxy:** nothing to set. `AUTH_COOKIE_SECURE` defaults to `auto`, which marks the session cookie Secure whenever MGMate is opened over HTTPS (also behind a TLS-terminating proxy) and keeps a plain-HTTP LAN address working.
> **Logging in:** without further configuration the web login uses your `SAIC_USER` / `SAIC_PASSWORD`. Set `AUTH_USERNAME` / `AUTH_PASSWORD` for separate credentials, or point MGMate at your identity provider -- see [`AUTHENTICATION.md`](documentation/AUTHENTICATION.md).

**Unraid:** import `unraid/mgmate.xml` from Community Apps and fill in the variables in the template UI.

See [`docker/all-in-one/README.md`](docker/all-in-one/README.md) for the volume layout and Unraid setup steps, and [`CONFIGURATION.md`](documentation/CONFIGURATION.md) for every variable.

---

### Option B: Docker Compose

Separate containers for each service. More flexible -- you can use your own PostgreSQL server instead of the bundled one, and containers update independently.

#### 1. Clone the repository

```bash
git clone https://github.com/ViriatusOG/MGMate.git
cd mgmate
```

#### 2. Create your `.env` file

```bash
cp .env.example .env
```

Then open `.env` and fill in at minimum:

| Variable | Description |
| ---------- | ----------- |
| `SAIC_USER` | MG iSmart account email |
| `SAIC_PASSWORD` | MG iSmart account password |
| `SAIC_REGION` | Region the car is registered in: `eu` (default), `au`, or `tr` -- automatically mapped to the right API endpoint |
| `SAIC_REST_URI` | Optional, only needed for a region not listed above -- set directly to your gateway's endpoint |
| `CORS_ORIGIN` | The URL you open in your browser, e.g. `http://192.168.1.100:8080` |
| `POSTGRES_PASSWORD` | Generate with `openssl rand -hex 32`. Using an external Postgres server instead of the bundled one? Set this to match its existing password. |
| `MQTT_BROKER_PASSWORD` | Generate with `openssl rand -hex 32`. Mosquitto always runs bundled, so this is required either way. |

`docker compose up` refuses to start until both `POSTGRES_PASSWORD` and `MQTT_BROKER_PASSWORD` are set.

Everything else is optional: push notifications, single sign-on, tyre pressure bands, the traction battery's real size, the rate limit and the map's data sources. Every variable, with its default, is in [`CONFIGURATION.md`](documentation/CONFIGURATION.md); sign-in has its own guide in [`AUTHENTICATION.md`](documentation/AUTHENTICATION.md).

#### 3. Start the stack

With the bundled PostgreSQL container:

```bash
docker compose --profile bundled-postgres up -d
```

Using your own existing PostgreSQL server (set `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_USER`, `POSTGRES_DB`, `POSTGRES_PASSWORD` in `.env` to match):

```bash
docker compose up -d
```

The frontend is served on port `8080` by default (configurable via `FRONTEND_PORT` in `.env`).

---

## Updating

Read the release notes first: anything an upgrade needs from you is listed under **Upgrade Notes** at the top. Database migrations run automatically on startup and only forwards, so to go back to an older version, restore the backup you took before upgrading (see [Backup and restore](#backup-and-restore)). A new PostgreSQL major version is never installed by an update: moving from 18 to the next one means a `pg_dump` from the old version and a restore into a fresh volume on the new one, step by step in [Moving to a new PostgreSQL major version](docker/all-in-one/README.md#moving-to-a-new-postgresql-major-version).

**Docker Compose** builds MGMate from the cloned source, so update the source and rebuild. `docker compose pull` fetches the gateway, Postgres and Mosquitto images:

```bash
git pull
docker compose pull
docker compose up -d --build
```

**The Compose project name is fixed to `mgmate`.** Compose puts it in front of every volume (`mgmate_postgres_data`), and `docker-compose.yml` sets it, so the folder you run from no longer decides which database you get. An install that started in a folder with another name has its data under that name instead: before you update, run `docker compose ls`, and if the name there is not `mgmate`, add it to `.env`:

```bash
COMPOSE_PROJECT_NAME=mg   # the NAME column of docker compose ls
```

Without it, `docker compose up` stops with "The container name ... is already in use". Don't remove the old containers to get past that: MGMate would start on new, empty volumes. If that already happened, see [My data is gone after switching](documentation/PREVIEW_BUILDS.md#my-data-is-gone-after-switching).

**All-in-one container:**

```bash
docker pull ghcr.io/viriatusog/mgmate:latest
docker stop mgmate && docker rm mgmate
# Re-run your original docker run command
```

When a newer release is out, the sidebar says so under the version and links to its release notes. MGMate asks GitHub once a day; set `UPDATE_CHECK_ENABLED=false` to stop it. A Compose build only knows its own version when you pass one, so build with `APP_VERSION=$(git describe --tags) docker compose up -d --build` to get the notice.

### Monitoring

Point an uptime monitor (Uptime Kuma, Gatus, ...) at `/api/health` on the web interface's address, for example `http://192.168.1.100:8080/api/health`. It needs no sign-in, and answers `200` while everything works and `503` when something does not, naming each part:

```json
{ "status": "healthy", "checks": { "database": "healthy", "mqtt": "healthy", "worker": "healthy" } }
```

- `database`: the API reaches PostgreSQL.
- `mqtt`: the API reaches the broker it sends car commands through.
- `worker`: the Worker is running and subscribed to the broker. Right after the API starts it reads `degraded` (still `200`) until the Worker's first heartbeat arrives.

The car's side (an MG session that expired, a car out of reach) shows up as the "No data from the car" and "MG sign-in failed" notifications instead. Docker's own healthchecks follow the same signals: the Worker's container turns unhealthy when it loses the broker or the database, and so does the all-in-one container.

---

## Backup and restore

The only data that matters for disaster recovery is your PostgreSQL database (all vehicle telemetry/trip history) and the DataProtection keys used to encrypt login cookies -- losing the keys just logs everyone out, it doesn't lose any vehicle data.

**Docker Compose, bundled Postgres:** back up the database with `pg_dump` via the `postgres` service:

```bash
docker compose exec postgres pg_dump -U garagestack garagestack > mgmate-backup.sql
```

Restore into a fresh database:

```bash
cat mgmate-backup.sql | docker compose exec -T postgres psql -U garagestack garagestack
```

(Replace `garagestack`/`garagestack` with your `POSTGRES_USER`/`POSTGRES_DB` if you customised them.)

**Docker Compose, external Postgres:** the `postgres` service above only exists when started with `--profile bundled-postgres`, so `docker compose exec postgres ...` won't work here -- back up directly against your own server instead, using the same `POSTGRES_HOST`/`POSTGRES_PORT`/`POSTGRES_USER`/`POSTGRES_DB` values you set in `.env`:

```bash
pg_dump -h <POSTGRES_HOST> -p <POSTGRES_PORT> -U <POSTGRES_USER> <POSTGRES_DB> > mgmate-backup.sql
```

Restore the same way, with `psql` in place of `pg_dump`:

```bash
psql -h <POSTGRES_HOST> -p <POSTGRES_PORT> -U <POSTGRES_USER> <POSTGRES_DB> < mgmate-backup.sql
```

Either way, the DataProtection keys live in the `api_dataprotection` named volume -- back it up with the API stopped so nothing is mid-write:

```bash
docker compose stop api
docker run --rm -v mgmate_api_dataprotection:/keys -v "$(pwd)":/backup alpine tar czf /backup/dataprotection-backup.tar.gz -C /keys .
docker compose start api
```

(Volume names are prefixed with the Compose project name -- run `docker volume ls` to confirm yours if it's not `mgmate`.) The `mosquitto_data`, `worker_logs`, and `api_logs` volumes hold disposable/regeneratable data and don't need backing up.

**All-in-one container:** everything lives under the single `/data` bind mount (`mgmate-data/` by default). Stop the container first so nothing is mid-write, then copy the whole directory:

```bash
docker stop mgmate
cp -r mgmate-data mgmate-data-backup
docker start mgmate
```

To restore, stop the container, replace `mgmate-data` with the backup, and start it again.

### Database size

Telemetry older than a year is folded into one row per quarter of an hour, so the database keeps growing, but far more slowly. Saved trips, each day's totals and the latest value of every reading are kept; only the order of events within a quarter of an hour is lost. Compaction cannot be undone, so take a backup first if you want to keep the raw rows. Set `TELEMETRY_FULL_DETAIL_DAYS` to change the period, or to `0` to keep every row. See [Data kept over time](documentation/ARCHITECTURE.md#data-kept-over-time).

---

## Push notifications

MGMate checks your vehicle's state every 5 minutes and sends both a browser push notification and an in-app notification (bell icon) when any of the following conditions are detected. "Engine started", MG app messages and a failed MG sign-in go out the moment they arrive over MQTT instead. Each alert has a 1-hour cooldown per vehicle to avoid repeated notifications, and goes out once per report from the car: a car that sleeps for days with a soft tyre is reported once, not every hour.

| Alert | Condition |
| ------- | --------- |
| Engine started | Engine transitions from off to running |
| Low tyre pressure | Any tyre below `TYRE_PRESSURE_LOW_BAR` (default 2.2 bar) |
| High tyre pressure | Any tyre above `TYRE_PRESSURE_HIGH_BAR` (default 3.2 bar) |
| Possible slow puncture | One tyre lost `TYRE_PRESSURE_DROP_BAR` (default 0.15 bar) or more that the other three kept, over the last two weeks. Comparing the tyres with each other leaves out the weather, which moves all four together. Sent at most once every three days; tap a tyre on the dashboard's diagram for its history |
| Low EV battery | EV state-of-charge below `LOW_EV_PERCENT` (default 20 %, plug-in vehicles) |
| Low fuel | Fuel level below `LOW_FUEL_PERCENT` (default 15 %, hybrids and plug-in hybrids) |
| Low 12V battery | 12V battery below `AUX_BATTERY_LOW_VOLT` (default 12.0 V) on several polls in a row while the engine is off |
| No data from the car | Nothing reported for `STALE_DATA_HOURS` (default 48 hours): the MG session, the gateway or the car's connection needs a look |
| MG sign-in failed | The gateway could not sign in to the MG account, so no new data arrives (sent once per outage, as it happens) |
| Car left unlocked | `doors/locked = false` while engine is off |
| Door left open | Any door, boot, or bonnet open while engine is off |
| Window left open | Any window open while engine is off |
| Moved while parked | The car reports a position more than 1.5 km from where it parked, with the engine off: towed, lifted onto a truck, or taken on a ferry. Sent once per parking, with the car's first report after the move (a sleeping car only reports now and then). Not sent when the odometer shows the car was driven there: a drive MG never announced is saved as a trip without a route instead, and the car counts as parked at its new spot |
| Arrived home or at work | The car parks at a home or work place with **Notify on arrival and departure** switched on (Trip log > Home and work), judged by the position of the poll it parked in. Not sent for a drive that never left the place it started in |
| Left home or work | The car drives out of such a place: sent on its first position outside the place's radius, so starting the engine to warm the car up sends nothing |
| Charging complete | Charging stops while the cable is still connected (plug-in vehicles) |
| Charging interrupted | Charging stops because of a fault the car names: the charger, an overheating port, a plug not fully in, or a voltage mismatch (plug-in vehicles; unplugging mid-charge sends nothing) |
| Maintenance due | A maintenance item reaches 90 % of its interval, or passes it (checked every 6 hours, 7-day cooldown per item) |
| Open recall | The RDW lists a recall on the car's plate that has not been repaired (once per recall; a recall later reported repaired sends nothing) |
| APK due | The APK runs out within 30 days, and again within 7 days, of the date the RDW has (once each per APK date) |
| APK expired | The APK's last valid day has passed (repeated monthly until the RDW has a new date) |
| No insurance on record | The RDW has no WAM insurance on record for the car (repeated monthly while that lasts) |
| MG app message | The official MG app receives a message, such as an alarm or a reminder (sent as it arrives, once per message) |
| Scheduled climate | A climate schedule ran: climate started, the car refused, or it did not answer (nothing is sent for a run skipped because the car was in use or the day was mild) |

Push notifications require VAPID keys to be configured (`VAPID_PUBLIC_KEY` / `VAPID_PRIVATE_KEY`). Without them, alerts still appear in the in-app notification panel. While MGMate is open in a browser that allowed notifications but is not subscribed to push, the same alerts show up as system notifications too; a subscribed browser gets them through push instead, so nothing arrives twice.

Settings has a push switch for the device you are on and a per-type checklist for these alerts, which offers only the types the car's drivetrain can produce: the plug-in alerts (low EV battery, charging complete, charging interrupted) are left out for a plain hybrid, low fuel for a full electric car, and the RDW type (APK, recalls and insurance) wherever `RDW_ENABLED` is off. Both are kept per device: a type switched off on your phone still reaches your laptop, and still shows up in the notification panel. Only the switch turns push on or off; the checklist never asks the browser for permission.

Notification texts are written by the background worker, which has no browser to take a language from, so their language is a deployment setting: `NOTIFICATION_LANGUAGE=en` (the default, and the only language shipped).

MG app messages are the exception: they are passed on as SAIC wrote them, in the language of your MG account. Each one is sent once, even though the gateway repeats its latest message every time it starts, because MGMate remembers which message it last saw. Two kinds are left out: the "vehicle started" message, which the engine started alert already covers, and any message sent more than a day ago, so a new install does not announce whatever the account last received. A message that arrives while the worker is not running is not sent afterwards.

The tyre pressure thresholds (`TYRE_PRESSURE_LOW_BAR` / `TYRE_PRESSURE_GOOD_BAR` / `TYRE_PRESSURE_HIGH_BAR`) also drive the colour-coded dots on the dashboard's vehicle diagram. Set them once in your `.env` (or container environment) to match your vehicle's placarded pressure instead of the app's generic defaults.

To generate a VAPID key pair (requires Node.js):

```bash
npx web-push generate-vapid-keys
```

No Node.js installed? Use a temporary Docker container instead:

```bash
docker run --rm node:lts-alpine npx --yes web-push generate-vapid-keys
```

Copy the public key to `VAPID_PUBLIC_KEY` and the private key to `VAPID_PRIVATE_KEY` in your `.env` file (or as container environment variables). Keep the private key secret -- regenerating it invalidates all existing push subscriptions, requiring users to re-enable notifications in the browser.

Note: "keys left in the car" is not currently supported because the SAIC MQTT gateway does not expose a key-in-vehicle sensor.

---

## Trip log

**Trip log** in the sidebar lists the saved trips for a month or a whole year. For each trip it shows the date and times, the addresses it left from and arrived at, and the odometer at either end.

- **Purpose.** Mark a trip as business, commute or private. Choosing the same purpose again clears it. When trips in the period have no purpose yet, one button gives them all the same one; trips that already have a purpose keep it.
- **Home and work.** Set your home and work places once (**Home and work** at the top, or click a trip's start or end address to use it). A car has one home and can have several work places. Every trip between home and a work place is marked as a commute: the trips already in the log, and each new one as soon as it is saved. A trip counts as ending at a place when it ends within its radius (250 m unless you change it), so parking down the street or in the office garage still counts. Trips you classified yourself, or deliberately left unclassified, are never changed; an automatic commute shows "Automatic", and choosing another purpose overrides it. Moving or removing a place updates the automatic ones. Business and private stay yours to choose. The places are kept per car in MGMate's own database.
- **Arriving and leaving.** Switch on **Notify on arrival and departure** for a place, and the car parking there, or driving away from it, sends a notification (see [Push notifications](#push-notifications)). It is off for every place until you switch it on, and each device can still leave those two types out.
- **Notes.** Record anything a trip log should say, such as who you visited or why you took another route. Notes save when you leave the field.
- **Hide a trip.** A trip the GPS got wrong, or one that should not count, can be hidden: it leaves the map, the statistics, the dashboard, the totals and the export, and is listed only under **Show hidden trips**, where **Show again** brings it back. Nothing is deleted.
- **Join two trips.** A short stop, such as filling up on the way to work, splits one drive into two trips. **Join with next trip** (tap twice) makes them one, from the first one's start to the second one's end, with both notes kept. A purpose you chose for either is kept; otherwise it is labelled again from the new ends, so home to the fuel station to work becomes a commute. A join cannot be undone.
- **Totals.** The distance per purpose for the period is shown at the top.
- **Drives MG never announced.** The gateway only follows the car once MG says it started, and MG does not always say so: a drive started with the phone's digital key can pass unannounced. While parked, the gateway still reads the status MG keeps for the car every 2 minutes (a question to MG, never to the car, so it costs the 12 V battery nothing), and when one of those reads (or any other) shows the engine on, MGMate asks the gateway for a live check straight away. Seeing the car running, the gateway follows the rest of the drive as usual, so the trip gets its route from that point on.
- **Part route.** A drive first seen part-way has no fixes for its start. When it is first seen close to the parking spot and the odometer agrees, the trip simply starts at that spot. When it is first seen further off (more than 30 km in a straight line) or after going somewhere else on the way (the odometer counts more than three times the straight line), the trip still starts at the parking spot and counts the whole distance the odometer gives. It shows with a dashed outline and **Part route**, and "≈" before the start time and the duration, because when the car left is an estimate: its wake-up when it reported one, otherwise the missing distance at 50 km/h. Its detail draws a dashed straight line from the parking spot to the point where it was first followed, the real route from there, and a bar with how many miles were and were not followed. Average and top speed come from the followed part. Without an odometer reading that shows the car drove there (a ferry, a tow truck), the trip stays as recorded.
- **Trips without a route.** A drive that ends before any read sees the engine on has no position taken on the way. When the car turns up parked somewhere else with the odometer at least 1 km higher and no trip in between, the drive is saved anyway: from where it was last seen parked to where it was found, with the distance from the odometer and the energy from the battery level. It shows with a dashed outline and **No route**, "?" for the start time and no duration, and its detail draws a dashed straight line between the two spots with a note on why. It counts in the distance, energy and mi/kWh totals, not in driving time or speed, and purpose, notes and the export work as for any trip.
- **Distance.** A trip counts for the distance on its odometer when the car reported a reading at both ends. Otherwise it counts for the distance along its GPS fixes, which is a little short because the fixes cut corners.
- **Addresses** are looked up once through the same OpenStreetMap geocoder as the map, and then kept with the trip for good, so a log exported next year still shows them. With place names switched off (Settings > Map, or `GEOCODING__ENABLED=false`), ends show as coordinates and no lookups are made.
- **Export CSV** saves the period, hidden trips left out, as a spreadsheet: date, departure and arrival time, from and to address, odometer start and end, distance, purpose and notes. The file is comma-separated, so it opens in columns in Excel.

Only trips the Worker has saved can be logged, so the trip being driven, and one finished in the last few minutes, appear once the car has been parked for a while.

---

## Climate schedules

**Climate schedules** in the sidebar switch the car's climate on at a time you set, so it is warm (or cool) when you leave. They run on the MGMate server: no phone or browser has to be open.

- **When.** A time on the chosen weekdays, or once, on the next day that time comes round, when no weekday is chosen. The time is kept in the time zone the browser had when the schedule was made, so 07:30 stays 07:30 across daylight saving time.
- **What.** A temperature (16 to 28 °C), or the fan only, or the front defrost; seat heating for either front seat, and the rear defroster.
- **Only when it is needed.** A schedule can run only below or above an outside temperature: warming the car on cold mornings only, say. The car's own outside temperature from the last three hours decides; without a recent one the schedule runs.
- **When it does not run.** Not while the engine runs, and not when the MGMate Worker finds it more than 10 minutes late (after a restart, say): a car warmed long after you left is no use, so that run is recorded as missed. A car that answers that it is still busy, as one woken from a long sleep can, is asked again, up to three times.
- **What you hear.** Each run sends a notification saying whether the car started climate, refused, or did not answer. The schedule's card shows the last run and when it runs next, and the optional dashboard card the next start.

Commands sent by hand at the same moment as a scheduled run share the car's one command at a time; see [`ARCHITECTURE.md`](documentation/ARCHITECTURE.md#climate-schedules) for how a run is carried out.

---

## Maintenance

**Maintenance** in the sidebar keeps the car's service items, such as the oil, the tyres or the yearly inspection.

- **Intervals.** Each item has a distance interval, a time interval, or both; with both, it is due at whichever comes first.
- **When it is due.** From the last service you logged and the car's odometer, an item is *OK*, *due soon* once 90 % of its interval has passed, or *overdue*. One without a service logged yet counts from the car's first registration date at 0 miles, the way MG counts the first service of a new car; until that date is set it shows *No start point*, and its dialog asks for the date (**Count from: Registration**) or for a past service. The date is the same one the [Warranty](#warranty) uses.
- **Logging a service.** Record the date, the odometer reading (the current one is filled in) and notes. The item's interval starts again from there, and its history is kept.
- **Where it shows.** The dashboard's maintenance card shows the most urgent item, and so does the [Homepage widget](#homepage-dashboard-widget). A notification comes when an item becomes due soon or overdue (checked every 6 hours, at most once a week per item).

### Warranty

Below the service items, **Warranty** shows when each part of the manufacturer's warranty runs out, so you know what is still covered.

- **Where it starts.** Every cover counts from the car's first registration date, which you set at the top of the section or from a maintenance item (the date is kept on the car, together with the delivery date and the odometer at delivery). Service items without a logged service count from the same date.
- **The covers.** They start as MG UK's warranty statement for electric cars: the vehicle (7 years or 80,000 miles), the high voltage battery (8 years or 100,000 miles, repaired or replaced below 70 % of its capacity), anti-perforation (7 years, any mileage), and shorter covers such as the 12V battery, the charging cable and wear items. **Edit warranty** changes, adds or removes covers, or resets them to MG's list.
- **When it ends.** A cover ends at its time limit or its mileage limit, whichever comes first, counted on the total odometer. Each one is *active*, *ending soon* (in its last 30 days or last 10 % of its miles), *expired* or *not covered*. The main covers are shown as cards with what is left, the rest as a table (a list on a phone), soonest first.

![The Maintenance page: service items with their due bars, and the Warranty section starting from the first registration, with cards for the vehicle, high voltage battery and anti-perforation covers and a table of the shorter ones](documentation/screenshots/desktop-maintenance.webp)

---

## Dutch registration (RDW)

For a car with a Dutch licence plate, MGMate shows what the [RDW](https://opendata.rdw.nl) has on record for it: when the APK runs out, the recalls on the car, and whether it is WAM insured. It is off until the deployment sets `RDW_ENABLED=true`; until then nothing is sent and none of the below appears.

- **The plate.** Enter it under **Settings > Licence plate**, which is drawn as a Dutch plate. Type it with or without dashes: they are placed as the plate has them while you type (`HPK-98-B`, `12-ABC-3`). The plate is kept per car in MGMate's own database, without the dashes, and **Remove** clears it along with what the RDW said about it.
- **The lookup.** Saving the plate looks it up straight away, so the card fills at once. After that the Worker asks again every `RDW_REFRESH_HOURS` (a day by default). The plate is the only thing sent, to `opendata.rdw.nl`, together with the RDW's own reference codes for the recalls it lists; no key is needed.
- **The card.** The **APK & recalls** card shows the days the APK has left and how many recalls are open. Clicking it shows the APK date, the insurance on record, each open recall with what can happen and what the dealer does about it, the recalls already repaired, and when the RDW was last asked. The recall texts are the RDW's own, in Dutch. A plate the RDW does not know is said so, rather than shown as an error.
- **Notifications.** A new open recall (once per recall: one reported repaired later sends nothing), the APK a month and a week before it runs out and once it has, and a car with no WAM insurance on record. They are listed under [Push notifications](#push-notifications), and can be switched off per device like the other types.

The settings, including an optional Socrata app token for a higher rate limit, are in [`CONFIGURATION.md`](documentation/CONFIGURATION.md#dutch-registration-rdw).

---

## Homepage dashboard widget

MGMate has a read-only endpoint for the [gethomepage.dev](https://gethomepage.dev) Custom API widget, switched on by setting `WIDGET_API_KEY`. The setup and every field it returns are in [`WIDGET.md`](documentation/WIDGET.md).

---

## Read-only API tokens

To read MGMate's data from a script, a dashboard or a watch, make a token under **Settings > API tokens**. It is shown once: copy it then. Send it with every request:

```bash
curl -H "Authorization: Bearer gs_..." https://mgmate.example.com/api/vehicles
```

A token reads as the account that made it, through the same endpoints the app uses, and it can only read: any request that would change something, send a command or make another token is refused with `403`. The full data export and the server logs are refused too: they hold the car's whole history and the services' diagnostics, so they need a signed-in session. The list under Settings shows when each token was last used; revoking one stops it at once. MGMate keeps only a hash of each token, so a lost one cannot be shown again: revoke it and make a new one.

**The API reference** is at `/api/openapi/v1.json`, readable with the token like any other endpoint: every endpoint, what it takes and what it answers, as an OpenAPI document. Load it into any OpenAPI viewer, for example:

```bash
curl -H "Authorization: Bearer gs_..." https://mgmate.example.com/api/openapi/v1.json -o mgmate-openapi.json
```

A few things to know before building on a token:

- Tokens do not expire. Revoke the ones you no longer use; an account can hold 20.
- Requests with a token count against the same per-address rate limit as the app (120 a minute by default, `RATE_LIMIT_GLOBAL_PER_MINUTE`), so poll every few seconds at most.
- Live updates (the `/hubs/telemetry` connection the dashboard uses) are not available with a token; poll the REST endpoints instead.

---

## Map overlays

The map view supports four POI overlay layers: charging stations, fuel stations, motorway service areas and speed cameras. All data is cached in the database and served instantly on subsequent visits. Open the **Filters** panel (sliders icon, top-right of the map) to toggle each layer and adjust filters.

### Basemap

The map underneath every overlay is drawn from OpenStreetMap vector tiles by [MapLibre GL](https://maplibre.org), served by [OpenFreeMap](https://openfreemap.org) -- no API key required.

- The basemap follows the interface theme: a dark style in the dark theme, a light one in the light theme, switching without a page reload. Turn on **Colourful maps** in Settings to show every map in full colour instead, whatever the theme. The setting covers every map in the app, the dashboard's location card included.
- Labels are in English (OpenStreetMap's `name:en` tags) and fall back to the local name where no translation exists.
- The renderer (about 1.5 MB) is downloaded only when a page with a map opens, and is deliberately kept out of the service worker's precache so an install does not pay for it up front.
- A browser without WebGL, or a tile server that cannot be reached, falls back to the classic OpenStreetMap raster tiles automatically.
- To serve the basemap yourself (your own OpenFreeMap or any MapLibre style), build the frontend image with `--build-arg MAP_STYLE_DARK=...`, `--build-arg MAP_STYLE_LIGHT=...` and `--build-arg MAP_STYLE_COLORFUL=...`, and add that host to `connect-src` and `img-src` in `frontend/nginx-security-headers.conf` -- the Content-Security-Policy only allows the hosts listed there.

### Charging stations

Requires a free [Open Charge Map](https://openchargemap.org/site/develop) API key (`OPENCHARGEMAP_API_KEY`).

- Markers show operational status at a glance.
- Clicking a marker shows the station name, operator, address, and available connectors with power ratings.
- **Power filter** -- a dual-handle slider lets you restrict results to a specific kW range (e.g. 50-150 kW for fast DC only). The filter is applied client-side from the local cache; no new API call is made when you move the slider. Set the upper handle to the maximum (350+) to remove the upper limit.
- Tile data is loaded on demand as you browse the map and cached for 7 days. The Worker does not pre-populate charging tiles.
- Available for BEV and PHEV vehicles only; hidden for HEV.

### Fuel stations (HEV and PHEV only)

Sourced from OpenStreetMap via the free [Overpass API](https://overpass-api.de) -- no API key required.

> **Performance note:** Fetching data from the Overpass API for uncached map tiles can be slow -- a single tile request may take anywhere from a few seconds to over 30 seconds depending on Overpass server load and the density of POIs in the area. The background Worker pre-populates a 100 km radius around your car's position on startup and every 6 hours, so that area loads instantly. Outside that radius, each newly-visible tile triggers an on-demand Overpass fetch; expect a visible delay until it is cached. Subsequent visits load from the database with no external call.

- Shows petrol and diesel stations from OSM data. Accuracy and completeness depend on OSM coverage in your area.
- **Brand filter** -- select one or more brands (e.g. BP, Shell, Total) from the Filters panel. Only stations with a matching `brand` or `operator` OSM tag are shown; untagged stations are hidden when any filter is active. The filter is applied client-side with no additional API call.
- The Worker pre-populates a 100 km radius around your car's last known position every 6 hours, so the layer loads instantly on first view without hitting Overpass.
- If Overpass answers that it is busy (429, 503 or 504), MGMate waits as long as Overpass asks (its `Retry-After`) before asking again, and shows the cached data in the meantime. The same goes for every other service MGMate asks.
- Tile data is cached for 7 days. Zooming or panning to a new area triggers on-demand fetching for uncached tiles.
- Hidden for BEV vehicles (petrol stations are not relevant).

### Motorway service areas

Sourced from OpenStreetMap (`highway=services`) via the Overpass API -- no API key required.

- Shows motorway service areas and rest stops.
- Available for all vehicle types; useful for BEV drivers because many service areas have fast-charger banks.
- Same DB-backed cache and Worker pre-population as fuel stations. The same Overpass slowness applies for uncached tiles outside the pre-populated radius -- see the note in the Fuel stations section above.

### Speed cameras

Sourced from OpenStreetMap (`highway=speed_camera`) via the Overpass API -- no API key required.

- Shows the cameras OSM holds a position for, all vehicle types. A marker's popup names the limit the camera enforces (`maxspeed`), the kind of camera (fixed, mobile site, average speed check, red light) and its operator, as far as the node is tagged for them. Coverage and accuracy are whatever OSM's mappers have entered.
- Off by default. **The map's layer panel > Speed cameras** switches it on.
- Positions on a map, not warnings: MGMate is a dashboard of the car's state, and nothing here alerts you to a camera ahead while driving.
- Several countries restrict pointing a driver at camera positions, France and Germany among them. Set `SPEEDCAMERAS__ENABLED=false` to remove the layer from the deployment: the toggle then disappears from the layer panel, the endpoint serves nothing, and the Worker fetches no camera data either.
- Same DB-backed cache and Worker pre-population as fuel stations. The same Overpass slowness applies for uncached tiles outside the pre-populated radius -- see the note in the Fuel stations section above.

### Place names (reverse geocoding)

Sourced from OpenStreetMap via [Nominatim](https://nominatim.openstreetmap.org) -- no API key required.

- The trip list leads with the route ("Zwolle to Deventer", or one city for a round trip) and moves the date into the line below it. Until the names arrive, the row shows a placeholder; if geocoding is off or unavailable, it keeps showing the date, exactly as before.
- The dashboard's location card and the car's map popup show the street and city the car is standing in.
- Answers are cached in PostgreSQL for 90 days, keyed by a coordinate grid and by language, so the same parking spot is looked up once. "Nothing mapped here" is cached for a day, since OSM coverage grows.
- The public Nominatim instance allows about one request per second, so a request resolves at most three new coordinates and the browser asks again for the rest; a cold trip list fills in over a few seconds and is instant from then on.
- Names are asked for in English, and fall back to the local name.
- **Settings > Map > Show place names** turns the whole thing off (it is on by default). Off, trips are listed by date, the location card shows no address, and no coordinates leave the browser.
- Set `GEOCODING__ENABLED=false` to switch it off for the whole deployment instead, or point `GEOCODING__BASEURL` at your own Nominatim instance.

### Snapped trip lines (map matching)

Telemetry arrives as GPS fixes tens of seconds apart, so a trip drawn straight from them cuts every corner and runs through buildings. Selecting a trip sends its fixes to [Valhalla](https://valhalla1.openstreetmap.de) -- the router behind OpenStreetMap's own directions -- which answers with the roads underneath them. No API key required.

- Only the selected trip is snapped. The raw line is drawn immediately and replaced when the answer lands, so the map never waits.
- The pill at the top of the map shows the snapped distance, which is the better figure: the straight-line distance through the fixes is always short by the corners it cut.
- The speed overlay follows the snapped line too, so its colours sit on the route that was driven.
- A trip with a hole in its telemetry (the gateway stopped publishing for a while) is matched in parts and stitched back together, with the hole left as the straight jump it always was. Valhalla refuses a trace containing a jump wider than 10 km, which is exactly what such a hole looks like.
- A match whose length no longer resembles the fixes it came from is discarded and the raw line kept: with sparse fixes a router can return a plausible route that is not the one taken.
- Snapped trips are cached in PostgreSQL for 30 days, keyed by a hash of the fixes, so a trip is matched once however often it is selected. "These fixes snap to nothing" is cached for a day.
- **The map's filter panel > Snap to roads** turns it off (it is on by default), and trips are then drawn from their raw fixes as before.
- Set `MAPMATCHING__ENABLED=false` to switch it off for the whole deployment instead, or point `MAPMATCHING__BASEURL` at your own Valhalla instance.

### Speed limits

The matcher also answers with the limit on each stretch of road it snapped the trip onto, from OpenStreetMap's `maxspeed` tags. **The map's filter panel > Speed limits** colours the selected trip against them: green within the limit, red above it, grey where OSM holds no limit. The legend adds how far over the limit the trip went, and over how much of its distance a limit was known at all. It needs no extra configuration, and no request beyond the one that snapped the trip.

Two things it cannot know, which the legend and its tooltip say rather than hide:

- **Not every road has a limit in OSM.** The share of the trip where one is known is shown, and a trip over nothing but untagged roads keeps its ordinary colour instead of drawing entirely grey.
- **Conditional limits are not in the data.** A Dutch motorway signed 100 by day and 130 at night is tagged 100, so a night drive there reads as over the limit. The same goes for variable-limit sections.

A reading counts as over the limit only past 5 km/h above it: speedometers read high by a few percent by design, and a fix is a spot sample rather than an average over the stretch it covers.

---

## Security defaults

- API routes require login.
- Sign-in goes through your own identity provider when `OIDC_AUTHORITY` is configured, which also disables the built-in password login unless you keep it on with `AUTH_PASSWORD_LOGIN_ENABLED=true`. Without a provider, the built-in login reuses the configured MG account credentials unless `AUTH_USERNAME`/`AUTH_PASSWORD` are set. Full reference: [`AUTHENTICATION.md`](documentation/AUTHENTICATION.md).
- Sessions are encrypted, HTTP-only, `SameSite=Strict` cookies. Logout revokes the session server-side, not just the client-side cookie.
- With an identity provider, restrict who may sign in -- at the provider itself or with `OIDC_ALLOWED_GROUPS` / `OIDC_ALLOWED_EMAILS`. Without a restriction, every account the provider accepts can sign in and control the car.
- Login endpoints are rate-limited per IP address on top of the global limit.
- MQTT requires credentials, and the broker is never published outside the stack: only MGMate's own services and the gateway can reach it.
- Requests are rate-limited per address, and also per device in front of the frontend container unless you list your proxies (see [Reverse proxy examples](#reverse-proxy-examples)), so a device on your LAN cannot get round the limits by claiming new addresses.
- Every container image MGMate builds on (the gateway, PostgreSQL, Mosquitto, nginx, .NET) is pinned by digest, so an upstream tag that changes cannot change what you run without a new MGMate release. The server logs under `/data/logs` are readable by root only.
- Each change is tested by starting the whole Compose stack and the all-in-one container in CI, with a gateway that never contacts MG.
- The frontend sends a strict Content-Security-Policy with every response, which allows scripts only from MGMate itself. See [Behind a reverse proxy or CDN](#behind-a-reverse-proxy-or-cdn) for keeping it intact.

### What leaves your network

MGMate runs on your own hardware, but a few features ask public services for what they know. Each one is listed here with what it sends, and each one except the MG servers can be switched off.

| Service | Asked by | What it receives | Switch it off |
| ------- | -------- | ---------------- | ------------- |
| MG / SAIC iSmart API | the gateway | Your MG login, and the commands you send the car | Cannot be: it is where the car's data comes from |
| [Nominatim](https://nominatim.openstreetmap.org) (place names) | the API | The coordinates of a trip's ends, or of where the car is parked, and the interface language | Settings > Map > Show place names, or `GEOCODING__ENABLED=false` |
| [Valhalla](https://valhalla1.openstreetmap.de) (snapped trips, speed limits) | the API | The GPS fixes of the trip you select on the map | The map's filter panel > Snap to roads, or `MAPMATCHING__ENABLED=false` |
| [Overpass](https://overpass-api.de) (fuel stations, service areas, speed cameras) | the API and the Worker | The map tiles being looked at, and the ones around the car (0.5° squares, not the car's position) | Turn the layers off; `SPEEDCAMERAS__ENABLED=false` removes the camera layer |
| [Open Charge Map](https://openchargemap.org) (charging stations) | the API and the Worker | The map tiles being looked at, and your OCM key | Leave `OPENCHARGEMAP_API_KEY` empty |
| [RDW open data](https://opendata.rdw.nl) (APK, recalls, insurance) | the API and the Worker | The licence plate, and the RDW's own codes for the recalls it lists | Off until `RDW_ENABLED=true` |
| [GitHub](https://github.com/ViriatusOG/MGMate/releases) (update notice) | the API | One request a day for the latest release | `UPDATE_CHECK_ENABLED=false` |
| [OpenFreeMap](https://openfreemap.org) and [OpenStreetMap](https://www.openstreetmap.org) tiles (the basemap) | your browser | The map tiles your browser shows | Serve your own tiles, see [Basemap](#basemap) |
| Your browser's push service (Google, Mozilla, Apple) | the Worker | Each notification, encrypted so only your browser can read it, and `VAPID_SUBJECT` | Leave the VAPID keys empty, or switch push off per device |

Requests to the map, lookup and update services identify themselves as `MGMate/1.0 (+https://github.com/ViriatusOG/MGMate)`, as Nominatim's usage policy asks. Apart from what the table lists, the car's telemetry, its trips, your notifications and your settings stay on your server, unless you connect something to it yourself, such as a script with an API token.

### HTTPS

MGMate serves plain HTTP, which is all it needs on your own network, except for three things browsers keep for secure pages: push notifications, installing it as an app, and the service worker behind both. Browsers count `http://localhost` as secure, but not `http://192.168.1.100:8080`. To use those on your phone, give MGMate an `https://` address: put it behind a reverse proxy that terminates TLS (examples below), or reach it through a VPN that provides HTTPS certificates. Over plain HTTP, Settings > Notifications says why push is not available.

### Reverse proxy examples

Point the proxy at the web interface (`http://<host>:8080` by default, the frontend container or the all-in-one container), then set `CORS_ORIGIN` to the new address, for example `CORS_ORIGIN=https://garage.example.com`. Live updates use a WebSocket on `/hubs/`, so the proxy must pass WebSocket upgrades through. The session cookie is marked `Secure` automatically once the browser uses HTTPS (`AUTH_COOKIE_SECURE=auto`, the default).

**Caddy:**

```caddy
garage.example.com {
    reverse_proxy 192.168.1.100:8080
}
```

**Traefik** (file provider; WebSockets need no extra setting):

```yaml
http:
  routers:
    mgmate:
      rule: Host(`garage.example.com`)
      entryPoints: [websecure]
      tls:
        certResolver: letsencrypt
      service: mgmate
  services:
    mgmate:
      loadBalancer:
        servers:
          - url: http://192.168.1.100:8080
```

**Nginx Proxy Manager:** add a Proxy Host for `garage.example.com` that forwards to `http`, `192.168.1.100`, port `8080`, with **Websockets Support** switched on and a certificate requested on the SSL tab.

Every proxy in front of the API (yours, and with Compose the frontend container) is trusted by default as long as it has a private address, so visitors are told apart for the login rate limit without further setup. The catch is that anything else on your LAN is believed the same way and could claim to be a new visitor on every sign-in attempt, so in that default sign-ins, widget calls and exports are also capped per device in front of the frontend container, at three times their per-visitor limit (30 sign-ins and 180 widget calls per 5 minutes, 15 exports per 15 minutes; a household behind one proxy shares that). Other requests stay limited per claimed address only, so a LAN device can still spread them over made-up addresses. To close the gap, list each proxy in `ForwardedHeaders__TrustedProxies__0`, `__1`, ... (see [CONFIGURATION.md](documentation/CONFIGURATION.md)): then only those are believed and the extra cap is off. The frontend container is one of them, and when your proxy reaches it over a Docker network, list that network as a range (`172.18.0.0/16`) rather than single addresses, which change when a container is recreated.

### Behind a reverse proxy or CDN

- **Let MGMate's security headers through.** A proxy that sets its own `Content-Security-Policy`, `X-Frame-Options` or `Permissions-Policy` replaces MGMate's rather than adding to them. A Traefik headers middleware does this, for example. The page then runs without its script restrictions. Leave those three headers out of whatever headers the proxy adds for MGMate; transport headers such as HSTS are fine.
- **On Cloudflare, switch Rocket Loader off for this host** (Speed > Optimization, or a Configuration Rule). It rewrites every script on the page and delays the one that applies the stylesheet, which wastes the stylesheet preload and fills the console with warnings. With MGMate's policy in place it is blocked outright. Cloudflare's injected Web Analytics beacon and bot-detection script are blocked by the policy too, so switch those off for the host if you would rather not see the console reports.

---

## Documentation

For running MGMate:

- [`CONFIGURATION.md`](documentation/CONFIGURATION.md): every setting, with its default
- [`AUTHENTICATION.md`](documentation/AUTHENTICATION.md): single sign-on, the built-in login, and provider examples
- [`WIDGET.md`](documentation/WIDGET.md): the gethomepage.dev widget
- [`docker/all-in-one/README.md`](docker/all-in-one/README.md): the all-in-one image and Unraid
- [Screenshots](documentation/screenshots/README.md) of the main pages

For working on it:

- [`CONTRIBUTING.md`](.github/CONTRIBUTING.md): setting up, tests, and adding a language
- [`ARCHITECTURE.md`](documentation/ARCHITECTURE.md): how the services fit together
- [`DEMO.md`](documentation/DEMO.md): the app on sample data, without a car, and the API reference
- [`RELEASING.md`](documentation/RELEASING.md): versions, tags and release notes
- [`PREVIEW_BUILDS.md`](documentation/PREVIEW_BUILDS.md): throwaway `pr-<number>` images for testing a fix on someone else's car, and how to run one
- [`ACCESSIBILITY.md`](.github/ACCESSIBILITY.md) and [`SECURITY.md`](.github/SECURITY.md), which also says how to report a vulnerability

What changed in each version is on the [releases page](https://github.com/ViriatusOG/MGMate/releases).

---

> **Development note:** This project was built with AI-assisted development (Claude Code). All code was reviewed, directed, and validated by a human developer throughout - AI acted as a coding assistant, not an autonomous agent.
