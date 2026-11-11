// Takes the screenshot gallery (documentation/screenshots) and the install screenshots in the
// manifest (public/screenshot-*.webp) from a running demo stack, so they show the app as it is.
//
//   docker compose --env-file .env.demo -f docker-compose.demo.yml up -d --build   (repository root)
//   cd frontend && pnpm screenshots [base URL, http://localhost:8080 by default]
//
// SCREENSHOTS_ONLY=notifications,settings retakes just those gallery shots (by file name).
//
// Desktop shots are 1280 x 800, phone shots 390 x 844 at twice the density, in the dark theme with
// the date filter on 30 days, in the UK, and one of each in the light theme. CHROMIUM_PATH points at a
// browser other than Playwright's own; HTTPS_PROXY, when set, is where the map's tiles are fetched.
import { copyFile, writeFile } from 'node:fs/promises'
import { chromium } from '@playwright/test'

const base = (process.argv[2] ?? 'http://localhost:8080').replace(/\/$/, '')
const docs = new URL('../../documentation/screenshots/', import.meta.url)
const publicDir = new URL('../public/', import.meta.url)

const DESKTOP = { name: 'desktop', viewport: { width: 1280, height: 800 }, deviceScaleFactor: 1 }
const PHONE = {
  name: 'mobile',
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 2,
  isMobile: true,
  hasTouch: true,
}

// The pages of the gallery, each with what has to have happened before it is worth a picture.
const PAGES = [
  { file: 'dashboard', path: '/' },
  { file: 'map', path: '/map' },
  { file: 'map-trip', path: '/map', prepare: selectTrip },
  { file: 'statistics', path: '/statistics' },
  { file: 'trips', path: '/trip-log' },
  { file: 'charges', path: '/charges' },
  { file: 'reports', path: '/reports' },
  { file: 'battery-health', path: '/battery-health' },
  { file: 'maintenance', path: '/maintenance' },
  { file: 'climate-schedules', path: '/climate-schedules' },
  { file: 'settings', path: '/settings' },
  { file: 'debug', path: '/debug' },
  { file: 'notifications', path: '/', prepare: openNotifications },
  { file: 'dashboard-charging', path: '/', prepare: parkAndCharge, after: driveOn, tall: true },
]

// The install screenshots in the manifest (vite.config.ts), copied from the gallery's.
const MANIFEST = { dashboard: 'home', map: 'map', statistics: 'statistics' }

const browser = await chromium.launch({
  executablePath: process.env.CHROMIUM_PATH || undefined,
  // Playwright sends even localhost through a proxy unless told otherwise.
  proxy: process.env.HTTPS_PROXY
    ? { server: process.env.HTTPS_PROXY, bypass: 'localhost,127.0.0.1' }
    : undefined,
})

const only = process.env.SCREENSHOTS_ONLY?.split(',').map((f) => f.trim())

try {
  for (const device of [DESKTOP, PHONE]) {
    const light = [{ file: 'dashboard-light', path: '/' }]
    await shoot(device, 'dark', only ? PAGES.filter((p) => only.includes(p.file)) : PAGES)
    await shoot(device, 'light', only ? light.filter((p) => only.includes(p.file)) : light)
  }
  for (const [page, name] of Object.entries(only ? {} : MANIFEST)) {
    for (const device of ['desktop', 'mobile']) {
      await copyFile(
        new URL(`${device}-${page}.webp`, docs),
        new URL(`screenshot-${device}-${name}.webp`, publicDir),
      )
    }
  }
} finally {
  await browser.close()
}

async function shoot(device, theme, pages) {
  if (pages.length === 0) return
  const { name, ...options } = device
  // The region follows the browser's time zone: London gives the gallery's miles, psi, pounds and
  // 24-hour clock whatever machine runs this.
  const context = await browser.newContext({
    ...options,
    colorScheme: theme,
    locale: 'en-GB',
    timezoneId: 'Europe/London',
  })
  // The install offer would cover the phone screenshots.
  await context.addInitScript(() => {
    localStorage.setItem('garagestack-pwa-install-dismissed', 'never')
  })
  const page = await context.newPage()
  await signIn(page)
  // Saved to the demo account, which every page then takes: the theme, English, the gallery's
  // 30 days, the Debug page switched on, and a selected trip coloured against the speed limits.
  await saveSettings(page, 'ui', {
    theme,
    locale: 'en',
    filterDays: 30,
    batteryPackKwh: 64,
    showDebugPage: true,
  })
  await saveSettings(page, 'map', { speedLimitOverlayEnabled: true })

  for (const shot of pages) {
    // A taller window shows the Overview's second row (car status) under the charging card.
    if (shot.tall)
      await page.setViewportSize({ ...options.viewport, height: name === 'mobile' ? 1500 : 1100 })
    await page.goto(base + shot.path)
    await settled(page)
    if (shot.prepare) await shot.prepare(page)
    // A focused control scrolls the page a little on its way in; the gallery shows it from the top.
    await page.evaluate(() => document.querySelector('.main-content')?.scrollTo({ top: 0 }))
    const file = `${name}-${shot.file}.webp`
    await writeFile(new URL(file, docs), await webp(page, await page.screenshot()))
    console.log(`screenshots: ${file}`)
    if (shot.tall) await page.setViewportSize(options.viewport)
    if (shot.after) await shot.after(page)
  }
  await context.close()
}

