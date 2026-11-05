using System;

namespace InverterDesk
{
    public static class HelpGuide
    {
        public const string Introduction=
            "Choose a setting above to see what it does.\r\n\r\n"+
            "CURRENT AND NEW VALUES\r\nCurrent is the value reported by the inverter. New value is your proposed edit. Amber marks an edit that has not been confirmed on the inverter. Teal marks a reported value that changed since connecting.\r\n\r\n"+
            "SAVE AND RESTORE SETTINGS\r\nSave settings writes the reported configuration to a JSON file. To restore it, connect to the same model, bank voltage and firmware, click Restore settings, choose the file and review the Current and Saved values. Click Restore selected settings to apply the checked rows. Matching values are skipped; experimental calibration must be selected separately. Each change is verified. If a change fails, the app tries to revert that step and stops; earlier successful changes remain applied. Older Inverter View snapshot files can also be loaded.\r\n\r\n"+
            "APPLYING A CHANGE\r\nSelect the setting, enter the new value and click Apply selected setting. The app reads the device again before sending and checks three readbacks afterward. Read the result message; a sent command alone does not confirm a change.\r\n\r\n"+
            "BATTERY VOLTAGES\r\nEnter the voltage for your whole battery bank. Use the charging and discharge limits specified for your battery. The charging target and the measured battery voltage are different values.\r\n\r\n"+
            "RESET BUTTONS\r\nUse current values discards your edits. Reset drafts fills the editors with default values without sending them. Factory reset, Factory calibration reset and Reset runtime send separate device commands and ask for confirmation. These device reset commands are unverified.\r\n\r\n"+
            "CALIBRATION\r\nThe calibration commands are experimental. Battery-voltage calibration has shown no effect on the tested inverter; the other five have not been tested. Test, then restore attempts a small change and automatically sends the original value back. A lost connection can prevent restoration.\r\n\r\n"+
            "DISPLAY AND CONNECTION\r\nDark mode and the °C / °F switch remember your preferences. Machine status and the battery chart share the available space equally. Hover over a chart point for its voltage. Select your USB-to-RS232 adapter's COM port. The tested connection is 2400 baud, 8 data bits, no parity, 1 stop bit and no flow control. Changing the baud selector changes the PC connection, not the inverter's stored baud rate.";

