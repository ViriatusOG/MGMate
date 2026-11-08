# Changelog

## MGMate

MGMate starts from GarageStack's `main` (November 2026) and is renamed throughout: the app, the .NET projects (`MGMate.*`), the frontend package, the docs and the images (`ghcr.io/viriatusog/mgmate`). The database name and user stay `garagestack`, and so do the browser storage keys, the session cookie and the MQTT topics, so an existing install keeps its data, settings and sign-in.

## GarageStack (upstream)

The notes for every release, with what changed, what to check before upgrading and which variables are new, are on the [GitHub releases page](https://github.com/joszz/GarageStack/releases). Each one is written by hand when the version is tagged (see [`documentation/RELEASING.md`](documentation/RELEASING.md)).

What has landed since the latest release is the list of [pull requests merged into `main`](https://github.com/joszz/GarageStack/pulls?q=is%3Apr+is%3Amerged+base%3Amain) after its date.
