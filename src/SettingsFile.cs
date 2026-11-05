using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace InverterDesk
{
    public static class SettingsFile
    {
        public sealed class Document
        {
            public string schema { get; set; }
            public Device device { get; set; }
        }
        public sealed class Device
        {
            public string schema { get; set; }
            public string rawHex { get; set; }
            public string machine { get; set; }
            public string firmware { get; set; }
            public string chargerFirmware { get; set; }
            public int nominalBatteryVoltage { get; set; }
            public Entry[] settings { get; set; }
        }
        public sealed class Entry
        {
            public int address { get; set; }
            public int raw { get; set; }
        }
        public static string Save(Status status)
        {
            return new JavaScriptSerializer().Serialize(new {
                schema="inverter-view.settings.v1", application="Inverter View "+AppInfo.Version,
                savedUtc=DateTime.UtcNow.ToString("o"), device=status.Snapshot()
            });
        }
        public static Status Read(string text)
        {
            if(string.IsNullOrWhiteSpace(text)||text.Length>1048576)throw new ArgumentException("The settings file is empty or too large.");
            var json=new JavaScriptSerializer { MaxJsonLength=1048576,RecursionLimit=32 };
            Document document=json.Deserialize<Document>(text);
            if(document==null||document.device==null||(!string.IsNullOrEmpty(document.schema)&&document.schema!="inverter-view.settings.v1"))throw new ArgumentException("This is not a supported Inverter View settings file.");
            Device device=document.device;
            if(device.schema!="inverter-desk.status.v1"||string.IsNullOrWhiteSpace(device.rawHex)||device.rawHex.Length>400)throw new ArgumentException("The file has no valid saved configuration.");
            Status saved;
            try{saved=new Status(Protocol.FromHex(device.rawHex));}catch(Exception ex){throw new ArgumentException("The saved configuration is damaged: "+ex.Message);}
            if(saved.Model!=device.machine||saved.Firmware!=device.firmware||saved.ChargerFirmware!=device.chargerFirmware||saved.Units*12!=device.nominalBatteryVoltage)throw new ArgumentException("The saved device information does not match its configuration.");
            if(device.settings==null||device.settings.Length!=Catalog.All.Length||device.settings.Any(e=>e==null)||device.settings.Select(e=>e.address).Distinct().Count()!=Catalog.All.Length)throw new ArgumentException("The settings list is incomplete or contains duplicates.");
            foreach(Setting setting in Catalog.All)
            {
                Entry entry=device.settings.SingleOrDefault(e=>e.address==setting.Address);
                if(entry==null||entry.raw!=saved.Value(setting))throw new ArgumentException("The saved setting values do not match the recorded configuration.");
            }
            return saved;
        }
        public static void CheckCompatible(Status saved,Status current)
        {
            if(saved==null||current==null||!saved.SameIdentity(current)||saved.Units!=current.Units)throw new ArgumentException("The saved settings are for a different model, battery-bank voltage or firmware. Connect the matching inverter before restoring.");
        }
    }

    public sealed class SettingsRestoreResult
    {
        public readonly List<KeyValuePair<Setting,WriteResult>> Steps=new List<KeyValuePair<Setting,WriteResult>>();
        public bool Success;
        public int Remaining;
        public string Message;
        public Status Final;
    }
    public static class SettingsRestore
    {
        // Restore individually, checking the entire configuration between writes.
        // A failed step uses the normal rollback path and stops the remaining steps.
        public static SettingsRestoreResult Apply(ITransport transport,Status saved,int[] addresses,Status expected,Action<string> progress,Action<Setting,WriteResult> completed,bool allowOtherProfile=false,Func<bool> cancelled=null)
        {
            var result=new SettingsRestoreResult { Final=expected };
            try
            {
                SettingsFile.CheckCompatible(saved,expected);Transactions.CheckProfile(expected,allowOtherProfile);
                if(addresses==null||addresses.Length==0||addresses.Distinct().Count()!=addresses.Length)throw new ArgumentException("Select one or more distinct settings to restore.");
                var plan=new List<Setting>();
                foreach(int address in addresses)
                {
                    Setting setting=Catalog.All.SingleOrDefault(s=>s.Address==address);
                    if(setting==null)throw new ArgumentException("The selection contains an unknown setting.");
                    Protocol.Write(setting,saved.Value(setting));Protocol.Write(setting,expected.Value(setting));
                    plan.Add(setting);
                }
                result.Remaining=plan.Count;
                foreach(Setting setting in plan)
                {
                    if(cancelled!=null&&cancelled()){result.Message="Restore stopped. "+result.Steps.Count+" settings restored; "+result.Remaining+" not attempted.";return result;}
                    WriteResult step=Transactions.Change(transport,setting,saved.Value(setting),result.Final,false,progress,allowOtherProfile);
                    result.Steps.Add(new KeyValuePair<Setting,WriteResult>(setting,step));result.Remaining--;
                    if(step.Final!=null)result.Final=step.Final;
                    if(completed!=null)completed(setting,step);
                    if(step.Error!=null||(!step.NoChange&&!step.ReadbackVerified))
                    {
                        result.Message="Restore stopped at "+setting.Name+". "+(result.Steps.Count-1)+" earlier settings restored; "+result.Remaining+" not attempted. "+step.Message;
                        return result;
                    }
                }
                result.Success=true;result.Message="Settings restored successfully.";
            }
            catch(Exception ex){result.Message="Restore stopped: "+ex.Message;}
            return result;
        }
    }
}
