# Screenshots

The main pages of MGMate, on a desktop and on a phone. They were taken in [demo mode](../DEMO.md), so the car, its trips and the addresses are sample data.

## Desktop

### Overview

![The overview: the battery with a 24-hour charge level chart, the Overview card with the car on the road, its speed, tyre pressures and status list, and the last charge with its power curve](desktop-dashboard.webp)

### Overview while charging

![The overview while charging: the live charging card with power, voltage, current and target, and the Overview card with the car parked, the cable in, and an open window and boot on the drawing and in its list](desktop-dashboard-charging.webp)

### Map

![The map: a month of trips listed by where they went, drawn along the roads between Amsterdam, Haarlem, Almere, Utrecht and Amersfoort over a heatmap of the most driven roads](desktop-map.webp)

### A single trip

![One trip from Amsterdam to Utrecht, snapped to the roads and coloured green where it kept to the speed limit and red where it went over, with a legend summing up how much of it was over](desktop-map-trip.webp)

### Statistics

![The statistics: insight cards for the month's distance, average trip length, peak drive time, 12V trend, parking spots and average speed, above charts of the EV battery and the tyre pressures](desktop-statistics.webp)

### Trips

![Trips: totals for the month, a calendar with the days driven, and each trip with its places, times, battery used and mi/kWh](desktop-trips.webp)

### Charges

![Charges: energy added, average duration and cost, a calendar with the charging days, and each charge with its times, battery before and after and kWh added](desktop-charges.webp)

### Reports

![Reports: the month against the month before, a 12-month distance chart and the year month by month](desktop-reports.webp)

### Battery health

![Battery health: a gauge, the usable capacity against the pack when new, and the capacity readings over time](desktop-battery-health.webp)

### Maintenance

![The maintenance list: service items marked overdue, due soon or OK, one counting from the car's registration, and the warranty covers with the time and miles left on each](desktop-maintenance.webp)

### Climate schedules

![The climate schedules: a weekday schedule at 07:30 with seat heating, and a one-off front defrost at 08:15, each with when it runs next](desktop-climate-schedules.webp)

### Settings

![Settings: appearance, region and units, and the home location with its radius](desktop-settings.webp)

### Debug

![The Debug page: device info, data coverage, what MG cannot do, and the controls and sensors with their latest values](desktop-debug.webp)

### Light theme

![The dashboard in the light theme](desktop-dashboard-light.webp)

## Phone

<!-- markdownlint-disable MD033 -->

| Overview | Map | A single trip | Light theme |
| -------- | --- | ------------- | ----------- |
| <img src="mobile-dashboard.webp" alt="The dashboard on a phone" width="180"> | <img src="mobile-map.webp" alt="The map on a phone" width="180"> | <img src="mobile-map-trip.webp" alt="One trip against the speed limits on a phone" width="180"> | <img src="mobile-dashboard-light.webp" alt="The dashboard on a phone in the light theme" width="180"> |
| **Trips** | **Charges** | **Reports** | **Battery health** |
| <img src="mobile-trips.webp" alt="Trips on a phone" width="180"> | <img src="mobile-charges.webp" alt="Charges on a phone" width="180"> | <img src="mobile-reports.webp" alt="Reports on a phone" width="180"> | <img src="mobile-battery-health.webp" alt="Battery health on a phone" width="180"> |
| **Statistics** | **Maintenance** | **Climate schedules** | **Charging** |
| <img src="mobile-statistics.webp" alt="The statistics on a phone" width="180"> | <img src="mobile-maintenance.webp" alt="The maintenance list on a phone" width="180"> | <img src="mobile-climate-schedules.webp" alt="The climate schedules on a phone" width="180"> | <img src="mobile-dashboard-charging.webp" alt="The live charging card on a phone" width="180"> |
| **Settings** | **Debug** | | |
| <img src="mobile-settings.webp" alt="Settings on a phone" width="180"> | <img src="mobile-debug.webp" alt="The Debug page on a phone" width="180"> | | |

<!-- markdownlint-enable MD033 -->

## Taking new ones

`pnpm screenshots` in `frontend/` takes all of them again from a running demo stack, and copies the dashboard, map and statistics ones to the install screenshots in the app's manifest (`frontend/public/screenshot-*.webp`):

```bash
cp .env.demo.example .env.demo
docker compose --env-file .env.demo -f docker-compose.demo.yml up -d --build   # repository root
cd frontend && pnpm screenshots                                                 # or: pnpm screenshots http://localhost:5173
```

It signs in as `demo`, switches the Debug page on, and takes the desktop screenshots at 1280 x 800 (the charging overview taller, so the whole Overview card shows) and the phone screenshots at 390 x 844 at twice the pixel density, in the dark theme with the date filter on 30 days and the region set to the UK (miles, psi, pounds), plus the overview in the light theme. For the charging overview it parks the demo car and plugs it in through the demo API, and puts it back on the road afterwards. Run it against a freshly started demo API: the API's rate limit can cut a second run short (start the local API with `RATE_LIMIT_GLOBAL_PER_MINUTE=10000` to avoid that). The maps need the internet for their tiles; behind a proxy, `HTTPS_PROXY` is used for them. `CHROMIUM_PATH` points it at a Chromium other than Playwright's own. Keep the manifest's `sizes` in `frontend/vite.config.ts` in step if the sizes change.
