using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using InverterDesk;

static class UiTests
{
    static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static void Call(object o,string name,params object[] args){o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    static Status ChangedStatus(Status s,int offset,int value){byte[] f=(byte[])s.Frame.Clone();f[5+offset]=(byte)value;int sum=0;for(int i=5;i<f.Length-1;i++)sum+=f[i];f[f.Length-1]=(byte)sum;return new Status(f);}
    static void CheckChargingCards(MainForm form, Status baseline)
    {
        var amps = (Dictionary<string,Label>)Field(form, "metrics");
        var watts = (Dictionary<string,Label>)Field(form, "chargingWatts");
        Assert(watts["AC → Battery"].Text == "— W" && watts["Solar → Battery"].Text == "— W", "Power requires a reading");

        // Deliberately distinct inputs: 52.3 V battery, 120.6 V AC and 63 V PV.
        var sample = ChangedStatus(baseline, 20, 0x0B);
        sample = ChangedStatus(sample, 12, 0xB6);
        sample = ChangedStatus(sample, 13, 0x04);
        sample = ChangedStatus(sample, 34, 63);
        sample = ChangedStatus(sample, 23, 17);
        sample = ChangedStatus(sample, 66, 7);
        Call(form, "OnStatus", sample, true);
        Assert(amps["AC → Battery"].Text == "17 A DC" && watts["AC → Battery"].Text == "≈ 889 W", "AC battery-side power");
        Assert(amps["Solar → Battery"].Text == 0.7.ToString("0.#") + " A DC" && watts["Solar → Battery"].Text == "≈ 37 W", "Fractional solar battery-side power");

        Call(form, "OnStatus", ChangedStatus(sample, 20, 0x1C), true); // 54.0 V, same currents.
        Assert(watts["AC → Battery"].Text == "≈ 918 W" && watts["Solar → Battery"].Text == "≈ 38 W", "Both powers follow the new battery voltage");
        Call(form, "OnStatus", baseline, true);
        Assert(watts["AC → Battery"].Text == "≈ 0 W" && watts["Solar → Battery"].Text == "≈ 0 W", "Zero currents clear both previous power readings");
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            using(var form=new MainForm(false))
            {
                CheckChargingCards(form, new Status(Protocol.FromHex(Protocol.SampleHex)));
                var grid=(DataGridView)Field(form,"settings");var cal=(DataGridView)Field(form,"calibration");var right=(DataGridView)Field(form,"settingsRight");var bulk=right.Rows[0];
                Assert(grid.Rows.Count==10&&right.Rows.Count==10&&cal.Rows.Count==6,"All original controls must be visible");
                Assert((string)bulk.Cells[1].Value=="56.4 V"&&(string)bulk.Cells[3].Value=="Unchanged","Current value and baseline");
                Assert(bulk.Cells[1].ReadOnly&&!bulk.Cells[2].ReadOnly&&bulk.Cells[3].ReadOnly,"Only proposed value is editable");
                bulk.Cells[2].Value="56.8";
                Assert((string)bulk.Cells[1].Value=="56.4 V"&&(string)bulk.Cells[3].Value=="Pending edit","A draft must not change the reported value");
                Status baseline=(Status)Field(form,"latest");Call(form,"OnStatus",ChangedStatus(baseline,51,142),true);
                Assert((string)bulk.Cells[1].Value=="56.8 V"&&(string)bulk.Cells[3].Value=="56.4 V → 56.8 V","Device-side change must be explicit");
                Assert((string)bulk.Cells[2].Value=="56.8","Polling must preserve drafts");
                ((System.Collections.Generic.Dictionary<int,string>)Field(form,"outcomes"))[6]="Change successful";
                ((System.Collections.Generic.Dictionary<int,int>)Field(form,"verifiedValues"))[6]=142;
                Call(form,"OnStatus",ChangedStatus(baseline,51,142),true);Assert((string)bulk.Cells[3].Value=="Change successful","Confirmed result label");
                bulk.Cells[2].Value="57.2";Assert(Convert.ToString(bulk.Cells[3].Value).Contains("Pending edit"),"Changed value and another pending draft");
                Call(form,"FillDrafts",right);Assert((string)bulk.Cells[2].Value=="56.8"&&!Convert.ToString(bulk.Cells[3].Value).Contains("Pending edit"),"Discard draft uses reported value");
                Assert((string)cal.Rows[2].Cells[5].Value=="No effect observed"&&(string)cal.Rows[0].Cells[5].Value=="Not yet tested","Calibration evidence labels");
                foreach(var button in (System.Collections.Generic.List<Button>)Field(form,"writeButtons"))Assert(!button.Enabled,"Disconnected state must disable every device action");
                Assert(form.Text=="Inverter View","Application name");
                ((CheckBox)Field(form,"temperatureUnits")).Checked=true;
                Assert(((System.Collections.Generic.Dictionary<string,Label>)Field(form,"details"))["Inverter temperature"].Text=="78.8 °F","Fahrenheit conversion");
                ((CheckBox)Field(form,"temperatureUnits")).Checked=false;
                Assert(((System.Collections.Generic.Dictionary<string,Label>)Field(form,"details"))["Inverter temperature"].Text=="26 °C","Celsius conversion");
                Assert(((RichTextBox)Field(form,"machineStatus")).Text.Contains("AC charging: Inactive"),"Machine status flags");
                var statusText=(RichTextBox)Field(form,"machineStatus");statusText.Select(statusText.Text.IndexOf("OK"),2);Assert(statusText.SelectionColor==Theme.Good,"Normal machine status color");
                ((CheckBox)Field(form,"darkMode")).Checked=true;Assert(Theme.Dark&&form.BackColor.GetBrightness()<0.3f,"Dark theme");
                Assert(grid.DefaultCellStyle.ForeColor.GetBrightness()>0.6f&&grid.DefaultCellStyle.BackColor.GetBrightness()<0.3f,"Dark grid contrast");
                ((CheckBox)Field(form,"darkMode")).Checked=false;Assert(!Theme.Dark&&form.BackColor.GetBrightness()>0.8f,"Light theme restored");
                var footer=(Label)Field(form,"footer");var connection=(CheckBox)Field(form,"connection");var connect=(Button)Field(form,"connect");
                form.GetType().GetField("connected",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,true);
                Call(form,"UpdateConnectionLabel");Assert(connection.Checked,"Fresh connected state before disconnect");
                footer.Text="Disconnecting...";Call(form,"CompleteDisconnection");
                Assert(footer.Text=="Disconnected."&&!connection.Checked&&connection.Text=="Disconnected"&&connect.Text=="Connect"&&connect.Enabled,"Completed disconnect must replace its progress message and reset the controls");
                foreach(var button in (System.Collections.Generic.List<Button>)Field(form,"writeButtons"))Assert(!button.Enabled,"Completed disconnect must disable every device action");
                foreach(string result in new[]{"Disconnected: Port error","Restoration failed: no reply","Change successful"})
                {footer.Text=result;Call(form,"CompleteDisconnection");Assert(footer.Text==result,"Disconnect must preserve errors and completed action results");}
                Call(form,"ResetDrafts",false);Assert((string)bulk.Cells[1].Value=="56.8 V"&&(string)bulk.Cells[2].Value=="56.8","Reset drafts does not alter current values");
                var topics=(ComboBox)Field(form,"helpTopics");Assert(topics.Items.Count==27,"Guide covers every setting");
                for(int i=1;i<topics.Items.Count;i++){topics.SelectedIndex=i;Assert(((RichTextBox)Field(form,"helpBody")).Text.Length>100,"Help body "+i);}
                Assert(grid.Rows[0].Tag==Catalog.ByAddress(0)&&grid.Rows[9].Tag==Catalog.ByAddress(19)&&right.Rows[9].Tag==Catalog.ByAddress(16),"Original grouping order");
                using(var review=new RestoreDialog(ChangedStatus(ChangedStatus(baseline,51,142),41,126),baseline))
                {
                    var reviewGrid=(DataGridView)Find(review,typeof(DataGridView));
                    Assert(reviewGrid.Rows.Count==26&&Convert.ToBoolean(reviewGrid.Rows[6].Cells[0].Value),"Saved normal setting selected");
                    Assert(!Convert.ToBoolean(reviewGrid.Rows[22].Cells[0].Value)&&!reviewGrid.Rows[22].Cells[0].ReadOnly,"Calibration requires explicit restore selection");
                    Assert(reviewGrid.Rows[0].Cells[0].ReadOnly,"Matching restore value skipped");
                }
                if(args.Length==1)
                {
                    System.IO.Directory.CreateDirectory(args[0]);
                    foreach(string page in new[]{"Dashboard","Device settings","Calibration","Help","Dashboard dark","Device settings dark"})
                    {
                        using(var render=new MainForm(false))
                        {
                            Call(render,"OnStatus",baseline,true);((CheckBox)Field(render,"darkMode")).Checked=page.EndsWith(" dark");Call(render,"ShowPage",page.Replace(" dark",""));
                            var chart=(Trend)Field(render,"trend");chart.Clear();DateTime chartEnd=DateTime.UtcNow;for(int i=0;i<8;i++)chart.Add(52.0+(i%3)/10.0,chartEnd.AddSeconds((i-7)*15));
                            Control root=render.Controls[0];root.Font=render.Font;render.Controls.Remove(root);root.Size=render.ClientSize;root.Visible=true;Layout(root);
                            using(var bitmap=new System.Drawing.Bitmap(root.Width,root.Height)){root.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,root.Width,root.Height));bitmap.Save(System.IO.Path.Combine(args[0],page.Replace(" ","-")+".png"),System.Drawing.Imaging.ImageFormat.Png);}
                            render.Controls.Add(root);((Timer)Field(render,"ageTimer")).Stop();
                        }
                    }
                    Console.WriteLine("Rendered hidden test forms; no desktop input or serial connection.");
                }
                ((Timer)Field(form,"ageTimer")).Stop();
                Console.WriteLine("PASS UI: all 26 controls, current/draft separation, change tracking, read-only cells, disconnected guards and completion, preserved error results, application name.");
            }
            var culture=System.Threading.Thread.CurrentThread.CurrentCulture;
            try{System.Threading.Thread.CurrentThread.CurrentCulture=new System.Globalization.CultureInfo("de-DE");Assert(Catalog.ByAddress(6).Parse("56.4",4)==141&&Catalog.ByAddress(6).Parse("56,4",4)==141,"Decimal separators");}
            finally{System.Threading.Thread.CurrentThread.CurrentCulture=culture;}
            Console.WriteLine("PASS decimal-point and decimal-comma input. No serial port opened.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Layout(Control control){control.CreateControl();control.PerformLayout();foreach(Control child in control.Controls)Layout(child);control.PerformLayout();}
    static Control Find(Control root,Type type){foreach(Control child in root.Controls){if(child.GetType()==type)return child;Control found=Find(child,type);if(found!=null)return found;}return null;}
}
