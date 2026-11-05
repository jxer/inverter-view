# Additional OEM and service-utility leads

Research performed November 4, 2026 local time / November 5 UTC. None of these leads demonstrated a working calibration command on the tested machine.

## Matching service software

A publicly shared V-Series installer contained a PC executable with the **same SHA-256** as the tested original 1.03 executable:

`92c694b3850859d3f6f780911557a4a6b9ac179b2e26c53de06a425a188f5121`

It therefore did not supply an alternate implementation of the calibration command. Source context: [Fivestar / Sunmagic RS232 discussion](https://mybroadband.co.za/forum/threads/fivestar-sunmagic-rs232-protocol-decoding.1242521/).

The [public PowMr-related discussion and capture](https://github.com/softwarecrash/Solar2MQTT-ESP8266/discussions/234) identify the same PC utility and VPHB family, but contain no CSCA/CSICAL calibration-write success. Its machine identifier, firmware and status-frame length differ. Shared boards remain a lead rather than a proven firmware equivalence.

## VEVOR manual / Huahu lead

The VS8048AMN manual mirrored at [device.report](https://device.report/m/2612b729e03b98ea5f845e9c6002d941cf71cf196c36d933dfd0cbe19c2012d3) has an internal document identifier containing **UD8048BAMYN** and names **Guangdong Huahu New Energy Technology Co., Ltd.** as manufacturer. This is a manual-mirror observation, not verification of a PCB identifier. It does not establish who wrote firmware 2.10.

The [WOWTIGER Chinese site](https://www.wowtigerenergy.com/cn/) identifies that company, and its [Chinese support page](https://www.wowtigerenergy.com/cn/service/faq.html) contains general installation and monitoring guidance. No matching service-calibration utility or firmware image was located there.

## Techfine Chinese and English material

The [Chinese service site](https://techfine.net/service.html) links UD low-frequency product pages, but the UD product endpoint repeatedly timed out during this research. The [English download center](https://techfinepv.com/download/) contains manuals and certificates; the checked material did not provide a VPHB calibration protocol or firmware image.

The [VS-series EMC certificate](https://techfinepv.com/wp-content/uploads/EMC_Inverter_VS-Series_ENS2508290215E00101C_2025-12-05.pdf) explicitly includes **VS8048AM** and names Guangdong Techfine. It is a one-page certificate, not a test report with PCB photographs or MCU details. The UD AMT/AWT EMC and LVD links also resolve to one-page certificates. They provide product-family context, not proof of calibration compatibility.

## Most useful unresolved evidence

- A successful CSCA/CSICAL write capture on any identified V-Series firmware.
- A service utility explicitly intended for the VS8048AMN / UD8048BAMYN board and firmware 2.10.
- Firmware release notes or service documentation identifying calibration prerequisites, writable parameters and persistence behavior.
- PCB revision and MCU identifiers from existing manufacturer documentation or already available board photographs.

The public Chinese TQF-AllInOne source contains calibration commands for a different protocol family. Exact revisions, command formats and failed compatibility probes are preserved in [chinese-reference-findings.txt](chinese-reference-findings.txt). Neither those source files nor the unrelated MPP Solar BTA example demonstrate that the same command works here.

No vendor was contacted, no firmware was flashed, and no undocumented factory-entry or reset command was sent during the investigation.
