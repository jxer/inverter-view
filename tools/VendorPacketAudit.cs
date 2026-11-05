using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;

// Offline audit of the ORIGINAL vendor assembly, not recompiled decompiled code.
// Bypasses its constructor. No SerialPort is created, and no COM port is opened.
// The original sender builds its buffer, then catches its null-port exception.
internal static class VendorPacketAudit
{
    private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, InstancePrivate);
        if (field == null) throw new Exception("Missing field: " + name);
        if (value != null && !field.FieldType.IsInstanceOfType(value))
            value = Convert.ChangeType(value, field.FieldType);
        field.SetValue(target, value);
    }

    private static byte[] Build(Type type, bool calibration, int address, int value)
    {
        object instance = FormatterServices.GetUninitializedObject(type);
        Set(instance, "datatosend", new byte[128]);
        Set(instance, "settingDataBuf", new byte[20]);
        Set(instance, "serialPort1", null);
        Set(instance, "singleSettingPosition", address);
        Set(instance, "settingValue1st", 0);
        Set(instance, "settingValue2nd", value);
        Set(instance, calibration ? "uartSendSingleCalFlag" : "uartSendSingleSettingFlag", 1);
        MethodInfo sender = type.GetMethod("Command_Send_Operation", InstancePrivate);
        if (sender == null) throw new Exception("Missing vendor sender");
        sender.Invoke(instance, null);
        if (type.GetField("serialPort1", InstancePrivate).GetValue(instance) != null)
            throw new Exception("Unexpected serial port object");
        byte[] buffer = (byte[])type.GetField("datatosend", InstancePrivate).GetValue(instance);
        byte[] packet = new byte[5 + buffer[4]];
        Array.Copy(buffer, packet, packet.Length);
        return packet;
    }

    private static void Check(Type type, string label, bool calibration, int address, int value, string expected)
    {
        string actual = BitConverter.ToString(Build(type, calibration, address, value)).Replace('-', ' ');
        Console.WriteLine(label + ": " + actual);
        Console.WriteLine("MATCH: " + String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase));
        if (!String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Vendor packet mismatch: " + label);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new Exception("Pass the path to the original vendor executable");
            string path = Path.GetFullPath(args[0]);
            using (SHA256 sha = SHA256.Create())
            using (FileStream file = File.OpenRead(path))
                Console.WriteLine("ASSEMBLY_SHA256: " + BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant());
            Assembly assembly = Assembly.LoadFrom(path);
            Type form = assembly.GetType("V_Series_Runtime.V_Series_Runtime_Form", true);
            RuntimeHelpers.RunClassConstructor(form.TypeHandle);
            Check(form, "bulk_14_6", false, 6, 146, "43 53 50 41 0A 43 53 49 50 41 4D 06 00 92 98");
            Check(form, "battery_cal_plus_0_1", true, 50, 126, "43 53 43 41 0A 43 53 49 43 41 4C 32 00 7E B0");
            Check(form, "battery_cal_restore_0_0", true, 50, 125, "43 53 43 41 0A 43 53 49 43 41 4C 32 00 7D AF");
            int count=0;
            foreach(var setting in InverterDesk.Catalog.All)
            foreach(int value in new[]{setting.Min,InverterDesk.Catalog.DefaultRaw(setting.Address),setting.Max})
            {
                string original=BitConverter.ToString(Build(form,setting.Calibration,setting.Address,value));
                string replacement=BitConverter.ToString(InverterDesk.Protocol.Write(setting,value));
                if(original!=replacement)throw new Exception("Packet mismatch for "+setting.Name+" raw "+value);
                count++;
            }
            Console.WriteLine("PASS: "+count+" packets across all 26 controls match the original vendor builder. No COM port created or opened.");
            return 0;
        }
        catch (Exception exc)
        {
            Console.Error.WriteLine(exc.ToString());
            return 1;
        }
    }
}