async function signIn(page) {
  await page.goto(`${base}/login`)
  await page.fill('#username', process.env.DEMO_USERNAME ?? 'demo')
  await page.fill('#password', process.env.DEMO_PASSWORD ?? 'demo')
  await Promise.all([page.waitForURL(`${base}/`), page.click('button[type="submit"]')])
}

async function saveSettings(page, section, changes) {
  const status = await page.evaluate(
    async ({ section, changes }) => {
      const response = await fetch(`/api/settings/${section}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(changes),
      })
      return response.status
    },
    { section, changes },
  )
  if (status >= 300) throw new Error(`Saving the screenshot settings answered ${status}`)
}

// Loaded, skeletons gone, and a moment for the map's tiles and the charts' animations.
async function settled(page) {
  await page.waitForLoadState('networkidle').catch(() => {})
  await page
    .waitForFunction(() => !document.querySelector('.skeleton'), null, { timeout: 20_000 })
    .catch(() => {})
  await page.waitForTimeout(3000)
}

// One trip, snapped to the roads and coloured against the speed limits, as the gallery shows it.
async function selectTrip(page) {
  const trips = page.locator('.trip-list__button')
  // On a phone the list can sit in a closed panel; the map alone is shot then.
  if (
    !(await trips
      .first()
      .isVisible()
      .catch(() => false))
  )
    return
  const toUtrecht = trips.filter({ hasText: 'Utrecht' }).first()
  await ((await toUtrecht.count()) > 0 ? toUtrecht : trips.first()).click()
  await settled(page)
}

// The notification panel over the Overview, from the bell (in the sidebar on a wide screen).
async function openNotifications(page) {
  const bell = page.locator('.sidebar-notif-btn:visible, .notif-bell:visible').first()
  await bell.click()
  await page.locator('.notif-item').first().waitFor()
  await page.waitForTimeout(500)
}

// The demo car parked on the drive and charging, with the boot and a window open, so the Overview
// shows the live charging card and the car status drawing with something to point out.
async function parkAndCharge(page) {
  await setDemoStatus(page, {
    engineRunning: false,
    speed: 0,
    chargerConnected: true,
    isCharging: true,
    trunkOpen: true,
    driverWindowOpen: true,
  })
  await page.reload()
  await settled(page)
}

// Back on the road as the demo starts, for the pages shot after it.
async function driveOn(page) {
  await setDemoStatus(page, {
    engineRunning: true,
    speed: 40,
    chargerConnected: false,
    isCharging: false,
    trunkOpen: false,
    driverWindowOpen: false,
  })
}

async function setDemoStatus(page, override) {
  const status = await page.evaluate(async (override) => {
    const [vehicle] = await (await fetch('/api/vehicles', { credentials: 'include' })).json()
    const response = await fetch(`/api/demo/status/${vehicle.vin}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(override),
    })
    return response.status
  }, override)
  if (status >= 300) throw new Error(`Changing the demo car answered ${status}`)
}

// The browser encodes WebP itself, so the script needs no image library.
async function webp(page, png) {
  const dataUrl = await page.evaluate(
    async (source) => {
      const image = new Image()
      image.src = source
      await image.decode()
      const canvas = document.createElement('canvas')
      canvas.width = image.naturalWidth
      canvas.height = image.naturalHeight
      canvas.getContext('2d').drawImage(image, 0, 0)
      return canvas.toDataURL('image/webp', 0.85)
    },
    `data:image/png;base64,${png.toString('base64')}`,
  )
  return Buffer.from(dataUrl.slice(dataUrl.indexOf(',') + 1), 'base64')
}
