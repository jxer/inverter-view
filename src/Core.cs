// Copyright (c) 2026 vevor-inverter-desk contributors. MIT license.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace InverterDesk
{
    public sealed class Setting
    {
        public int Address, Offset, Min, Max;
        public string Name, Group, Unit;
        public string[] Choices;
        public bool Calibration, BatteryVoltage;
        public decimal Scale = 1;
        public Setting(int address, string name, string group, int min, int max, string unit)
        { Address=address; Offset=address>=48?address-9:address+45; Name=name; Group=group; Min=min; Max=max; Unit=unit; Calibration=address>=48; }
        public decimal Number(int raw, int units)
        { return Calibration ? (raw-125)/Scale : BatteryVoltage ? raw*units/10m : raw/Scale; }
        public string Text(int raw, int units)
        { return Choices!=null ? raw>=1&&raw<=Choices.Length?Choices[raw-1]:"Unknown ("+raw+")" : Number(raw,units).ToString(Calibration||BatteryVoltage?"0.0":"0",CultureInfo.InvariantCulture)+" "+Unit; }
        public string Editable(int raw,int units)
        { return Choices!=null?Text(raw,units):Number(raw,units).ToString(CultureInfo.InvariantCulture); }
        public int Parse(string text, int units)
        {
            if(Choices!=null) { int p=Array.IndexOf(Choices,text); if(p<0)throw new ArgumentException("Choose a value from the list."); return p+1; }
            decimal v;
            NumberStyles style=NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingWhite|NumberStyles.AllowTrailingWhite;
            if(!decimal.TryParse(text,style,CultureInfo.CurrentCulture,out v)&&!decimal.TryParse(text,style,CultureInfo.InvariantCulture,out v))throw new ArgumentException("Enter a valid number.");
            if(BatteryVoltage&&units<=0)throw new ArgumentException("Battery-bank voltage has not been identified.");
            decimal raw=Calibration?v*Scale+125:BatteryVoltage?v*10/units:v*Scale;
            if(raw!=decimal.Truncate(raw))throw new ArgumentException("Use increments of "+(BatteryVoltage?units/10m:1/Scale).ToString(CultureInfo.InvariantCulture)+" "+Unit+".");
            if(raw<Min||raw>Max)throw new ArgumentException("Allowed range: "+Text(Min,units)+" to "+Text(Max,units)+".");
            return (int)raw;
        }
        public string Range(int units)
        { return Choices!=null?"Select one option":Text(Min,units)+" to "+Text(Max,units); }
    }
    public static class Catalog
    {
        private static Setting Choice(int a,string n,string g,params string[] c) { return new Setting(a,n,g,1,c.Length,""){Choices=c}; }
        private static Setting Volts(int a,string n,int min,int max) { return new Setting(a,n,"Battery charging",min,max,"V"){BatteryVoltage=true}; }
        public static readonly Setting[] All = new Setting[] {
            Choice(0,"AC input voltage range","AC output / input","Narrow","Wide"),
            Choice(1,"AC input frequency range","AC output / input","Narrow","Wide"),
            Choice(2,"Power-source priority","Operating modes","Utility first","Solar first","Battery first"),
            Choice(3,"Charging-source priority","Operating modes","Utility first","Solar first","Solar only"),
            new Setting(4,"AC charging current limit","Battery charging",10,100,"%"),
            new Setting(5,"Solar charging current limit","Battery charging",20,100,"%"),
            Volts(6,"Bulk charging voltage",135,150), Volts(7,"Float charging voltage",125,140),
            Volts(8,"Low-battery shutdown voltage",95,115), Volts(9,"Battery-to-utility threshold",115,125),
            Volts(10,"Utility-to-battery threshold",130,140),
            new Setting(11,"Configured AC output voltage","AC output / input",200,240,"V"),
            Choice(12,"AC input detection speed","AC output / input","Fast","Normal","Slow"),
            Choice(13,"AC output frequency","AC output / input","50 Hz","60 Hz"),
            Choice(14,"Automatic restart after fault","Options","On","Off"),
            Choice(15,"Display backlight","Options","On","Off","Timed"),
            Choice(16,"Audible alerts","Options","Enabled","Disabled"),
            Choice(17,"Battery-capacity limiting","Options","Disabled","Enabled"),
            Choice(18,"Load limiting","Options","Disabled","Enabled"),
            Choice(19,"Disconnected-load alert","Options","Disabled","Enabled"),
            new Setting(48,"AC output voltage","Calibration",100,150,"%"){Scale=10},
            new Setting(49,"AC input voltage","Calibration",100,150,"%"){Scale=10},
            new Setting(50,"Battery voltage","Calibration",100,150,"%"){Scale=10},
            new Setting(51,"Charging current","Calibration",110,140,"%"),
            new Setting(52,"Load measurement","Calibration",115,135,"%"),
            new Setting(53,"Protection / load capacity","Calibration",120,130,"%")
        };
        public static Setting ByAddress(int address) { return All.Single(s=>s.Address==address); }
        public static int DefaultRaw(int address)
        {
            int[] defaults={1,1,1,1,100,100,142,136,102,120,135,220,2,1,1,1,1,2,2,1};
            if(address>=48&&address<=53)return 125;
            if(address>=0&&address<defaults.Length)return defaults[address];
            throw new ArgumentException("Unknown setting address.");
        }
    }
    public sealed class Status
    {
        public readonly byte[] Frame, Data;
        public readonly DateTime ReceivedUtc;
        public Status(byte[] frame)
        {
            if(!Protocol.Valid(frame))throw new ArgumentException("Invalid status frame.");
            Frame=(byte[])frame.Clone(); Data=frame.Skip(5).Take(frame.Length-6).ToArray(); ReceivedUtc=DateTime.UtcNow;
        }
        private int U16(int p) { return Data[p]+256*Data[p+1]; }
        public int Units { get { return Data[31]; } }
        public string Model { get { return Data[11].ToString()+(Units*12).ToString(); } }
        public string Firmware { get { return Data[9]+"."+Data[10].ToString("00"); } }
        public string ChargerFirmware { get { return Data[32]+"."+Data[33].ToString("00"); } }
        public double Battery { get { return U16(20)/10.0; } }
        public double Output { get { return U16(16)/10.0; } }
        public double OutputHz { get { return U16(18)/10.0; } }
        public double AcInput { get { return U16(12)/10.0; } }
        public double InputHz { get { return U16(14)/10.0; } }
        public double SolarCurrent { get { return Data[36]+(Data.Length>66?Data[66]/10.0:0); } }
        public double PvVoltage { get { return U16(34); } }
        public double Runtime { get { return U16(28)+Data[27]/10.0; } }
        public int Load { get { return Data[24]; } }
        public string Mode { get { return Data[30]==5?"Utility / AC":Data[30]==6?"Battery / DC":"Device state "+Data[30]; } }
        public bool Alarm { get { return Data.Skip(3).Take(6).Any(b=>b!=0)||(Data[2]&15)!=0; } }
        public bool WriteProfile { get { return Frame.Length==86&&Data[11]==80&&Units==4&&Firmware=="2.10"; } }
        public string MachineStatusText
        {
            get
            {
                return Mode+" mode\r\nController: "+((Data[2]&2)!=0?"Fault reported":(Data[2]&1)!=0?"Warning reported":"OK")+
                    "\r\nCharger: "+((Data[0]&8)==0?"Not connected":(Data[2]&8)!=0?"Fault reported":(Data[2]&4)!=0?"Warning reported":"OK")+
                    "\r\nBattery: "+((Data[0]&1)!=0?"Full":"Not full")+
                    "\r\nAC charging: "+((Data[0]&2)!=0?"Active":"Inactive")+
                    "\r\nSolar charging: "+((Data[0]&4)!=0?"Active":"Inactive")+
                    "\r\nWarnings: "+(Data.Skip(3).Take(3).Any(b=>b!=0)?Protocol.Hex(Data.Skip(3).Take(3).ToArray()):"None reported")+
                    "\r\nFaults: "+(Data.Skip(6).Take(3).Any(b=>b!=0)?Protocol.Hex(Data.Skip(6).Take(3).ToArray()):"None reported");
            }
        }
        public int Value(Setting s) { return Data[s.Offset]; }
        public bool SameIdentity(Status other) { return other!=null&&Model==other.Model&&Firmware==other.Firmware&&ChargerFirmware==other.ChargerFirmware&&Data.Length==other.Data.Length; }
        public bool SameConfiguration(Status other,int exceptOffset)
        { if(!SameIdentity(other))return false; for(int i=39;i<Data.Length;i++)if(i!=66&&i!=exceptOffset&&Data[i]!=other.Data[i])return false; return true; } // Byte 66 is live solar-current telemetry, not configuration.
        public object Snapshot()
        {
            return new { schema="inverter-desk.status.v1", receivedUtc=ReceivedUtc.ToString("o"), machine=Model, firmware=Firmware, chargerFirmware=ChargerFirmware,
                nominalBatteryVoltage=Units*12, batteryVoltage=Battery, outputVoltageReported=Output, outputFrequency=OutputHz, acInputVoltage=AcInput,
                acInputFrequency=InputHz, batteryCapacityReportedPercent=(int)Data[22], acChargeCurrent=(int)Data[23], solarCurrent=SolarCurrent, pvVoltage=PvVoltage,
                loadReportedPercent=Load, operatingMode=Mode, runtimeHours=Runtime,inverterTemperatureC=Data[25]==255?(int?)null:Data[25],chargerTemperatureC=Data[37]==0||Data[37]==255?(int?)null:Data[37], warningFlags=Protocol.Hex(Data.Skip(3).Take(3).ToArray()),
                faultFlags=Protocol.Hex(Data.Skip(6).Take(3).ToArray()), settings=Catalog.All.Select(s=>new {address=s.Address,name=s.Name,raw=Value(s),display=s.Text(Value(s),Units)}).ToArray(), rawHex=Protocol.Hex(Frame)};
        }
    }
    public static class Protocol
    {
        public static readonly byte[] Heartbeat=Encoding.ASCII.GetBytes("CMSG\u0006INV-HB");
        public static string Hex(byte[] b) { return BitConverter.ToString(b).Replace('-',' '); }
        public static byte[] FromHex(string s) { return s.Split(new[]{' ','\r','\n','\t'},StringSplitOptions.RemoveEmptyEntries).Select(x=>Convert.ToByte(x,16)).ToArray(); }
        public static bool Valid(byte[] f)
        {
            if(f==null||f.Length<72||f[0]!=86||f[1]!=80||f[2]!=72||f[3]!=66||f[4]+5!=f.Length)return false;
            int sum=0;for(int i=5;i<f.Length-1;i++)sum+=f[i];return (byte)sum==f[f.Length-1];
        }
        public static byte[] Write(Setting s,int value)
        {
            if(!Catalog.All.Contains(s)||value<s.Min||value>s.Max)throw new ArgumentException("Unsupported setting or value.");
            var bytes=new List<byte>(Encoding.ASCII.GetBytes(s.Calibration?"CSCA\u000aCSICAL":"CSPA\u000aCSIPAM"));
            bytes.Add((byte)s.Address);bytes.Add(0);bytes.Add((byte)value);bytes.Add((byte)(s.Address+value));return bytes.ToArray();
        }
        public static byte[] Maintenance(string kind)
        { if(kind=="parameters")return Encoding.ASCII.GetBytes("CRPA\u0006CREPAM");if(kind=="runtime")return Encoding.ASCII.GetBytes("CRST\u0006CRSYST");if(kind=="calibration")return Encoding.ASCII.GetBytes("CRCA\u0006CRECAL");throw new ArgumentException("Unknown maintenance operation."); }
        public const string SampleHex="56 50 48 42 51 08 00 00 00 00 00 00 00 00 02 0A 50 00 00 00 00 AB 04 58 02 09 02 64 00 00 1A 14 08 F2 00 06 04 01 01 00 00 00 1A 14 7D 7D 7D 7D 7D 7D 01 01 03 02 32 64 8D 85 68 7A 82 F0 02 02 02 03 01 02 02 01 00 00 01 73 82 04 03 01 0A 14 5F 32 6C 00 00 57";
    }
    public sealed class Decoder
    {
        private readonly List<byte> buffer=new List<byte>();
        public int BadFrames, NoiseBytes;
        public List<Status> Feed(byte[] chunk)
        {
            buffer.AddRange(chunk);var result=new List<Status>();
            while(buffer.Count>=5)
            {
                if(buffer[0]!=86||buffer[1]!=80||buffer[2]!=72||buffer[3]!=66){buffer.RemoveAt(0);NoiseBytes++;continue;}
                int length=buffer[4]+5;
                if(length<72||length>133){buffer.RemoveAt(0);BadFrames++;continue;}
                if(buffer.Count<length)break;
                byte[] frame=buffer.Take(length).ToArray();
                if(!Protocol.Valid(frame)){buffer.RemoveAt(0);BadFrames++;continue;}
                buffer.RemoveRange(0,length);result.Add(new Status(frame));
            }
            return result;
        }
    }
    public sealed class Journal : IDisposable
    {
        private StreamWriter writer;
        private readonly object sync=new object();
        public readonly string FilePath;
        public Action<string> Line;
        public Journal(string path) { FilePath=path;Directory.CreateDirectory(Path.GetDirectoryName(path));writer=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false));writer.AutoFlush=true; }
        public void Record(string kind,string message,byte[] bytes)
        {
            string time=DateTime.UtcNow.ToString("o");string hex=bytes==null?null:Protocol.Hex(bytes);
            lock(sync){if(writer==null)return;writer.WriteLine(new JavaScriptSerializer().Serialize(new {utc=time,kind=kind,message=message,hex=hex}));if(kind=="TX intent")((FileStream)writer.BaseStream).Flush(true);}
            if(Line!=null)Line(DateTime.Now.ToString("HH:mm:ss.fff")+"  "+kind.PadRight(10)+" "+message+(hex==null?"":"  "+hex));
        }
        public void Dispose() { lock(sync){if(writer!=null){writer.Dispose();writer=null;}} }
    }
    public interface ITransport { Status Query(); void Send(byte[] bytes,string purpose); void Delay(int milliseconds); }
    public sealed class SerialTransport : ITransport, IDisposable
    {
        private readonly SerialPort port;
        private readonly Decoder decoder=new Decoder();
        private readonly Queue<Status> replies=new Queue<Status>();
        private readonly Journal journal;
        public Action<Status> StatusReceived;
        public SerialTransport(string name,int baud,Journal log)
        {
            journal=log;port=new SerialPort(name,baud,Parity.None,8,StopBits.One){Handshake=Handshake.None,DtrEnable=false,RtsEnable=false,ReadTimeout=50,WriteTimeout=1500};
            try{port.Open();journal.Record("Connection","Opened "+name+" at "+baud+" baud, 8N1",null);}catch{port.Dispose();throw;}
        }
        public void Send(byte[] bytes,string purpose)
        { journal.Record("TX intent",purpose,bytes);port.Write(bytes,0,bytes.Length);journal.Record("TX sent",purpose,bytes); }
        private void ReadAvailable()
        {
            int count=port.BytesToRead;if(count==0){Thread.Sleep(10);return;}
            byte[] b=new byte[Math.Min(count,4096)];int n=port.Read(b,0,b.Length);if(n!=b.Length)Array.Resize(ref b,n);
            journal.Record("RX","Serial bytes",b);int bad=decoder.BadFrames;
            foreach(Status s in decoder.Feed(b)){replies.Enqueue(s);while(replies.Count>100)replies.Dequeue();if(StatusReceived!=null)StatusReceived(s);}
            if(decoder.BadFrames!=bad)journal.Record("Parser","Rejected invalid length/checksum; raw bytes retained",null);
        }
        public void Delay(int ms) { var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms)ReadAvailable(); }
        public Status Query()
        {
            Delay(40);replies.Clear();Send(Protocol.Heartbeat,"Read status");var watch=Stopwatch.StartNew();
            while(watch.ElapsedMilliseconds<3000){ReadAvailable();if(replies.Count>0)return replies.Dequeue();}
            throw new TimeoutException("No valid inverter status reply within 3 seconds.");
        }
        public void Dispose() { try{if(port.IsOpen)port.Close();}finally{port.Dispose();journal.Record("Connection","Serial port closed",null);} }
    }
    public sealed class WriteResult
    {
        public bool Sent, ReadbackVerified, RestoreAttempted, RestoreVerified, NoChange;
        public int Requested, Original;
        public string Error, RestoreError, Message;
        public Status Final;
    }
    public static class Transactions
    {
        public static void CheckProfile(Status s,bool allowOtherProfile=false)
        { if(!s.WriteProfile&&!allowOtherProfile)throw new InvalidOperationException("This device is outside the tested 8048 / 48 V / firmware 2.10 profile. Enable the explicit experimental-device option to try vendor-format commands.");if(s.Units<1||s.Units>4)throw new InvalidOperationException("Battery-bank size is not recognized.");if(s.Alarm)throw new InvalidOperationException("The inverter reports a warning or fault. Resolve it before changing settings."); }
        private static bool Verify(ITransport t,Status baseline,Setting setting,int value,out Status last)
        {
            bool matches=true;last=null;
            for(int i=0;i<3;i++){last=t.Query();if(!baseline.SameConfiguration(last,setting.Offset)||last.Alarm)throw new InvalidOperationException("Other configuration fields or warning/fault flags changed during verification.");matches&=last.Value(setting)==value;if(i<2)t.Delay(300);}
            return matches;
        }
        public static WriteResult Change(ITransport t,Setting setting,int value,Status expected,bool temporary,Action<string> progress,bool allowOtherProfile=false)
        {
            var r=new WriteResult{Requested=value};Status baseline=null;
            try
            {
                Protocol.Write(setting,value);baseline=t.Query();CheckProfile(baseline,allowOtherProfile);r.Original=baseline.Value(setting);
                Protocol.Write(setting,r.Original); // Refuse if the captured original cannot be encoded for restoration.
                if(!baseline.SameConfiguration(expected,-1))throw new InvalidOperationException("Settings changed since you reviewed them. Refresh the values and try again.");
                if(value==r.Original){r.NoChange=true;r.Final=baseline;r.Message="Already at the requested value. No write sent.";return r;}
                if(temporary&&Math.Abs(value-r.Original)>(setting.Calibration?(setting.Scale==10?5:1):1))throw new InvalidOperationException("Temporary tests are limited to 0.5% for voltage calibration, 1% for other calibration, or one raw step for a normal setting.");
                if(progress!=null)progress("Sending "+setting.Name+"; awaiting three fresh readbacks...");
                if(setting.Calibration)t.Delay(5000);
                // Mark before calling Send: even a write exception can occur after bytes reach hardware.
                r.Sent=true;t.Send(Protocol.Write(setting,value),"Set "+setting.Name+" to raw "+value);
                t.Delay(setting.Calibration?5000:350);Status last;
                r.ReadbackVerified=Verify(t,baseline,setting,value,out last);r.Final=last;
            }
            catch(Exception e){r.Error=e.Message;}
            finally
            {
                if(r.Sent&&(temporary||!r.ReadbackVerified||r.Error!=null))
                {
                    r.RestoreAttempted=true;if(progress!=null)progress("Sending original value and verifying restoration...");
                    for(int attempt=0;attempt<2&&!r.RestoreVerified;attempt++)try
                    {
                        t.Send(Protocol.Write(setting,r.Original),"Restore "+setting.Name+" to original raw "+r.Original);
                        t.Delay(setting.Calibration?5000:350);Status last;
                        r.RestoreVerified=Verify(t,baseline,setting,r.Original,out last)&&baseline.SameConfiguration(last,-1);r.Final=last;
                        r.RestoreError=r.RestoreVerified?null:"Original configuration was not confirmed.";
                    }catch(Exception e){r.RestoreError=e.Message;}
                }
            }
            if(!r.NoChange)
            {
                r.Message=r.Error!=null?"Not confirmed: "+r.Error:r.ReadbackVerified?"Change successful.":"No change confirmed. The requested value was not reported consistently.";
                if(r.RestoreAttempted)r.Message+=r.RestoreVerified?" Original configuration is reported again.":" RESTORATION NOT CONFIRMED: "+r.RestoreError;
                if(temporary&&!r.ReadbackVerified&&r.RestoreVerified)r.Message+=" This does not prove either calibration write was accepted.";
            }
            return r;
        }
    }
}
