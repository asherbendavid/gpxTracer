# GPX Route Player

A small WinForms utility that plays back a GPX route in real time on an OpenStreetMap map, so a simulated GPS journey can be watched visually while a separate app under test consumes the same route.

## Purpose

This tool exists to support testing of [Vaart](https://github.com/asherbendavid/vaart), a GPS speedometer/trip computer Android app. Testing relies on a GPX Location Spoofer app feeding mock GPS data to a phone running Vaart. Previously, verifying that Vaart was displaying the correct information at any given moment meant running a second phone with Waze alongside it, to have a visual reference for where the simulated route currently was.

GPX Route Player replaces that second phone. It loads the same GPX file used by the spoofer, plays it back at 1× real time using the file's own timestamps, and shows a marker moving along the route on a live OSM map — giving an independent, at-a-glance reference to check Vaart's output against.

It is a personal dev/test tool, not a general-purpose GPX viewer. It intentionally does one thing.

## Requirements

- Windows 10/11 with the WebView2 Runtime (present by default on Windows 11)
- .NET Framework 4.8
- Internet connection for OSM tile fetching during playback (the Leaflet library itself is bundled locally and does not require a connection to load)

## How to use

1. Launch the app. It opens maximized with a blank map.
2. Click **Load GPX** and select a `.gpx` file (exported from gpx.studio, or any GPX 1.1 file with timestamped `<trkpt>` elements).
   - The map auto-fits to the loaded route, drawn as a blue polyline.
   - The loaded filename appears in the status bar.
   - If the file has no timestamp data, a message box explains this and the file is rejected — any previously loaded route stays active.
3. Click **Play**. A 5-second countdown appears in the status bar ("Starting in 5s...") before playback begins, giving time to switch focus to the phone/app under test.
4. During playback, a red marker moves along the route in real time, matching the GPX file's own timestamps (1× speed, no multiplier). The progress slider advances to show position along the route. If the map is panned or zoomed away from the marker, it will smoothly pan back into view without changing zoom level.
5. Click **Stop** at any time to halt playback. The marker stays exactly where it stopped — useful for holding position at the moment an issue is spotted in the app under test.
6. Clicking **Play** again always restarts from the beginning of the route.

## Design decisions

- **VB.NET WinForms, .NET Framework 4.8** — matches the developer's existing tooling and target machine; no need for .NET Core/5+.
- **WebView2 + local Leaflet.js** — WebView2 is bundled with Windows 11 and needs no extra runtime install. Leaflet's JS/CSS are stored locally in the project (not pulled from a CDN), so the map frame loads instantly even with no internet — only the OSM tile imagery itself requires a live connection, which is unavoidable.
- **Slider is display-only** — the progress slider reflects playback position but is not interactive. Real-time seeking was considered out of scope for the initial version, since the tool's job is to passively mirror a route already being played by the spoofer, not to control it.
- **Linear interpolation within GPX segments** — distance and bearing between consecutive GPX points are effectively constant across short segments (the sample data used is typically a few meters and 1–10 seconds apart), so simple linear interpolation between points is visually indistinguishable from full great-circle interpolation, at a fraction of the complexity.
- **Whole-file rejection on missing timestamps** — if even one `<trkpt>` lacks a `<time>` element, the entire file is rejected rather than silently skipping bad points. A route with inconsistent timing data isn't safe to use for interpolated playback.
- **5-second countdown before playback** — gives time to switch window focus to the device/app being tested before the route starts moving.
- **No looping, no speed multiplier, no route editing** — deliberately out of scope. This tool has one job: play a route once, at real speed, as a visual reference.
- **Last-used GPX folder persisted via `My.Settings`** — routes are typically all saved in the same folder, so the file browser remembers it between sessions. No other state is persisted (e.g. window position/size), since the window always opens maximized.

## Possible future expansion

- **Real-time seeking** — make the progress slider interactive, allowing the marker to be dragged to a specific point along the route (currently shelved as a deliberate v1 scope cut).
- **Speed multiplier** — optional playback faster/slower than 1× real time, for quickly skimming through a long route without waiting for full real-time playback.
- **Heading/direction indicator** — show the marker's current bearing (e.g. as an arrow or rotated icon) rather than a plain dot, useful for junctions where direction of travel matters.
- **Configurable update interval** — currently a hardcoded constant (`UPDATE_INTERVAL_MS`); could be exposed as a UI setting if finer-grained playback smoothness is ever needed.
- **Multiple route support** — loading and comparing more than one GPX route simultaneously (explicitly out of scope for v1).
- **Instant vs. animated pan** — the auto-recenter-on-drift behavior currently uses Leaflet's animated `panTo`; could be made instant (`{ animate: false }`) if snappier tracking is preferred during fast-moving test routes.

## Project structure

```
/GPX_Tracer
  TracerForm.vb       - main window: UI, playback state machine, GPX-to-map wiring
  GpxParser.vb         - GPX file parsing (XDocument/LINQ to XML) and GpxPoint structure
  map.html             - local Leaflet map, loaded into the WebView2 control
  leaflet/              - local copies of leaflet.js / leaflet.css / marker images
```
