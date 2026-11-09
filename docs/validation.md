# Validation of version 1.0.5

## Offline checks

The build passed 28 protocol/transaction/settings-file test cases, including the complete original 90-frame capture. Coverage includes fragmented and coalesced replies, bad checksums and resynchronization, all six calibration addresses, full-bank voltage conversion, enum polarity, ignored writes, lost replies, partial-write failure, restoration failure, stale configuration and unexpected changes in other fields.

UI-model checks cover all 20 normal and six calibration controls, original two-column ordering, read-only current values, preserved edits, changed-since-connection indicators, local draft resets, °C/°F conversion, disconnected write guards, all 27 help topics, dark/light theme switching and machine-status colors. Disconnect-completion checks confirm the progress message clears, controls return to their disconnected state, and errors or completed write/restore results are preserved. Settings-file tests cover legacy exports, invalid files, model/firmware mismatch, selected restore, partial failure and cancellation between writes. Tests open no serial port.

An independent offline call into the original PC executable's packet builder matched **78 packets across all 26 controls**: minimum, default and maximum encoded values. No serial-port object was created. See [all-controls-packet-audit.txt](../evidence/all-controls-packet-audit.txt). This verifies packet compatibility with the PC utility, not acceptance by inverter firmware.

The charging-card UI check uses a 52.3 V battery, 120.6 V AC input and 63 V PV input. The dashboard shows 17 A DC and approximately 889 W from AC, and 0.7 A DC and approximately 37 W from solar. The test also changes battery voltage while holding both currents constant, then checks zero charging current from both sources.

The README screenshot shows the version 1.0.5 dashboard with example readings, including operating state 4 labeled **Charging only** in both dashboard locations. The two-column settings layout fits all 20 normal rows at the default window size.

The state 4 name reflects the owner's observation that the inverter output is off while solar or AC battery charging remains available. This release changes display wording; validation uses offline sample replies and opens no serial port.

## Communication hardware check

A development build with the same serial transport performed five status requests on COM4 at 2400/8N1. The journal confirms that every transmitted command was the fixed status heartbeat and that the serial port closed afterward. No setting, calibration or reset commands were sent in this check.

Last reported state:

- Machine 8048, inverter firmware 2.10, charger firmware 1.01, nominal 48 V bank.
- Bulk target 56.4 V (raw 141).
- All six calibration readbacks raw 125 / zero adjustment.
- Battery measurement 52.1 V; output channel 119.3 V at 60 Hz.
- No reported warning or fault flags.

See [app-live-final.json](../evidence/app-live-final.json) and its [raw-byte journal](../evidence/app-live-final.json.jsonl). These measurements are a historical validation snapshot, not a current remote dashboard.

## Packaging

The portable ZIP was reopened, its CRCs checked, and its embedded executable compared byte-for-byte with the tested build. The ZIP checksum is published in `dist/SHA256SUMS.txt`. The executable is unsigned. Proprietary vendor executables and decompiled vendor source are excluded from the distribution.

Calibration remains unresolved, and restoration after a real cable failure cannot be guaranteed by an offline test.
