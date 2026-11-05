using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InverterDesk;

sealed class FakeTransport : ITransport
{
    public byte[] Frame=Protocol.FromHex(Protocol.SampleHex);
    public int Queries;
    public bool IgnoreWrites, PartialFailure, RefuseRestore;
    public readonly List<byte[]> Writes=new List<byte[]>();
    public readonly Dictionary<int,Action<FakeTransport>> BeforeRead=new Dictionary<int,Action<FakeTransport>>();
    public void Set(int offset,int value){Frame[offset+5]=(byte)value;Recheck();}
    public void Recheck(){int sum=0;for(int i=5;i<Frame.Length-1;i++)sum+=Frame[i];Frame[Frame.Length-1]=(byte)sum;}
    public Status Current(){return new Status(Frame);}
    public Status Query(){Queries++;if(BeforeRead.ContainsKey(Queries))BeforeRead[Queries](this);return Current();}
    public void Delay(int milliseconds){}
    public void Send(byte[] bytes,string purpose)
    {
        Writes.Add((byte[])bytes.Clone());
        if(RefuseRestore&&Writes.Count>1)throw new IOException("Simulated disconnected cable during restore");
        if(!IgnoreWrites)Set(Catalog.ByAddress(bytes[11]).Offset,bytes[13]);
        if(PartialFailure&&Writes.Count==1)throw new IOException("Simulated exception after bytes reached the device");
    }
}
static class Tests
{
    static int passed,failed;
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Equal(string a,string b){Assert(a==b,"Expected "+b+"; got "+a);}
    static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid value accepted");}
    static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static WriteResult Change(FakeTransport t,int address,int value,bool temporary,Status baseline=null,bool other=false){return Transactions.Change(t,Catalog.ByAddress(address),value,baseline??t.Current(),temporary,null,other);}
    static int Main(string[] args)
    {
        Test("Captured sample and checksum",()=>{var s=new FakeTransport().Current();Assert(s.Model=="8048"&&s.Firmware=="2.10"&&s.ChargerFirmware=="1.01","identity");Assert(s.Battery==52.1&&s.Output==119.5&&s.OutputHz==60,"measurements");Assert(s.Value(Catalog.ByAddress(6))==141&&s.Value(Catalog.ByAddress(50))==125,"settings");Assert(!s.Alarm,"sample flags");});
        Test("Golden vendor heartbeat and write packets",()=>{Equal(Protocol.Hex(Protocol.Heartbeat),"43 4D 53 47 06 49 4E 56 2D 48 42");Equal(Protocol.Hex(Protocol.Write(Catalog.ByAddress(50),126)),"43 53 43 41 0A 43 53 49 43 41 4C 32 00 7E B0");Equal(Protocol.Hex(Protocol.Write(Catalog.ByAddress(6),146)),"43 53 50 41 0A 43 53 49 50 41 4D 06 00 92 98");});
        Test("All six calibration address/checksum mappings",()=>{byte[] expectedChecks={0xAD,0xAE,0xAF,0xB0,0xB1,0xB2};for(int i=0;i<6;i++){var s=Catalog.ByAddress(48+i);var p=Protocol.Write(s,125);Assert(p.Length==15&&p[11]==48+i&&p[12]==0&&p[13]==125&&p[14]==expectedChecks[i],"cal packet "+i);Assert(s.Offset==39+i,"readback offset");}});
        Test("Reset packet encoding",()=>{Equal(Protocol.Hex(Protocol.Maintenance("parameters")),"43 52 50 41 06 43 52 45 50 41 4D");Equal(Protocol.Hex(Protocol.Maintenance("runtime")),"43 52 53 54 06 43 52 53 59 53 54");Equal(Protocol.Hex(Protocol.Maintenance("calibration")),"43 52 43 41 06 43 52 45 43 41 4C");});
        Test("48 V bank conversion and input limits",()=>{var s=Catalog.ByAddress(6);Assert(s.Number(141,4)==56.4m&&s.Parse("58.4",4)==146,"full bank");Reject(()=>s.Parse("14.6",4));Reject(()=>s.Parse("56.5",4));Reject(()=>s.Parse("60.4",4));Assert(Catalog.ByAddress(50).Parse("0.5",4)==130,"cal scale");Reject(()=>Catalog.ByAddress(50).Parse("0.55",4));Reject(()=>Protocol.Write(s,0));});
        Test("Enum meanings preserve vendor order",()=>{Assert(Catalog.ByAddress(14).Parse("On",4)==1&&Catalog.ByAddress(16).Parse("Enabled",4)==1&&Catalog.ByAddress(17).Parse("Disabled",4)==1,"enum polarity");Assert(Catalog.ByAddress(13).Parse("60 Hz",4)==2,"frequency");});
        Test("Every byte boundary and coalesced status frames",()=>{byte[] b=Protocol.FromHex(Protocol.SampleHex);for(int split=1;split<b.Length;split++){var d=new Decoder();Assert(d.Feed(b.Take(split).ToArray()).Count==0,"fragment prematurely decoded");Assert(d.Feed(b.Skip(split).Concat(b).ToArray()).Count==2,"boundary "+split);}var each=new Decoder();int count=0;foreach(byte x in b)count+=each.Feed(new[]{x}).Count;Assert(count==1,"one-byte delivery");});
        Test("Noise, invalid lengths and corrupt checksum resynchronize",()=>{byte[] b=Protocol.FromHex(Protocol.SampleHex),bad=(byte[])b.Clone();bad[40]^=1;byte[] noise={0x06,0x15,0x56,0x50,0x48,0x42,0xFF};var d=new Decoder();Assert(d.Feed(noise.Concat(bad).Concat(b).ToArray()).Count==1,"resync");Assert(d.BadFrames>=2&&d.NoiseBytes>0,"rejection accounting");});
        Test("Changing live solar-current fraction is not configuration drift",()=>{var t=new FakeTransport();Status before=t.Current();t.Set(66,5);Assert(before.SameConfiguration(t.Current(),-1),"telemetry excluded");t.Set(67,2);Assert(!before.SameConfiguration(t.Current(),-1),"unknown extension guarded");});
        Test("Normal apply requires three matching fresh readbacks",()=>{var t=new FakeTransport();var r=Change(t,6,142,false);Assert(r.ReadbackVerified&&!r.RestoreAttempted&&t.Queries==4&&t.Writes.Count==1&&t.Current().Value(Catalog.ByAddress(6))==142,"apply");});
        Test("Temporary accepted calibration change restores original",()=>{var t=new FakeTransport();var r=Change(t,50,126,true);Assert(r.ReadbackVerified&&r.RestoreAttempted&&r.RestoreVerified&&t.Writes.Count==2&&t.Current().Value(Catalog.ByAddress(50))==125,"temporary restore");});
        Test("Ignored calibration write never reports acceptance",()=>{var t=new FakeTransport{IgnoreWrites=true};var r=Change(t,50,130,false);Assert(r.Sent&&!r.ReadbackVerified&&r.RestoreAttempted&&r.RestoreVerified&&t.Writes.Count==2,"ignored command");Assert(r.Message.Contains("No change confirmed"),"honest message");});
        Test("Lost post-write reply still restores",()=>{var t=new FakeTransport();t.BeforeRead[2]=x=>{throw new TimeoutException("Simulated missing reply");};var r=Change(t,50,126,true);Assert(r.Sent&&r.Error!=null&&r.RestoreVerified&&t.Current().Value(Catalog.ByAddress(50))==125,"lost reply restore");});
        Test("Partial write exception still restores",()=>{var t=new FakeTransport{PartialFailure=true};var r=Change(t,50,126,false);Assert(r.Sent&&!r.ReadbackVerified&&r.Error!=null&&r.RestoreVerified&&t.Writes.Count==2,"partial failure restore");});
        Test("Restore failure is explicit, never successful",()=>{var t=new FakeTransport{RefuseRestore=true};var r=Change(t,50,126,true);Assert(r.ReadbackVerified&&r.RestoreAttempted&&!r.RestoreVerified&&r.Message.Contains("RESTORATION NOT CONFIRMED")&&t.Writes.Count==3,"failed restore reporting");});
        Test("Changed reviewed configuration aborts before sending",()=>{var t=new FakeTransport();Status expected=t.Current();t.Set(51,142);var r=Change(t,50,126,true,expected);Assert(!r.Sent&&r.Error!=null&&t.Writes.Count==0,"stale review");});
        Test("Unknown firmware requires explicit experimental opt-in",()=>{var t=new FakeTransport();t.Set(10,11);var r=Change(t,50,126,true);Assert(!r.Sent&&r.Error!=null,"wrong profile blocked");r=Change(t,50,126,true,null,true);Assert(r.ReadbackVerified&&r.RestoreVerified,"explicit opt-in");});
        Test("Warnings and unrestorable original values block writes",()=>{var t=new FakeTransport();t.Set(3,1);Assert(!Change(t,50,126,true).Sent,"alarm");t.Set(3,0);t.Set(41,255);Assert(!Change(t,50,126,true).Sent&&t.Writes.Count==0,"invalid original");});
        Test("No-op and excessive temporary changes send nothing",()=>{var t=new FakeTransport();Assert(Change(t,50,125,true).NoChange,"no-op");Assert(!Change(t,50,131,true).Sent,"excessive calibration step");Assert(!Change(t,6,146,true).Sent&&t.Writes.Count==0,"excessive normal step");});
        Test("Unrelated settings mutation is not successful verification",()=>{var t=new FakeTransport();t.BeforeRead[2]=x=>x.Set(52,134);var r=Change(t,50,126,true);Assert(!r.ReadbackVerified&&r.Error!=null&&r.RestoreAttempted&&!r.RestoreVerified,"unrelated mutation");});
        Test("Inconsistent readbacks are not verified",()=>{var t=new FakeTransport();t.BeforeRead[3]=x=>x.Set(41,125);var r=Change(t,50,126,false);Assert(!r.ReadbackVerified&&r.RestoreVerified,"inconsistent replies");});
        Test("Settings files and legacy exports round trip all values",()=>{Status current=new FakeTransport().Current();Status loaded=SettingsFile.Read(SettingsFile.Save(current));Assert(current.SameConfiguration(loaded,-1),"settings round trip");string old=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {application="Inverter View 1.0.0",device=current.Snapshot()});Assert(SettingsFile.Read(old).SameConfiguration(current,-1),"legacy file");Reject(()=>SettingsFile.Read("{}"));Reject(()=>SettingsFile.Read(SettingsFile.Save(current).Replace("\"nominalBatteryVoltage\":48","\"nominalBatteryVoltage\":24")));Reject(()=>SettingsFile.Read(SettingsFile.Save(current).Replace("\"raw\":141","\"raw\":142")));});
        Test("Settings restore writes only selected changes",()=>{var t=new FakeTransport();var source=new FakeTransport();source.Set(51,142);source.Set(60,1);var r=SettingsRestore.Apply(t,source.Current(),new[]{6,15},t.Current(),null,null);Assert(r.Success&&r.Steps.Count==2&&t.Writes.Count==2&&t.Current().Value(Catalog.ByAddress(6))==142&&t.Current().Value(Catalog.ByAddress(15))==1,"selected restore");});
        Test("Settings restore stops after a failed step and reports partial result",()=>{var t=new FakeTransport();var source=new FakeTransport();source.Set(51,142);source.Set(60,1);source.Set(61,2);t.BeforeRead[5]=x=>x.IgnoreWrites=true;var r=SettingsRestore.Apply(t,source.Current(),new[]{6,15,16},t.Current(),null,null);Assert(!r.Success&&r.Steps.Count==2&&r.Remaining==1&&t.Current().Value(Catalog.ByAddress(6))==142&&t.Current().Value(Catalog.ByAddress(15))==3&&r.Steps[1].Value.RestoreVerified,"stop and revert failed step");});
        Test("Settings restore preflights identity and all selected ranges",()=>{var t=new FakeTransport();var source=new FakeTransport();source.Set(51,142);source.Set(60,255);var r=SettingsRestore.Apply(t,source.Current(),new[]{6,15},t.Current(),null,null);Assert(!r.Success&&t.Writes.Count==0,"preflight all ranges");source=new FakeTransport();source.Set(10,11);r=SettingsRestore.Apply(t,source.Current(),new[]{6},t.Current(),null,null,true);Assert(!r.Success&&t.Writes.Count==0,"firmware mismatch");});
        Test("Settings restore rejects drift and skips matching values",()=>{var t=new FakeTransport();var source=new FakeTransport();source.Set(51,142);Status reviewed=t.Current();t.Set(60,1);var r=SettingsRestore.Apply(t,source.Current(),new[]{6},reviewed,null,null);Assert(!r.Success&&t.Writes.Count==0,"changed since review");r=SettingsRestore.Apply(t,t.Current(),new[]{6},t.Current(),null,null);Assert(r.Success&&r.Steps[0].Value.NoChange&&t.Writes.Count==0,"matching settings skipped");});
        Test("Settings restore disconnect request stops before next setting",()=>{var t=new FakeTransport();var source=new FakeTransport();source.Set(51,142);source.Set(60,1);bool cancel=false;var r=SettingsRestore.Apply(t,source.Current(),new[]{6,15},t.Current(),null,(s,step)=>cancel=true,false,()=>cancel);Assert(!r.Success&&r.Steps.Count==1&&r.Remaining==1&&t.Writes.Count==1,"stop between writes");});
        if(args.Length>0&&File.Exists(args[0]))Test("Original captured status stream",()=>{var d=new Decoder();byte[] bytes=Protocol.FromHex(File.ReadAllText(args[0]));int count=0;for(int i=0;i<bytes.Length;i+=7)count+=d.Feed(bytes.Skip(i).Take(7).ToArray()).Count;Assert(count>=20&&count*86==bytes.Length&&d.BadFrames==0,"capture frames "+count);});
        Console.WriteLine(passed+" passed; "+failed+" failed. No serial port was opened.");return failed==0?0:1;
    }
}
