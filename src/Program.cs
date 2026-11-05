using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Web.Script.Serialization;

[assembly: AssemblyTitle("Inverter View")]
[assembly: AssemblyDescription("Portable V-Series inverter monitor and settings utility")]
namespace InverterDesk
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // A status-only smoke check: never sends configuration or calibration commands.
            if(args.Length==3&&args[0]=="--live-check")
            {
                string report=Path.GetFullPath(args[2]);
                try
                {
                    using(var log=new Journal(report+".jsonl"))
                    using(var serial=new SerialTransport(args[1],2400,log))
                    {
                        Status last=null;for(int i=0;i<5;i++){last=serial.Query();serial.Delay(500);}
                        File.WriteAllText(report,new JavaScriptSerializer().Serialize(new {application="Inverter View "+AppInfo.Version,readOnly=true,status=last.Snapshot()}));
                    }
                    return 0;
                }
                catch(Exception e){File.WriteAllText(report+".error.txt",e.Message);return 1;}
            }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());return 0;
        }
    }
}