        public static string For(int address)
        {
            switch(address)
            {
                case 0: return "Sets how much variation is accepted in the incoming utility or generator voltage.\r\n\r\nNarrow uses a tighter input range. Wide accepts a broader range. Choose the range appropriate for the incoming supply and connected equipment. This setting does not set the AC output voltage.";
                case 1: return "Sets the accepted frequency range for incoming utility or generator power.\r\n\r\nNarrow accepts a tighter range around normal mains frequency. Wide tolerates larger variations. The output-frequency setting is separate.";
                case 2: return "Chooses which energy source supplies the load first.\r\n\r\nUtility first: prefers the incoming AC supply.\r\n\r\nSolar first: uses solar when available, with battery support while solar is present; utility supplies the load when solar is absent or the battery reaches its transfer threshold.\r\n\r\nBattery first: uses solar and battery ahead of utility, including battery operation without solar, until the low-battery or transfer threshold is reached.\r\n\r\nCharging-source priority is a separate setting.";
                case 3: return "Chooses the sources allowed to charge the battery.\r\n\r\nUtility first: allows charging from utility and solar.\r\n\r\nSolar first: prefers solar charging and allows utility charging when battery voltage reaches the lower utility-changeover threshold. Utility charging stops at the upper threshold.\r\n\r\nSolar only: uses solar charging without utility charging.\r\n\r\nThis is separate from the priority used to power the load.";
                case 4: return "Limits AC charging current as a percentage of the inverter's available AC charging capacity.\r\n\r\nFor example, 50% requests half of that charging capacity; it does not mean 50 amps. The actual current also depends on charging stage and the battery. Keep the combined charging sources within the battery's permitted charge current.";
                case 5: return "Limits solar charging current as a percentage of the solar charger's available capacity.\r\n\r\nThe percentage is not an amp value. Actual current depends on solar conditions, battery voltage and the charging stage. Consider AC charging too when checking the battery's total charge-current limit.";
                case 6: return "Sets the constant-voltage target used during the main charging stage, also called boost or absorption charging.\r\n\r\nEnter the full battery-bank voltage. Use the target specified for your battery chemistry and battery manufacturer. Raising this target is not a way to correct an inaccurate voltage reading; measurement calibration is separate.";
                case 7: return "Sets the lower voltage target used to maintain the battery after the main charging stage.\r\n\r\nEnter the full battery-bank voltage and follow the battery manufacturer's float-charge guidance. Different battery chemistries have different requirements.";
                case 8: return "Sets the low battery-voltage shutdown threshold used to limit deep discharge.\r\n\r\nWhen the battery falls below the applicable shutdown threshold, inverter output may stop. Use a full-bank value suitable for the battery's discharge limits. The battery's own protection system can also disconnect it independently.";
                case 9: return "Sets the lower battery-voltage threshold for returning to utility support. It is also used to allow utility charging in solar-first charging mode.\r\n\r\nA lower value allows more battery discharge before utility support is requested. Use a full-bank voltage and keep it consistent with the battery's operating limits and the higher return threshold.";
                case 10: return "Sets the higher battery-voltage threshold for leaving utility charging and returning to solar/battery preference.\r\n\r\nIn solar-first charging mode, reaching this threshold ends utility charging. The gap above the lower changeover threshold helps avoid frequent switching. Enter the full-bank voltage.";
                case 11: return "Sets the requested AC output-voltage target. Choose the value required by the inverter and connected equipment.\r\n\r\nThe current configured target and the measured AC output are separate readings. On a split-phase unit, a reported measurement may represent one output channel; do not assume it should equal the configured whole-output target.";
                case 12: return "Controls how quickly the inverter reacts to changes in the incoming AC supply.\r\n\r\nFast reacts more quickly to fluctuations. Normal is the middle setting. Slow is less responsive to brief variations. Choose a setting that suits the utility or generator supply and the connected loads.";
                case 13: return "Sets the inverter's AC output frequency to 50 Hz or 60 Hz.\r\n\r\nUse the frequency required by the connected equipment and electrical system. Changing this is separate from changing the accepted input-frequency range.";
                case 14: return "Controls automatic restart attempts after supported overload or short-circuit faults.\r\n\r\nOn permits automatic restart attempts. Off keeps automatic restart disabled. Correct the cause of a fault before allowing the affected loads to run again.";
                case 15: return "Controls the inverter's own display backlight.\r\n\r\nOn keeps it lit. Off keeps it dark. Timed turns it off after inactivity. This does not change the Windows application's appearance.";
                case 16: return "Enables or disables the inverter's audible alerts, including fault beeps.\r\n\r\nDisabling the buzzer silences an indication; it does not clear a fault or change the underlying protection settings.";
                case 17: return "Controls the intelligent low-battery protection option shown as BAT CAP Limit in the original utility.\r\n\r\nIt is an enable/disable protection setting, rather than a battery amp-hour capacity entry. The operating guidance recommends leaving this protection enabled.";
                case 18: return "Controls protective load limiting, including protection associated with transformer temperature.\r\n\r\nThe operating guidance recommends leaving it enabled. This is separate from the reported load percentage and from the load-measurement calibration control.";
                case 19: return "This is the original utility's disconnected-load or load-alarm option.\r\n\r\nThe operating instructions mark this setting as unsupported for the described inverter. It is included for compatibility with the PC utility, but changing it may have no effect.";
                case 48: return Calibration("AC output-voltage measurement","-2.5% to +2.5%, in 0.1% steps",false);
                case 49: return Calibration("AC input-voltage measurement","-2.5% to +2.5%, in 0.1% steps",false);
                case 50: return Calibration("battery-voltage measurement","-2.5% to +2.5%, in 0.1% steps",true);
                case 51: return Calibration("charging-current measurement","-15% to +15%, in 1% steps",false);
                case 52: return Calibration("load measurement","-10% to +10%, in 1% steps",false);
                case 53: return Calibration("protection / load-carrying capacity","-5% to +5%, in 1% steps",false);
                default: throw new ArgumentException("No help topic for this setting.");
            }
        }
        private static string Calibration(string purpose,string range,bool battery)
        {
            return "Experimental adjustment for "+purpose+".\r\n\r\nThe original utility offers "+range+". The exact effect and firmware support have not been established.\r\n\r\n"+
                (battery?"Battery-voltage calibration commands have produced no observable change on the tested inverter.":"This calibration control has not been tested on hardware.")+
                "\r\n\r\nSend selected command tries the requested value and checks readbacks. Test, then restore makes a bounded temporary attempt and sends the original value back afterward. Review the result; sending is not proof of acceptance.\r\n\r\nCalibration changes a measurement or protection adjustment, not the normal charging-voltage targets.";
        }
    }
}
