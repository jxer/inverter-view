# Original-screen control checklist

All 20 normal configuration controls and all six calibration controls from the supplied Techfine 1.03 screen are implemented. Ordering follows its left and middle parameter columns. Display labels use plain English. The old password boxes were intentionally removed.

| Original normal control | Inverter View label | Address | Group |
| --- | --- | ---: | --- |
| Input Voltage Range | AC input voltage range | 0 | AC / operating modes |
| Input Freq Range | AC input frequency range | 1 | AC / operating modes |
| Input Check Speed | AC input detection speed | 12 | AC / operating modes |
| Inv Output Voltage | Configured AC output voltage | 11 | AC / operating modes |
| Inv Output Freq | AC output frequency | 13 | AC / operating modes |
| Work Mode | Power-source priority | 2 | AC / operating modes |
| Charge Mode | Charging-source priority | 3 | AC / operating modes |
| Ac charge Cur | AC charging current limit | 4 | AC / operating modes |
| Solar Charge Cur | Solar charging current limit | 5 | AC / operating modes |
| Load Offline Beep | Disconnected-load alert | 19 | AC / operating modes |
| Bulk Voltage | Bulk charging voltage | 6 | Battery / options |
| Float Voltage | Float charging voltage | 7 | Battery / options |
| Lock Voltage | Low-battery shutdown voltage | 8 | Battery / options |
| Dc To Ac Voltage | Battery-to-utility threshold | 9 | Battery / options |
| Ac To Dc Voltage | Utility-to-battery threshold | 10 | Battery / options |
| BAT CAP Limit | Battery-capacity limiting | 17 | Battery / options |
| Load T Limit | Load limiting | 18 | Battery / options |
| Fault Restart | Automatic restart after fault | 14 | Battery / options |
| Backlight Control | Display backlight | 15 | Battery / options |
| Beep Enable | Audible alerts | 16 | Battery / options |

| Original calibration control | English label | Address | Live write evidence |
| --- | --- | ---: | --- |
| 输出电压 | AC output voltage | 48 | Not tested |
| 输入电压 | AC input voltage | 49 | Not tested |
| 电池电压 | Battery voltage | 50 | No effect observed |
| 充电电流 | Charging current | 51 | Not tested |
| 输出负载 | Load measurement | 52 | Not tested |
| 带载能力 | Protection / load capacity | 53 | Not tested |

Calibration editors accept the original utility's adjustment ranges. Sending and test/restore actions apply to the selected row. Exact percentage semantics are inferred from the PC display, not verified firmware behavior.

## Reset actions

The original **Reset Param** calls a local editor-reset routine. It does not send the device factory reset command. Likewise, **复位校准参数** resets local calibration editors. These are reproduced as **Reset drafts** on the appropriate page.

| Original action | New action | Behavior |
| --- | --- | --- |
| Reset Param | Reset drafts | Local proposed normal values only |
| Reset Sys Time | Reset runtime | Unverified CRST / CRSYST device command |
| Factory Reset | Factory reset | Unverified CRPA / CREPAM device command |
| 复位校准参数 | Reset drafts on Calibration | Local proposed calibration values only |
| 恢复校准默认值 | Factory calibration reset | Unverified CRCA / CRECAL device command |

## Information fields

Inverter and charger firmware, machine/bank identity, AC input/output voltage and frequency, battery voltage/capacity, AC and solar charging current, load, PV voltage, inverter and charger temperatures, runtime, controller code and charger code are displayed. MCU temperature is explicitly **Not reported** rather than fabricated. Machine status shows mode, controller/charger flags, battery-full indication, charging activity, warnings and faults. Raw flags and the unmapped extension remain available under diagnostics.

The original utility prefixes a decimal controller code with `0x`; Inverter View formats the raw value as actual hexadecimal. Output voltage is not automatically doubled for split phase.
