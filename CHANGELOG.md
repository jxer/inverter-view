# Changes

## 1.0.5 — 2026-11-09

- Named operating state 4 **Charging only** in both the dashboard's Operating mode row and Machine status, replacing the generic Device state 4 label.
- Explained that the inverter's AC output is off in this mode while solar or AC battery charging remains available. The separate charging indicators continue to show which sources are active.
- Updated the command-line reader, protocol notes and dashboard screenshot to match.

## 1.0.4 — 2026-11-06

- The **AC → Battery** and **Solar → Battery** dashboard cards now show DC charging amps and approximate watts for each source. Each calculation uses the reported battery voltage from the same status reply and rounds to the nearest watt.
- Both cards use the same units, caption and layout, with distinct AC and solar panel icons. The settings help explains how to read both measurements.

## 1.0.3 — 2026-11-05

- Renamed the AC charging dashboard card to AC → Battery and displayed its value in A DC, with the caption Battery-side charging current.
- Clarified the settings help and protocol notes: this reading is the AC charger's DC output into the battery, not AC input amperage. Measurement decoding and device commands are unchanged.
- Added a dashboard screenshot with clearly labeled example readings to the README.

## 1.0.2 — 2026-11-04

- Moved the connection indicator below Restore settings in the sidebar and removed the empty dashboard header row.
- Enlarged the machine-status heading and made status text scale with the available panel space, retaining the status colors.
- Removed the dashboard's battery-capacity/load explanation and gave its space to the status and voltage panels.
- Removed Refresh, widened Connect/Disconnect, and update the port list when opened or connecting.
- Fixed the footer remaining on Disconnecting after a normal disconnect. Connection errors and completed write/restore results remain visible.

## 1.0.1 — 2026-11-04

- Moved the RS232 port, baud rate, Refresh, Connect and Save/Restore settings controls into the sidebar.
- Matched the temperature control to the Dark mode slider, with the selected °C/°F unit shown on its thumb.
- Enlarged the dashboard card titles and captions, added gauge/battery/AC/solar icons, and padded the icons to prevent clipping.
- Divided the machine-status and battery-chart area equally and removed time labels and timestamp tooltips from the chart.
- Removed the redundant Dashboard heading and the battery card's bank/time subtitle.
- Added darker bold text, pale blue surfaces and a blue Windows 11 title bar in light mode.
- Centralized the displayed, executable, saved-settings and portable-package versions in VERSION. Each new version requires its own changelog entry before building.

## 1.0.0 — 2026-11-04

- Initial portable Windows application, named Inverter View.
- Live serial monitoring, lossless host-side RX logging and colored machine-status values.
- Load-first Dashboard with battery voltage, AC charging current and solar charging.
- Battery-voltage history chart.
- Dark/light themes and Celsius/Fahrenheit display preferences.
- Save settings and Restore settings with per-setting selection, matching-device checks, three-readback verification and explicit partial-failure reporting. Older snapshot exports remain readable.
- Twenty normal settings grouped like the original utility, current values and proposed edits kept separate, and changes since connection marked.
- Six calibration controls available for experiments with explicit evidence labels and test/restore support.
- Separate local draft reset and device factory/runtime reset actions.
- Full-bank battery voltage input, three-readback verification, bounded temporary tests and restoration failure reporting.
- No vendor branding, password gates, preview button or reply counter in the interface.
- Basic settings help with direct links from the selected setting, based on the supplied manuals.
- Short change confirmation and a clear Change successful indicator after verified readback.
- Single-line title, supplied application icon, connection checkbox, footer version and GitHub link.
- Included protocol notes, investigation history and serial evidence. Calibration remains unresolved.
