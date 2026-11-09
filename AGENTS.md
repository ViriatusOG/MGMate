# Notes for AI coding agents

These rules apply to every change in this repository, whoever or whatever makes it.

## Keep the README up to date

Every pull request that adds a feature or changes how something works must update `README.md` in the same pull request:

- Describe the new feature, or correct the text that no longer matches, in the feature list and in the section about that page. Remove what was taken out.
- When a page looks different, retake the screenshots from the demo with `pnpm screenshots` in `frontend/` (see `documentation/screenshots/README.md`) and commit them. Add a new page to `PAGES` in `frontend/scripts/screenshots.mjs` and to the screenshot gallery.
- Update `documentation/CONFIGURATION.md` for new settings and `documentation/ARCHITECTURE.md` for how it works.
- Tick the README item in the pull request template.

A bug fix that does not change what the user sees or does needs no README change. The full contributor guide is `.github/CONTRIBUTING.md`.

## Other standing rules

- The app is English only. Don't add translations; new strings go in `frontend/src/locales/en.json`.
- Units in examples and screenshots are UK: miles, mi/kWh, psi, £.
- Never put a personal name in code, the licence, commits, the README or anything public; the maintainer is credited as ViriatusOG. Keep the upstream MIT notice for GarageStack in `LICENSE`.
- Internal names such as the database, MQTT user, cookie and settings keys stay `garagestack`, so existing installs keep their data.
