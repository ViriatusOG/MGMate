# MGMate for Android

The native Android app: car controls, charging, the car's location and trips. What it does and how to install it is in the [main README](../README.md#android-app). This page is for building it.

## How it is made

- Kotlin and Jetpack Compose, one activity, no navigation library. `minSdk` 26 (Android 8).
- `data/Api.kt` talks to the server's REST API with the same cookie session as the web app (`POST /api/auth/login`). The server needs no change for the app.
- `data/LiveHub.kt` is a small client for the server's SignalR hub (`/hubs/telemetry`), which pushes new readings and the car's answer to a command.
- `data/Session.kt` keeps the server address and sign-in in `EncryptedSharedPreferences`.
- `MainActivity.kt` holds the lock (`androidx.biometric`: fingerprint, face or the phone's screen lock).
- The map is [osmdroid](https://github.com/osmdroid/osmdroid) with OpenStreetMap tiles.
- UK units throughout: miles, mph, mi/kWh, degrees Celsius. English only.

The app never asks the car for anything by itself. Readings come from `GET /api/vehicles/{vin}/status`, which the server answers from its database, so nothing here may poll a command or the `refresh` command.

## Build

With Android Studio, open the `android` folder. From a shell, with JDK 17 and the Android SDK installed:

```bash
cd android
./gradlew testDebugUnitTest assembleDebug
```

The APK is written to `app/build/outputs/apk/debug/`.

## The builds GitHub makes

The [Android app workflow](../.github/workflows/android.yml) runs on every pull request and push to `main` that touches `android/`. It runs the tests, builds a release APK and attaches it to the run as the `mgmate-android` artifact. A build of `main` also replaces `mgmate.apk` on the rolling [`android-latest`](https://github.com/ViriatusOG/MGMate/releases/tag/android-latest) pre-release, which is where the README sends people to download it, and writes a `version.json` (`versionCode`, `versionName`) beside it. The version comes from the git tags, like the server's.

## Updates from inside the app

`data/Updater.kt` reads that `version.json` once each time the app is started and when asked under **Settings > Update**. When its `versionCode` (the number of commits on `main`) is higher than the installed build's, Settings offers the update: the app downloads `mgmate.apk` into its cache folder and opens Android's installer on it through a `FileProvider`. The updater has its own HTTP client, so the server's session cookie never goes to GitHub.

This only works between builds signed with the same key, which is why the signing key below matters.

## Signing

Android only installs an update over an app signed with the same key. The key is never in the repository: the workflow reads it from four repository secrets.

| Secret | What it holds |
|---|---|
| `MGMATE_KEYSTORE_BASE64` | The keystore file, base64 encoded (`base64 -w0 mgmate.jks`) |
| `MGMATE_KEYSTORE_PASSWORD` | The keystore's password |
| `MGMATE_KEY_ALIAS` | The key's alias |
| `MGMATE_KEY_PASSWORD` | The key's password |

Make a key once and keep a copy somewhere safe, because losing it means uninstalling the app before a newer one installs:

```bash
keytool -genkeypair -v -keystore mgmate.jks -alias mgmate -keyalg RSA -keysize 4096 -validity 10000
```

Without the secrets the workflow still builds, signed with a throwaway debug key: fine for a first try, but each such APK only installs after the previous one is uninstalled.
