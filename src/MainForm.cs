// Copyright (c) 2026 vevor-inverter-desk contributors. MIT license.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace InverterDesk
{
    public sealed class MetricIcon : Control
    {
        private readonly string kind;
        public MetricIcon(string name)
        {
            kind=name;Size=new Size(34,34);TabStop=false;AccessibleName=name+" icon";AccessibleRole=AccessibleRole.Graphic;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;var saved=g.Save();float scale=Math.Max(1,Math.Min(Width-8,Height-8))/32f;
            g.TranslateTransform((Width-32*scale)/2,(Height-32*scale)/2);g.ScaleTransform(scale,scale);g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var pen=new Pen(Theme.Accent,1.8f))using(var fill=new SolidBrush(Theme.Accent))
            {
                pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
                if(kind=="Reported load")
                {
                    g.DrawArc(pen,4,10,24,24,180,180);g.DrawLine(pen,4,23,28,23);
                    for(int i=0;i<5;i++){double angle=Math.PI+i*Math.PI/4;g.DrawLine(pen,16+(float)Math.Cos(angle)*9,22+(float)Math.Sin(angle)*9,16+(float)Math.Cos(angle)*12,22+(float)Math.Sin(angle)*12);}
                    g.DrawLine(pen,16,22,23,14);g.FillEllipse(fill,14,20,4,4);
                }
                else if(kind=="Battery voltage")
                {
                    g.DrawRectangle(pen,3,8,24,16);g.DrawRectangle(pen,27,13,3,6);
                    for(int i=0;i<3;i++)g.FillRectangle(fill,6+i*7,11,4,10);
                }
                else if(kind==MainForm.AcToBatteryTitle)
                {
                    g.DrawEllipse(pen,2,2,28,28);var wave=new PointF[25];
                    for(int i=0;i<wave.Length;i++)wave[i]=new PointF(6+i*20f/24,16-(float)Math.Sin(i*Math.PI*2/24)*6);
                    g.DrawLines(pen,wave);
                }
                else if(kind==MainForm.SolarToBatteryTitle)
                {
                    g.DrawEllipse(pen,22,3,5,5);g.DrawLine(pen,24.5f,0.5f,24.5f,1);g.DrawLine(pen,29,5.5f,31,5.5f);g.DrawLine(pen,19,5.5f,20,5.5f);g.DrawLine(pen,28,9,29,10);
                    g.DrawPolygon(pen,new[]{new PointF(6,13),new PointF(25,13),new PointF(29,27),new PointF(2,27)});
                    g.DrawLine(pen,5,20,27,20);g.DrawLine(pen,12,13,10,27);g.DrawLine(pen,19,13,21,27);g.DrawLine(pen,15,27,15,31);g.DrawLine(pen,10,31,20,31);
                }
            }
            g.Restore(saved);
        }
    }
    public sealed class ToggleSwitch : CheckBox
    {
        public string CheckedLabel,UncheckedLabel;
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            AutoSize=false;Cursor=Cursors.Hand;Size=new Size(161,34);
        }
        protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float scale=e.Graphics.DpiX/96f,w=42*scale,h=23*scale,x=Width-w-1,y=(Height-h)/2f;
            using(var path=new GraphicsPath())
            using(var track=new SolidBrush(Checked?Color.FromArgb(0,155,153):Color.FromArgb(93,114,134)))
            using(var thumb=new SolidBrush(Color.White))
            {
                path.AddArc(x,y,h,h,90,180);path.AddArc(x+w-h,y,h,h,270,180);path.CloseFigure();e.Graphics.FillPath(track,path);
                float inset=3*scale,d=h-2*inset,thumbX=Checked?x+w-inset-d:x+inset;e.Graphics.FillEllipse(thumb,thumbX,y+inset,d,d);
                string label=Checked?CheckedLabel:UncheckedLabel;
                if(!string.IsNullOrEmpty(label))using(var font=new Font("Segoe UI",7.5f,FontStyle.Bold))using(var ink=new SolidBrush(Color.FromArgb(15,27,40)))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                    e.Graphics.DrawString(label,font,ink,new RectangleF(thumbX,y+inset,d,d),format);
            }
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(0,0,Math.Max(0,(int)x-7),Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(0,1,Width-1,Height-2),ForeColor,BackColor);
        }
    }
    public sealed class Trend : Control
    {
        private readonly List<KeyValuePair<DateTime,double>> samples=new List<KeyValuePair<DateTime,double>>();
        private readonly ToolTip tooltip=new ToolTip();
        public Trend(){DoubleBuffered=true;BackColor=Color.White;}
        public void Add(double value,DateTime receivedUtc){samples.Add(new KeyValuePair<DateTime,double>(receivedUtc,value));if(samples.Count>180)samples.RemoveAt(0);Invalidate();}
        public void Clear(){samples.Clear();tooltip.SetToolTip(this,null);Invalidate();}
        private Rectangle Plot { get { return new Rectangle(52,50,Math.Max(50,Width-72),Math.Max(30,Height-66)); } }
        private double Fraction(DateTime time){double span=(samples.Last().Key-samples[0].Key).TotalSeconds;return span<=0?1:(time-samples[0].Key).TotalSeconds/span;}
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(samples.Count==0||!Plot.Contains(e.Location)){tooltip.SetToolTip(this,null);return;}
            var sample=samples.OrderBy(p=>Math.Abs(Plot.Left+Fraction(p.Key)*Plot.Width-e.X)).First();
            string text=sample.Value.ToString("0.0")+" V";
            if(tooltip.GetToolTip(this)!=text)tooltip.SetToolTip(this,text);
        }
        protected override void Dispose(bool disposing){if(disposing)tooltip.Dispose();base.Dispose(disposing);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var f=new Font("Segoe UI",10,Theme.Dark?FontStyle.Regular:FontStyle.Bold))using(var brush=new SolidBrush(Theme.Muted))
            {
                e.Graphics.DrawString("BATTERY VOLTAGE",f,brush,16,14);
                if(samples.Count==0){e.Graphics.DrawString("Connect to build a voltage history.",f,brush,16,58);return;}
                double low=Math.Floor((samples.Min(p=>p.Value)-0.1)*10)/10, high=Math.Ceiling((samples.Max(p=>p.Value)+0.1)*10)/10;
                Rectangle plot=Plot;
                using(var grid=new Pen(Theme.Dark?Color.FromArgb(65,82,99):Color.FromArgb(229,236,242)))for(int j=0;j<3;j++){float y=plot.Top+j*plot.Height/2f;e.Graphics.DrawLine(grid,plot.Left,y,plot.Right,y);e.Graphics.DrawString((high-(high-low)*j/2).ToString("0.0"),f,brush,8,y-7);}
                var points=samples.Select(p=>new PointF(plot.Left+(float)Fraction(p.Key)*plot.Width,plot.Bottom-(float)((p.Value-low)/(high-low))*plot.Height)).ToArray();
                using(var line=new Pen(Theme.Accent,2.5f))
                {if(points.Length>1)e.Graphics.DrawLines(line,points);else e.Graphics.DrawEllipse(line,points[0].X-2,points[0].Y-2,4,4);}
            }
        }
    }
    public sealed class MainForm : Form
    {
        internal const string AcToBatteryTitle = "AC → Battery";
        internal const string SolarToBatteryTitle = "Solar → Battery";
        private static readonly Color Navy=Color.FromArgb(17,35,53), Ink=Color.FromArgb(25,45,65), Muted=Color.FromArgb(90,107,123), Teal=Color.FromArgb(0,126,126), Paper=Color.FromArgb(242,246,250);
        private readonly ComboBox ports=new ComboBox(),bauds=new ComboBox();
        private readonly ToggleSwitch temperatureUnits=new ToggleSwitch{UncheckedLabel="°C",CheckedLabel="°F"};
        private readonly bool persistPreferences;
        private readonly Button connect=new Button(),export=new Button(),restore=new Button();
        private readonly CheckBox connection=new CheckBox();
        private readonly CheckBox darkMode=new ToggleSwitch();
        private readonly Label footer=new Label();
        private readonly Panel host=new Panel();
        private readonly Dictionary<string,Control> pages=new Dictionary<string,Control>();
        private readonly Dictionary<string,Button> nav=new Dictionary<string,Button>();
        private readonly Dictionary<string,Label> metrics=new Dictionary<string,Label>(),details=new Dictionary<string,Label>();
        private readonly Dictionary<string,Label> chargingWatts = new Dictionary<string,Label>();
        private readonly Label bulkVoltage = new Label(), floatVoltage = new Label();
        private readonly Dictionary<int,string> outcomes=new Dictionary<int,string>();
        private readonly Dictionary<int,int> verifiedValues=new Dictionary<int,int>();
        private readonly HashSet<int> populated=new HashSet<int>();
        private readonly List<Button> writeButtons=new List<Button>();
        private readonly DataGridView settings=new DataGridView(),settingsRight=new DataGridView(),calibration=new DataGridView();
        private readonly TextBox traffic=new TextBox(),diagnostic=new TextBox();
        private readonly RichTextBox machineStatus=new RichTextBox();
        private readonly CheckBox otherProfile=new CheckBox();
        private readonly ComboBox helpTopics=new ComboBox();
        private readonly RichTextBox helpBody=new RichTextBox();
        private readonly Trend trend=new Trend();
        private readonly object gate=new object();
        private Action<SerialTransport,Journal> pending;
        private Thread worker;
        private volatile bool stop;
        private bool connected,busy,closeWhenStopped;
        private Status latest,sessionBaseline;
        private Setting undoSetting;
        private int undoRaw,undoTarget;
        private string logPath;
        private readonly System.Windows.Forms.Timer ageTimer=new System.Windows.Forms.Timer();
        public MainForm(bool savePreferences=true)
        {
            persistPreferences=savePreferences;
            Text="Inverter View";Font=new Font("Segoe UI",10);BackColor=Paper;ForeColor=Ink;Size=new Size(1360,900);MinimumSize=new Size(1180,800);StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,240));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(root);
            var sidebar=new Panel{Dock=DockStyle.Fill,BackColor=Navy,Margin=Padding.Empty};root.Controls.Add(sidebar,0,0);
            var side=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,BackColor=Navy,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(18,26,18,12),Margin=Padding.Empty};sidebar.Controls.Add(side);
            side.Controls.Add(new Label{Text="Inverter View",Font=new Font("Segoe UI",17,FontStyle.Bold),ForeColor=Color.White,Size=new Size(204,42),Margin=new Padding(0,0,0,18),AutoEllipsis=false});
            foreach(string name in new[]{"Dashboard","Device settings","Calibration","Traffic & diagnostics","Help"}){string n=name;var b=Button(n,204,42);b.Margin=new Padding(0,0,0,7);b.TextAlign=ContentAlignment.MiddleLeft;b.BackColor=Navy;b.ForeColor=Color.FromArgb(208,225,237);b.Click+=(s,e)=>ShowPage(n);nav.Add(n,b);side.Controls.Add(b);}
            side.Controls.Add(BuildConnectionControls());
            var sidebarFooter=new Panel{Dock=DockStyle.Bottom,Height=161,BackColor=Navy};
            darkMode.Text="Dark mode";darkMode.AccessibleName="Dark mode";darkMode.Location=new Point(22,5);darkMode.Width=196;darkMode.ForeColor=Color.FromArgb(208,225,237);sidebarFooter.Controls.Add(darkMode);
            temperatureUnits.Text="Temperature";temperatureUnits.AccessibleName="Temperature unit: Celsius or Fahrenheit";temperatureUnits.Font=new Font("Segoe UI",9);temperatureUnits.ForeColor=Color.FromArgb(208,225,237);temperatureUnits.Location=new Point(22,45);temperatureUnits.Width=196;sidebarFooter.Controls.Add(temperatureUnits);
            var github=new LinkLabel{Text="GitHub",Location=new Point(22,91),AutoSize=true,LinkColor=Color.FromArgb(166,215,232),ActiveLinkColor=Color.White,VisitedLinkColor=Color.FromArgb(166,215,232)};github.LinkClicked+=(s,e)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/jxer/inverter-view"){UseShellExecute=true});}catch(Exception ex){footer.Text="Could not open GitHub: "+ex.Message;}};sidebarFooter.Controls.Add(github);
            sidebarFooter.Controls.Add(new Label{Text="v"+AppInfo.Version,ForeColor=Color.FromArgb(139,175,195),Dock=DockStyle.Bottom,Height=34,Padding=new Padding(22,0,0,10)});sidebar.Controls.Add(sidebarFooter);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(22,18,22,12),Margin=Padding.Empty};right.RowStyles.Add(new RowStyle(SizeType.Percent,100));right.RowStyles.Add(new RowStyle(SizeType.Absolute,64));root.Controls.Add(right,1,0);
            if(persistPreferences)try{var pref=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preferences.json")));if(pref!=null){if(pref.ContainsKey("temperatureUnit")&&pref["temperatureUnit"]=="F")temperatureUnits.Checked=true;if(pref.ContainsKey("theme")&&pref["theme"]=="dark")darkMode.Checked=true;}}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}
            temperatureUnits.CheckedChanged+=(s,e)=>{if(latest!=null)UpdateTemperatures(latest);SavePreferences();};
            darkMode.CheckedChanged+=(s,e)=>{ApplyTheme();SavePreferences();};
            ports.DropDown+=(s,e)=>RefreshPorts();connect.Click+=(s,e)=>ToggleConnection();export.Click+=(s,e)=>ExportSettings();restore.Click+=(s,e)=>RestoreSettings();
            host.Dock=DockStyle.Fill;host.Margin=Padding.Empty;right.Controls.Add(host,0,0);
            footer.Dock=DockStyle.Fill;footer.Padding=new Padding(2,10,2,0);footer.ForeColor=Muted;footer.Text="Choose the inverter's COM port, then connect. The original monitoring app must release the port.";right.Controls.Add(footer,0,1);
            pages.Add("Dashboard",BuildOverview());pages.Add("Device settings",BuildSettings(false));pages.Add("Calibration",BuildSettings(true));pages.Add("Traffic & diagnostics",BuildDiagnostics());pages.Add("Help",BuildHelp());
            foreach(Control page in pages.Values){page.Dock=DockStyle.Fill;page.Visible=false;host.Controls.Add(page);}ShowPage("Dashboard");RefreshPorts();UpdateButtons();ApplyTheme();
            ageTimer.Interval=1000;ageTimer.Tick+=(s,e)=>UpdateConnectionLabel();ageTimer.Start();FormClosing+=OnClosing;
        }
        private static Button Button(string text,int width,int height=34){var b=new Button();Configure(b,text,width,height);return b;}
        private Control BuildConnectionControls()
        {
            var panel=new FlowLayoutPanel{Width=204,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,14,0,0)};
            panel.Controls.Add(new Label{Text="RS232 (serial) port",AutoSize=true,ForeColor=Color.FromArgb(208,225,237),Margin=new Padding(0,0,0,8)});
            var portRow=new FlowLayoutPanel{Width=204,Height=32,WrapContents=false,Margin=new Padding(0,0,0,7)};
            ports.Width=116;ports.DropDownStyle=ComboBoxStyle.DropDownList;ports.AccessibleName="RS232 serial port";ports.Margin=new Padding(0,0,7,0);portRow.Controls.Add(ports);
            bauds.Width=81;bauds.DropDownStyle=ComboBoxStyle.DropDownList;bauds.AccessibleName="Baud rate";bauds.Items.AddRange(new object[]{"2400","4800","9600"});bauds.SelectedIndex=0;bauds.Margin=Padding.Empty;portRow.Controls.Add(bauds);panel.Controls.Add(portRow);
            Configure(connect,"Connect",204);connect.Margin=new Padding(0,0,0,7);connect.BackColor=Teal;connect.ForeColor=Color.White;panel.Controls.Add(connect);
            Configure(export,"Save settings",204);Configure(restore,"Restore settings",204);export.Margin=new Padding(0,0,0,7);restore.Margin=Padding.Empty;panel.Controls.Add(export);panel.Controls.Add(restore);writeButtons.Add(restore);
            connection.AutoCheck=false;connection.TabStop=false;connection.Size=new Size(204,38);connection.Font=new Font("Segoe UI",12,FontStyle.Bold);connection.Text="Disconnected";connection.ForeColor=Color.Firebrick;connection.CheckAlign=ContentAlignment.MiddleLeft;connection.TextAlign=ContentAlignment.MiddleLeft;connection.Margin=new Padding(0,10,0,0);connection.AccessibleName="Connection status";panel.Controls.Add(connection);
            return panel;
        }
        private void SavePreferences()
        {
            if(!persistPreferences)return;
            try{File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preferences.json"),new JavaScriptSerializer().Serialize(new {temperatureUnit=temperatureUnits.Checked?"F":"C",theme=darkMode.Checked?"dark":"light"}));}
            catch(Exception ex){footer.Text="Display changed; preference could not be saved: "+ex.Message;}
        }
        private void ApplyTheme()
        {
            Theme.Dark=darkMode.Checked;Theme.Apply(this);
            if(latest!=null)OnStatus(latest,false);else {UpdateConnectionLabel();FitMachineStatus();}
        }
        private static void Configure(Button b,string text,int width,int height=34){b.Text=text;b.Width=width;b.Height=height;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=Color.FromArgb(225,234,242);b.ForeColor=Ink;b.Cursor=Cursors.Hand;b.Margin=new Padding(0,0,7,7);}
        private static Label Heading(string title,string subtitle){return new Label{Dock=DockStyle.Fill,Text=title+"\n"+subtitle,Font=new Font("Segoe UI",12),ForeColor=Ink,Padding=new Padding(0,4,0,8)};}
        private void ShowPage(string name){foreach(var p in pages)p.Value.Visible=p.Key==name;pages[name].BringToFront();foreach(var p in nav)p.Value.BackColor=p.Key==name?Teal:Navy;}
        private Control BuildChargingFooter(string title)
        {
            var footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 46, Margin = Padding.Empty };
            var watts = new Label {
                Text = "— W", Dock = DockStyle.Top, Height = 26,
                Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Teal,
                TextAlign = ContentAlignment.MiddleLeft,
                AccessibleDescription = "Calculated from battery voltage and this charger's DC current."
            };
            var caption = new Label {
                Text = "Battery-side charging", Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10), ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft
            };
            footerPanel.Controls.Add(caption);
            footerPanel.Controls.Add(watts);
            chargingWatts.Add(title, watts);
            return footerPanel;
        }

        private Control BuildBatterySettingsFooter()
        {
            var panel = new TableLayoutPanel {
                Dock = DockStyle.Bottom, Height = 46, ColumnCount = 1, RowCount = 2,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            bulkVoltage.Text = "Bulk: — V";
            floatVoltage.Text = "Float: — V";
            bulkVoltage.AccessibleName = "Current bulk charging voltage setting";
            floatVoltage.AccessibleName = "Current float charging voltage setting";
            foreach (Label label in new[] { bulkVoltage, floatVoltage })
            {
                label.Dock = DockStyle.Fill;
                label.Margin = Padding.Empty;
                label.Font = new Font("Segoe UI", 10);
                label.ForeColor = Muted;
                label.TextAlign = ContentAlignment.MiddleLeft;
                label.AccessibleDescription = "Latest setting reported by the inverter, in full-bank volts.";
            }
            panel.Controls.Add(bulkVoltage, 0, 0);
            panel.Controls.Add(floatVoltage, 0, 1);
            return panel;
        }

        private Control BuildOverview()
        {
            var page=new TableLayoutPanel{ColumnCount=1,RowCount=2,Margin=Padding.Empty};page.RowStyles.Add(new RowStyle(SizeType.Absolute,170));page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var cards=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};for(int i=0;i<4;i++)cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            string[] keys = { "Reported load", "Battery voltage", AcToBatteryTitle, SolarToBatteryTitle };
            for(int i=0;i<4;i++)
            {
                bool charging = keys[i] == AcToBatteryTitle || keys[i] == SolarToBatteryTitle;
                var card=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Margin=new Padding(0,0,10,12),Padding=new Padding(15,12,12,10)};
                var header=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=2,RowCount=1,Height=42,Margin=Padding.Empty};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));
                var title=new Label{Text=keys[i],Dock=DockStyle.Fill,ForeColor=Muted,Font=new Font("Segoe UI",12,FontStyle.Bold),Margin=Padding.Empty,TextAlign=ContentAlignment.MiddleLeft};
                var value=new Label{Text="—",Dock=DockStyle.Top,Height=46,Font=new Font("Segoe UI",charging?23:25,FontStyle.Bold),ForeColor=Teal};
                header.Controls.Add(title,0,0);header.Controls.Add(new MetricIcon(keys[i]){Dock=DockStyle.Fill,Margin=Padding.Empty},1,0);
                card.Controls.Add(value);card.Controls.Add(header);
                if (charging)
                    card.Controls.Add(BuildChargingFooter(keys[i]));
                else if (keys[i] == "Battery voltage")
                    card.Controls.Add(BuildBatterySettingsFooter());
                else if (keys[i] == "Reported load")
                    card.Controls.Add(new Label{Text="Device-reported percentage",Dock=DockStyle.Bottom,Height=42,Font=new Font("Segoe UI",10),ForeColor=Muted,TextAlign=ContentAlignment.MiddleLeft});
                metrics[keys[i]]=value;cards.Controls.Add(card,i,0);
            }
            page.Controls.Add(cards,0,0);
            var lower=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));page.Controls.Add(lower,0,1);
            var readings=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=17,BackColor=Color.White,Padding=new Padding(16),Margin=new Padding(0,0,10,0)};readings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));readings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));
            string[] fields={"Operating mode","AC input","Input frequency","AC output","Output frequency","PV voltage","Battery capacity","MCU temperature","Inverter temperature","Charger temperature","Runtime","Machine / bank","Inverter firmware","Charger firmware","Controller code","Charger code","Warnings / faults"};
            for(int i=0;i<fields.Length;i++){readings.RowStyles.Add(i==0?new RowStyle(SizeType.Absolute,44):new RowStyle(SizeType.Percent,100f/(fields.Length-1)));readings.Controls.Add(new Label{Text=fields[i],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Muted},0,i);var value=new Label{Text="—",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};details[fields[i]]=value;readings.Controls.Add(value,1,i);}lower.Controls.Add(readings,0,0);
            var chartBox=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};chartBox.RowStyles.Add(new RowStyle(SizeType.Percent,50));chartBox.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            var statusPanel=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(16,12,16,12),Margin=new Padding(0,0,0,5)};
            machineStatus.ReadOnly=true;machineStatus.Dock=DockStyle.Fill;machineStatus.BackColor=Color.White;machineStatus.BorderStyle=BorderStyle.None;machineStatus.Font=new Font("Segoe UI",14);machineStatus.ScrollBars=RichTextBoxScrollBars.Vertical;machineStatus.Text="Connect to read the machine status.";machineStatus.DetectUrls=false;machineStatus.Resize+=(s,e)=>FitMachineStatus();
            statusPanel.Controls.Add(machineStatus);statusPanel.Controls.Add(new Label{Text="Machine status",Dock=DockStyle.Top,Height=32,Font=new Font("Segoe UI",14,FontStyle.Bold)});chartBox.Controls.Add(statusPanel,0,0);
            trend.Dock=DockStyle.Fill;trend.Margin=new Padding(0,5,0,0);chartBox.Controls.Add(trend,0,1);lower.Controls.Add(chartBox,1,0);return page;
        }
        private Control BuildSettings(bool cal)
        {
            var page=new TableLayoutPanel { ColumnCount=1, RowCount=4, Margin=Padding.Empty };
            page.RowStyles.Add(new RowStyle(SizeType.Absolute,cal?102:63));
            page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute,cal?64:0));
            var heading=Heading(cal?"Calibration · experimental":"Device settings",
                cal?"These commands do not appear to work on the tested inverter. Battery-voltage writes had no observed effect; the other five have not been tested. Sending is not proof of acceptance.":"Current = device readback. Amber = pending edit. Teal = changed since connecting. Select a row to see its limits and result.");
            if(cal)heading.ForeColor=Color.FromArgb(148,87,15);
            page.Controls.Add(heading,0,0);
            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0),Margin=Padding.Empty };
            if(cal)
            {
                ConfigureGrid(calibration,Catalog.All.Where(d=>d.Calibration),true);
                page.Controls.Add(calibration,0,1);
                AddAction(actions,"Send selected command",190,()=>Apply(calibration,false),true);
                AddAction(actions,"Test, then restore",152,()=>Apply(calibration,true),true);
                AddAction(actions,"Use current values",146,()=>FillDrafts(calibration),false);
                AddAction(actions,"Reset drafts",108,()=>ResetDrafts(true),false);
                AddAction(actions,"Factory calibration reset",190,()=>ResetDevice("calibration"),true);
                AddAction(actions,"Help",65,()=>ShowSettingHelp(calibration),false);
                page.Controls.Add(new Label { Text="Reset drafts changes the editors only. Factory calibration reset sends an unverified device command. A temporary test automatically sends the captured original value back; persistence after restart remains untested.",Dock=DockStyle.Fill,ForeColor=Muted,Padding=new Padding(0,8,0,0),Font=new Font("Segoe UI",9) },0,3);
            }
            else
            {
                int[] left={0,1,12,11,13,2,3,4,5,19};
                int[] right={6,7,8,9,10,17,18,14,15,16};
                ConfigureGrid(settings,left.Select(Catalog.ByAddress),false);
                ConfigureGrid(settingsRight,right.Select(Catalog.ByAddress),false);
                var groups=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
                groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
                groups.Controls.Add(SettingsGroup("AC input / output and operating modes",settings),0,0);
                groups.Controls.Add(SettingsGroup("Battery voltages and options",settingsRight),1,0);
                page.Controls.Add(groups,0,1);
                AddAction(actions,"Undo last change",143,Undo,true);
                AddAction(actions,"Use current values",146,()=>{FillDrafts(settings);FillDrafts(settingsRight);},false);
                AddAction(actions,"Reset drafts",108,()=>ResetDrafts(false),false);
                AddAction(actions,"Reset runtime",119,()=>ResetDevice("runtime"),true);
                AddAction(actions,"Factory reset",119,()=>ResetDevice("parameters"),true);
            }
            page.Controls.Add(actions,0,2);return page;
        }
        private void AddAction(FlowLayoutPanel panel,string text,int width,Action action,bool writes)
        {
            var button=Button(text,width);button.Click+=(s,e)=>action();panel.Controls.Add(button);if(writes)writeButtons.Add(button);
        }
        private Control SettingsGroup(string title,DataGridView grid)
        {
            var group=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Margin=new Padding(0,0,10,0) };
            group.RowStyles.Add(new RowStyle(SizeType.Absolute,27));group.RowStyles.Add(new RowStyle(SizeType.Percent,100));group.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            group.Controls.Add(new Label { Text=title,Dock=DockStyle.Fill,Font=new Font("Segoe UI",10,FontStyle.Bold) },0,0);
            group.Controls.Add(grid,0,1);
            var apply=Button("Apply selected setting",196);apply.Dock=DockStyle.Left;apply.BackColor=Teal;apply.ForeColor=Color.White;apply.Click+=(s,e)=>Apply(grid,false);writeButtons.Add(apply);var buttons=new Panel{Dock=DockStyle.Fill};buttons.Controls.Add(apply);var help=Button("Help",72);help.Dock=DockStyle.Right;help.Click+=(s,e)=>ShowSettingHelp(grid);buttons.Controls.Add(help);group.Controls.Add(buttons,0,2);
            return group;
        }
        private void ConfigureGrid(DataGridView grid,IEnumerable<Setting> items,bool cal)
        {
            grid.Dock=DockStyle.Fill;grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.AllowUserToResizeRows=false;
            grid.RowHeadersVisible=false;grid.MultiSelect=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            grid.RowTemplate.Height=cal?50:36;grid.ColumnHeadersHeight=30;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(225,234,242);
            grid.ColumnHeadersDefaultCellStyle.ForeColor=Ink;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(216,241,240);grid.DefaultCellStyle.SelectionForeColor=Ink;
            grid.DefaultCellStyle.Padding=new Padding(3);grid.DefaultCellStyle.Font=new Font("Segoe UI",9);grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(248,250,252);grid.EditMode=DataGridViewEditMode.EditOnEnter;
            string[] names={"Setting","Current","New value","Change","Allowed values","Last result"};int[] weights=cal?new[]{23,13,14,18,15,17}:new[]{35,20,23,22,1,1};
            for(int i=0;i<names.Length;i++){grid.Columns.Add("c"+i,names[i]);grid.Columns[i].FillWeight=weights[i];grid.Columns[i].ReadOnly=i!=2;grid.Columns[i].SortMode=DataGridViewColumnSortMode.NotSortable;grid.Columns[i].DefaultCellStyle.WrapMode=DataGridViewTriState.True;}
            if(!cal){grid.Columns[4].Visible=false;grid.Columns[5].Visible=false;}
            foreach(Setting d in items)
            {
                int i=grid.Rows.Add(d.Name,"—","","Waiting",d.BatteryVoltage?"Connect to identify bank":d.Range(4),d.Calibration?(d.Address==50?"No effect observed":"Not yet tested"):(d.Address==6?"Previously verified":"Not yet hardware-verified"));
                grid.Rows[i].Tag=d;if(d.Choices!=null)grid.Rows[i].Cells[2]=new DataGridViewComboBoxCell { DataSource=d.Choices,FlatStyle=FlatStyle.Flat };
            }
            grid.DataError+=(s,e)=>{e.ThrowException=false;};
            grid.CellValueChanged+=(s,e)=>{if(e.ColumnIndex==2&&e.RowIndex>=0&&latest!=null){var row=grid.Rows[e.RowIndex];var setting=row.Tag as Setting;if(setting!=null){outcomes.Remove(setting.Address);verifiedValues.Remove(setting.Address);}UpdateChange(row,latest);}};
            grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(grid.IsCurrentCellDirty&&grid.CurrentCell is DataGridViewComboBoxCell)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            grid.SelectionChanged+=(s,e)=>{if(latest!=null&&grid.CurrentRow!=null&&!busy){var d=(Setting)grid.CurrentRow.Tag;if(d!=null)footer.Text=d.Name+" · "+d.Range(latest.Units)+" · "+Convert.ToString(grid.CurrentRow.Cells[5].Value);}};
        }
        private void ResetDrafts(bool cal)
        {
            if(latest==null||busy)return;
            foreach(var grid in cal?new[]{calibration}:new[]{settings,settingsRight})
            {
                grid.CancelEdit();foreach(DataGridViewRow row in grid.Rows){var d=(Setting)row.Tag;row.Cells[2].Value=d.Editable(Catalog.DefaultRaw(d.Address),latest.Units);}
            }
            footer.Text="Draft values reset to the original PC utility's defaults. Nothing was sent. Review and apply individual settings if needed.";
        }

        private Control BuildHelp()
        {
            var page=new TableLayoutPanel{ColumnCount=1,RowCount=3,Margin=Padding.Empty};page.RowStyles.Add(new RowStyle(SizeType.Absolute,65));page.RowStyles.Add(new RowStyle(SizeType.Absolute,44));page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            page.Controls.Add(Heading("Settings help","A basic guide to the controls and what they do."),0,0);
            helpTopics.Dock=DockStyle.Top;helpTopics.DropDownStyle=ComboBoxStyle.DropDownList;helpTopics.Items.Add("Using settings");
            foreach(var setting in Catalog.All)helpTopics.Items.Add((setting.Calibration?"Calibration: ":"")+setting.Name);
            helpTopics.SelectedIndexChanged+=(s,e)=>UpdateHelp();page.Controls.Add(helpTopics,0,1);
            helpBody.Dock=DockStyle.Fill;helpBody.ReadOnly=true;helpBody.BackColor=Color.White;helpBody.ForeColor=Ink;helpBody.Font=new Font("Segoe UI",11);helpBody.BorderStyle=BorderStyle.None;helpBody.DetectUrls=false;helpBody.ScrollBars=RichTextBoxScrollBars.Vertical;page.Controls.Add(helpBody,0,2);
            helpTopics.SelectedIndex=0;return page;
        }
        private void ShowSettingHelp(DataGridView grid)
        {
            if(grid.CurrentRow==null)return;var setting=(Setting)grid.CurrentRow.Tag;ShowPage("Help");helpTopics.SelectedIndex=Array.IndexOf(Catalog.All,setting)+1;UpdateHelp();
        }
        private void UpdateHelp()
        {
            int index=helpTopics.SelectedIndex;
            if(index<=0){helpBody.Text="USING SETTINGS\r\n\r\n"+HelpGuide.Introduction;return;}
            Setting setting=Catalog.All[index-1];string text=setting.Name.ToUpperInvariant()+"\r\n\r\n"+HelpGuide.For(setting.Address);
            if(latest!=null)text+="\r\n\r\nAPP INPUT RANGE\r\n"+setting.Range(latest.Units);
            helpBody.Text=text;helpBody.Select(0,0);
        }
        private Control BuildDiagnostics()
        {
            var page=new TableLayoutPanel{ColumnCount=1,RowCount=5,Margin=Padding.Empty};page.RowStyles.Add(new RowStyle(SizeType.Absolute,63));page.RowStyles.Add(new RowStyle(SizeType.Absolute,105));page.RowStyles.Add(new RowStyle(SizeType.Percent,100));page.RowStyles.Add(new RowStyle(SizeType.Absolute,49));page.RowStyles.Add(new RowStyle(SizeType.Absolute,90));
            page.Controls.Add(Heading("Traffic & diagnostics","Complete transmitted and received bytes are saved for each live session."),0,0);
            diagnostic.Multiline=true;diagnostic.ReadOnly=true;diagnostic.Dock=DockStyle.Fill;diagnostic.BorderStyle=BorderStyle.None;diagnostic.BackColor=Paper;diagnostic.Font=new Font("Consolas",9);diagnostic.Text="No device connected.";page.Controls.Add(diagnostic,0,1);
            traffic.Multiline=true;traffic.ReadOnly=true;traffic.WordWrap=false;traffic.ScrollBars=ScrollBars.Both;traffic.Dock=DockStyle.Fill;traffic.BackColor=Navy;traffic.ForeColor=Color.FromArgb(204,229,238);traffic.BorderStyle=BorderStyle.None;traffic.Font=new Font("Consolas",9);page.Controls.Add(traffic,0,2);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0)};var save=Button("Export visible log",148);save.Click+=(s,e)=>SaveText("Inverter log","inverter-log.txt",traffic.Text);buttons.Controls.Add(save);var clear=Button("Clear view",103);clear.Click+=(s,e)=>traffic.Clear();buttons.Controls.Add(clear);
            var reset=Button("Maintenance...",140);reset.Click+=(s,e)=>Maintenance();buttons.Controls.Add(reset);writeButtons.Add(reset);page.Controls.Add(buttons,0,3);
            otherProfile.Text="Allow experimental writes on other V-Series firmware";otherProfile.AutoSize=false;otherProfile.Dock=DockStyle.Top;otherProfile.Height=31;otherProfile.CheckedChanged+=(s,e)=>{if(otherProfile.Checked&&MessageBox.Show(this,"Other device models and firmware have not been validated. The same-looking status fields may have different meanings. Allow vendor-format setting and calibration commands for this session?","Experimental device profile",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)otherProfile.Checked=false;};
            var bottom=new Panel{Dock=DockStyle.Fill};bottom.Controls.Add(new Label{Dock=DockStyle.Bottom,Height=51,Text="Closing or disconnecting during a test waits for the restoration attempt. If communication fails, restoration cannot be guaranteed; the result and log will say so.",ForeColor=Muted,Font=new Font("Segoe UI",9)});bottom.Controls.Add(otherProfile);page.Controls.Add(bottom,0,4);return page;
        }
        private void RefreshPorts(){string selected=ports.Text;ports.Items.Clear();foreach(string p in SerialPort.GetPortNames().OrderBy(x=>x))ports.Items.Add(p);if(ports.Items.Contains(selected))ports.SelectedItem=selected;else if(ports.Items.Contains("COM4"))ports.SelectedItem="COM4";else if(ports.Items.Count>0)ports.SelectedIndex=0;}
        private void Ui(Action action){if(!IsDisposed&&IsHandleCreated)try{BeginInvoke(action);}catch(InvalidOperationException){} }
        private void AddLog(string line){if(traffic.TextLength>300000)traffic.Text=traffic.Text.Substring(150000);traffic.AppendText(line+Environment.NewLine);}
        private void ToggleConnection()
        {
            if(worker!=null&&worker.IsAlive){stop=true;connected=false;connect.Enabled=false;footer.Text=busy?"Finishing verification / restoration before disconnecting...":"Disconnecting...";UpdateConnectionLabel();return;}
            RefreshPorts();
            if(string.IsNullOrEmpty(ports.Text)){footer.Text="No COM port is available. Connect the USB adapter, then open the port list.";return;}
            string port=ports.Text;int baud=int.Parse(bauds.Text);stop=false;latest=null;sessionBaseline=null;outcomes.Clear();verifiedValues.Clear();populated.Clear();trend.Clear();undoSetting=null;otherProfile.Checked=false;connect.Enabled=false;ports.Enabled=false;bauds.Enabled=false;footer.Text="Opening "+port+"...";
            worker=new Thread(()=>Session(port,baud)){IsBackground=true,Name="Inverter serial session"};worker.Start();
        }
        private void Session(string port,int baud)
        {
            try
            {
                string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Logs");string path=Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-session.jsonl");
                using(var journal=new Journal(path))
                {
                    journal.Line=line=>Ui(()=>AddLog(line));using(var serial=new SerialTransport(port,baud,journal))
                    {
                        serial.StatusReceived=s=>Ui(()=>OnStatus(s));Ui(()=>{connected=true;logPath=path;connect.Text="Disconnect";connect.Enabled=true;footer.Text="Connected. Live traffic is being recorded.";UpdateButtons();});
                        int failures=0;
                        while(!stop)
                        {
                            Action<SerialTransport,Journal> action=null;lock(gate){action=pending;pending=null;}
                            if(action!=null){action(serial,journal);continue;}
                            try{serial.Query();failures=0;}catch(TimeoutException e){failures++;journal.Record("Timeout",e.Message,null);Ui(()=>footer.Text="No fresh status reply. Settings are unavailable until communication resumes.");if(failures>=3)throw;}
                            serial.Delay(650);
                        }
                    }
                }
            }
            catch(Exception e){Ui(()=>{footer.Text="Disconnected: "+e.Message+" Close any other app using this COM port, then reconnect.";AddLog("ERROR  "+e.Message);});}
            finally{Ui(CompleteDisconnection);}
        }
        private void CompleteDisconnection()
        {
            connected=false;busy=false;worker=null;lock(gate){pending=null;}
            connect.Text="Connect";connect.Enabled=true;ports.Enabled=true;bauds.Enabled=true;
            // Keep errors and completed write/restore results visible after the port closes.
            if(footer.Text=="Disconnecting..."||footer.Text=="Finishing verification / restoration before disconnecting...")footer.Text="Disconnected.";
            UpdateConnectionLabel();if(closeWhenStopped)Close();
        }
        private void UpdateConnectionLabel()
        {
            bool fresh=connected&&latest!=null&&(DateTime.UtcNow-latest.ReceivedUtc).TotalSeconds<4;
            connection.Checked=fresh;connection.Text=fresh?"Connected":connected&&latest==null?"Connecting...":"Disconnected";
            connection.ForeColor=fresh?Theme.Good:connected&&latest==null?Theme.Warning:Theme.Bad;UpdateButtons();
        }
        private void UpdateButtons(){bool ready=connected&&!busy&&latest!=null&&(DateTime.UtcNow-latest.ReceivedUtc).TotalSeconds<4;foreach(Button b in writeButtons)b.Enabled=ready;export.Enabled=latest!=null;settings.ReadOnly=busy;settingsRight.ReadOnly=busy;calibration.ReadOnly=busy;otherProfile.Enabled=!busy;}
        private void UpdateChargingCard(string title, double batteryVoltage, double chargingAmps)
        {
            metrics[title].Text = chargingAmps.ToString("0.#") + " A DC";
            double watts = batteryVoltage * chargingAmps;
            chargingWatts[title].Text = "≈ " + watts.ToString("0") + " W";
        }

        private void OnStatus(Status s,bool received=true)
        {
            UpdateMachineStatus(s);
            details["MCU temperature"].Text="Not reported";details["Controller code"].Text="0x"+s.Data[26].ToString("X2");details["Charger code"].Text="0x"+s.Data[38].ToString("X2");
            latest = s;
            if (sessionBaseline == null) sessionBaseline = s;
            metrics["Battery voltage"].Text = s.Battery.ToString("0.0") + " V";
            Setting bulkSetting = Catalog.ByAddress(6), floatSetting = Catalog.ByAddress(7);
            bulkVoltage.Text = "Bulk: " + (s.Units > 0 ? bulkSetting.Text(s.Value(bulkSetting), s.Units) : "— V");
            floatVoltage.Text = "Float: " + (s.Units > 0 ? floatSetting.Text(s.Value(floatSetting), s.Units) : "— V");
            metrics["Reported load"].Text = s.Load + " %";
            UpdateChargingCard(AcToBatteryTitle, s.Battery, s.AcChargeCurrent);
            UpdateChargingCard(SolarToBatteryTitle, s.Battery, s.SolarCurrent);
            details["Operating mode"].Text=s.Mode;details["AC input"].Text=s.AcInput.ToString("0.0")+" V";details["Input frequency"].Text=s.InputHz.ToString("0.0")+" Hz";details["Output frequency"].Text=s.OutputHz.ToString("0.0")+" Hz";details["PV voltage"].Text=s.PvVoltage.ToString("0")+" V";details["AC output"].Text=s.Output.ToString("0.0")+" V";details["Battery capacity"].Text=s.Data[22]+" % (reported)";details["Inverter temperature"].Text=s.Data[25]==255?"Unavailable":s.Data[25]+" °C";details["Charger temperature"].Text=s.Data[37]==0||s.Data[37]==255?"Unavailable":s.Data[37]+" °C";details["Runtime"].Text=((int)(s.Runtime/24))+" d "+(s.Runtime%24).ToString("0.0")+" h";details["Machine / bank"].Text=s.Model+" / "+s.Units*12+" V";details["Inverter firmware"].Text=s.Firmware;details["Charger firmware"].Text=s.ChargerFirmware;details["Warnings / faults"].Text=s.Alarm?"Reported — inspect diagnostics":"None reported";details["Warnings / faults"].ForeColor=s.Alarm?Theme.Bad:Theme.Accent;
            foreach(var grid in new[]{settings,settingsRight,calibration})foreach(DataGridViewRow row in grid.Rows){Setting d=(Setting)row.Tag;row.Cells[1].Value=d.Text(s.Value(d),s.Units);row.Cells[4].Value=d.Range(s.Units);if(!populated.Contains(d.Address)){if(d.Choices==null||s.Value(d)>=d.Min&&s.Value(d)<=d.Max)row.Cells[2].Value=d.Editable(s.Value(d),s.Units);populated.Add(d.Address);}row.Cells[5].Value=outcomes.ContainsKey(d.Address)?outcomes[d.Address]:d.Calibration?(d.Address==50?"No effect observed":"Not yet tested"):(d.Address==6?"Previously verified":"Not yet hardware-verified");UpdateChange(row,s);}
            diagnostic.Text="Machine "+s.Model+" | Inverter "+s.Firmware+" | Charger "+s.ChargerFirmware+"\r\nStatus: "+Protocol.Hex(s.Data.Take(3).ToArray())+"    Warning: "+Protocol.Hex(s.Data.Skip(3).Take(3).ToArray())+"    Fault: "+Protocol.Hex(s.Data.Skip(6).Take(3).ToArray())+"\r\nController code: "+s.Data[26]+" (0x"+s.Data[26].ToString("X2")+")  |  Charger code: "+s.Data[38]+" (0x"+s.Data[38].ToString("X2")+")\r\nUnmapped extension: "+Protocol.Hex(s.Data.Skip(67).ToArray())+"\r\n"+(logPath==null?"Opening session log...":"Log: "+logPath);

            if(received)trend.Add(s.Battery,s.ReceivedUtc);UpdateConnectionLabel();
            UpdateTemperatures(s);
        }
        private void UpdateTemperatures(Status s)
        {
            details["Inverter temperature"].Text=Temperature(s.Data[25],false);
            details["Charger temperature"].Text=Temperature(s.Data[37],true);
        }
        private void UpdateMachineStatus(Status s)
        {
            machineStatus.Clear();
            string[] lines=s.MachineStatusText.Split(new[]{"\r\n"},StringSplitOptions.None);
            foreach(string line in lines)
            {
                int split=line.IndexOf(':');string label=split<0?"Operating mode: ":line.Substring(0,split+2),value=split<0?line:line.Substring(split+2);
                bool fault=value=="Fault reported"||(label=="Faults: "&&value!="None reported");
                bool warning=value=="Warning reported"||(label=="Warnings: "&&value!="None reported");
                Color color=fault?Theme.Bad:warning?Theme.Warning:value=="Inactive"||value=="Not connected"?Theme.Muted:value=="Not full"?Theme.Warning:Theme.Good;
                if(machineStatus.TextLength>0)machineStatus.AppendText("\n");
                machineStatus.SelectionColor=Theme.Text;machineStatus.AppendText(label);machineStatus.SelectionColor=color;machineStatus.AppendText(value);
            }
            FitMachineStatus();machineStatus.Select(0,0);
        }
        private void FitMachineStatus()
        {
            if(machineStatus.ClientSize.Width<40||machineStatus.ClientSize.Height<40)return;
            string[] lines=machineStatus.Lines;float pointSize=12;
            int width=machineStatus.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-6,height=machineStatus.ClientSize.Height-8;
            using(Graphics graphics=machineStatus.CreateGraphics())
            {
                for(float size=22;size>=12;size-=0.5f)
                using(var font=new Font(machineStatus.Font.FontFamily,size,machineStatus.Font.Style))
                {
                    int needed=0;
                    foreach(string line in lines)needed+=TextRenderer.MeasureText(graphics,line.Length==0?" ":line,font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPadding).Height;
                    if(needed<=height){pointSize=size;break;}
                }
            }
            float zoom=pointSize/machineStatus.Font.SizeInPoints;
            if(Math.Abs(machineStatus.ZoomFactor-zoom)>0.01f)machineStatus.ZoomFactor=zoom;
        }
        private string Temperature(int celsius,bool zeroUnavailable)
        {
            if(celsius==255||(zeroUnavailable&&celsius==0))return "Unavailable";
            return temperatureUnits.Checked?(celsius*9m/5m+32m).ToString("0.#")+" °F":celsius+" °C";
        }
        private void UpdateChange(DataGridViewRow row,Status status)
        {
            Setting d=row.Tag as Setting;if(d==null||sessionBaseline==null)return;
            bool changed=sessionBaseline.Value(d)!=status.Value(d),pendingEdit=false;string draftState="";
            string text=Convert.ToString(row.Cells[2].Value);
            if(!string.IsNullOrWhiteSpace(text))try{pendingEdit=d.Parse(text,status.Units)!=status.Value(d);if(pendingEdit)draftState="Pending edit";}catch(ArgumentException){pendingEdit=true;draftState="Check new value";}
            string observed=changed?d.Text(sessionBaseline.Value(d),sessionBaseline.Units)+" → "+d.Text(status.Value(d),status.Units):"Unchanged";
            row.Cells[3].Value=pendingEdit?(changed?observed+"\n"+draftState:draftState):observed;
            if(outcomes.ContainsKey(d.Address))
            {
                string result=outcomes[d.Address];
                if(result!="Change successful"||(verifiedValues.ContainsKey(d.Address)&&verifiedValues[d.Address]==status.Value(d)))row.Cells[3].Value=result;
            }
            row.Cells[1].Style.BackColor=changed?Theme.Background(Color.FromArgb(210,240,234)):Color.Empty;row.Cells[1].Style.SelectionBackColor=changed?Theme.Background(Color.FromArgb(190,226,220)):Color.Empty;
            row.Cells[2].Style.BackColor=pendingEdit?Theme.Background(Color.FromArgb(255,239,199)):Color.Empty;row.Cells[2].Style.SelectionBackColor=pendingEdit?Theme.Background(Color.FromArgb(255,225,158)):Color.Empty;
            row.Cells[2].ToolTipText=d.Range(status.Units);row.Cells[3].ToolTipText="At connection: "+d.Text(sessionBaseline.Value(d),sessionBaseline.Units)+". Current reported: "+d.Text(status.Value(d),status.Units)+". Draft values are not sent until you apply them.";
        }
        private void FillDrafts(DataGridView grid){if(latest==null||busy)return;grid.CancelEdit();foreach(DataGridViewRow row in grid.Rows){Setting d=(Setting)row.Tag;if(d.Choices==null||latest.Value(d)>=d.Min&&latest.Value(d)<=d.Max)row.Cells[2].Value=d.Editable(latest.Value(d),latest.Units);}}
        private void Apply(DataGridView grid,bool temporary)
        {
            if(!CanAct()||grid.CurrentRow==null)return;grid.EndEdit();Setting d=(Setting)grid.CurrentRow.Tag;
            try
            {
                int raw=d.Parse(Convert.ToString(grid.CurrentRow.Cells[2].Value),latest.Units);Status expected=latest;
                if(raw==expected.Value(d)){footer.Text="Already at that value. No write sent.";return;}
                string message=d.Name+": "+d.Text(expected.Value(d),expected.Units)+" → "+d.Text(raw,expected.Units)+".\n\n";
                if(d.Calibration)message+="Calibration is experimental.\n\n";
                message+=temporary?"Apply a temporary change, then restore the previous value?":"Apply this change? If it isn't successful, the app will try to restore the previous value.";
                if(MessageBox.Show(this,message,temporary?"Temporary test and restore":"Confirm device change",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
                EnqueueChange(d,raw,expected,temporary);
            }catch(Exception e){MessageBox.Show(this,e.Message,"Check the requested value",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        }
        private bool CanAct(){if(!connected||busy||latest==null||(DateTime.UtcNow-latest.ReceivedUtc).TotalSeconds>=4){footer.Text="Connect and wait for a fresh reading before changing the device.";return false;}return true;}
        private void EnqueueChange(Setting d,int raw,Status expected,bool temporary)
        {
            bool allow=otherProfile.Checked;busy=true;UpdateButtons();footer.Text="Queued "+d.Name+"...";
            lock(gate)pending=(serial,journal)=>{
                WriteResult result=Transactions.Change(serial,d,raw,expected,temporary,text=>Ui(()=>footer.Text=text),allow);
                journal.Record("Result",new JavaScriptSerializer().Serialize(new {setting=d.Name,result.Sent,result.ReadbackVerified,result.RestoreAttempted,result.RestoreVerified,result.Error,result.RestoreError,result.Message}),null);
                Ui(()=>{busy=false;outcomes[d.Address]=result.RestoreAttempted&&!result.RestoreVerified?"Restore failed":result.ReadbackVerified&&result.Error==null?(temporary?"Test successful; restored":"Change successful"):"Change not confirmed";if(result.ReadbackVerified&&!temporary&&result.Error==null)verifiedValues[d.Address]=result.Requested;if(result.Sent&&!temporary&&result.ReadbackVerified&&result.Error==null){undoSetting=d;undoRaw=result.Original;undoTarget=result.Requested;}if(result.Final!=null)OnStatus(result.Final,false);footer.Text=result.Message;UpdateButtons();if(result.RestoreAttempted&&!result.RestoreVerified)MessageBox.Show(this,result.Message,"Restoration not confirmed",MessageBoxButtons.OK,MessageBoxIcon.Warning);});
            };
        }
        private void Undo()
        {
            if(!CanAct())return;if(undoSetting==null){footer.Text="There is no verified change to undo in this session.";return;}if(latest.Value(undoSetting)!=undoTarget){footer.Text="The device value has changed since that operation. Review it before applying another value.";return;}
            if(MessageBox.Show(this,"Restore "+undoSetting.Name+" to "+undoSetting.Text(undoRaw,latest.Units)+"?","Undo last verified change",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes)EnqueueChange(undoSetting,undoRaw,latest,false);
        }
        private void Maintenance()
        {
            if(!CanAct())return;
            using(var dialog=new Form { Text="Device maintenance",Size=new Size(520,300),StartPosition=FormStartPosition.CenterParent,Font=Font,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false })
            {
                var panel=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(18) };dialog.Controls.Add(panel);
                panel.Controls.Add(new Label { Text="These device reset commands are unverified on this firmware.",Width=460,Height=55 });
                foreach(string entry in new[]{"parameters","runtime","calibration"})
                {
                    string kind=entry;var button=Button(ResetName(kind),440);button.Click+=(s,e)=>{dialog.Close();ResetDevice(kind);};panel.Controls.Add(button);
                }
                dialog.ShowDialog(this);
            }
        }
        private static string ResetName(string kind)
        { return kind=="parameters"?"Factory reset normal settings":kind=="runtime"?"Reset runtime counter":"Factory reset calibration"; }
        private void ResetDevice(string kind)
        {
            if(!CanAct())return;string label=ResetName(kind);
            if(MessageBox.Show(this,label+" on the connected inverter? This command is unverified and may replace values that cannot be recovered automatically.","Confirm device reset",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            Status expected=latest;bool allow=otherProfile.Checked;busy=true;UpdateButtons();
            lock(gate)pending=(serial,journal)=>{
                string result;
                try
                {
                    Status before=serial.Query();Transactions.CheckProfile(before,allow);
                    if(!before.SameConfiguration(expected,-1))throw new InvalidOperationException("Settings changed before reset; no reset sent.");
                    journal.Record("Before reset",new JavaScriptSerializer().Serialize(before.Snapshot()),null);
                    serial.Send(Protocol.Maintenance(kind),label);serial.Delay(5000);for(int j=0;j<3;j++)serial.Query();
                    result="Reset command sent; acceptance is not established. Review the reported values.";
                }
                catch(Exception ex){result="Reset result unconfirmed: "+ex.Message;}
                journal.Record("Maintenance",result,null);Ui(()=>{busy=false;footer.Text=result;populated.Clear();UpdateButtons();});
            };
        }
        private void ExportSettings(){if(latest==null)return;SaveText("Save inverter settings","inverter-settings.json",SettingsFile.Save(latest));}
        private void RestoreSettings()
        {
            if(!CanAct())return;
            using(var file=new OpenFileDialog { Title="Restore inverter settings",Filter="Inverter settings|*.json|All files|*.*",CheckFileExists=true })
            {
                if(file.ShowDialog(this)!=DialogResult.OK)return;
                try
                {
                    if(new FileInfo(file.FileName).Length>1048576)throw new ArgumentException("The settings file is too large.");
                    Status saved=SettingsFile.Read(File.ReadAllText(file.FileName)),expected=latest;
                    using(var review=new RestoreDialog(saved,expected))
                    {
                        if(review.ShowDialog(this)!=DialogResult.OK)return;
                        int[] selected=review.SelectedAddresses;bool allow=otherProfile.Checked;busy=true;undoSetting=null;UpdateButtons();footer.Text="Restoring selected settings...";
                        lock(gate)pending=(serial,journal)=>{
                            journal.Record("Before settings restore",new JavaScriptSerializer().Serialize(expected.Snapshot()),null);
                            SettingsRestoreResult result=SettingsRestore.Apply(serial,saved,selected,expected,message=>Ui(()=>footer.Text=message),(setting,step)=>{
                                journal.Record("Settings restore result",new JavaScriptSerializer().Serialize(new {setting=setting.Name,step.Requested,step.Original,step.ReadbackVerified,step.RestoreAttempted,step.RestoreVerified,step.Error,step.RestoreError,step.Message}),null);
                            },allow,()=>stop);
                            Ui(()=>{
                                busy=false;
                                foreach(var entry in result.Steps)
                                {
                                    Setting setting=entry.Key;WriteResult step=entry.Value;
                                    foreach(var grid in new[]{settings,settingsRight,calibration})foreach(DataGridViewRow row in grid.Rows)if(row.Tag==setting)row.Cells[2].Value=setting.Editable(step.Requested,expected.Units);
                                    outcomes[setting.Address]=step.RestoreAttempted&&!step.RestoreVerified?"Restore failed":step.ReadbackVerified&&step.Error==null||step.NoChange?"Change successful":"Change not confirmed";
                                    if((step.ReadbackVerified&&step.Error==null)||step.NoChange)verifiedValues[setting.Address]=step.Requested;
                                }
                                if(result.Final!=null)OnStatus(result.Final,false);footer.Text=result.Message;UpdateButtons();
                                if(!result.Success)MessageBox.Show(this,result.Message,"Settings restore stopped",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                            });
                        };
                    }
                }
                catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not restore settings",MessageBoxButtons.OK,MessageBoxIcon.Information);}
            }
        }
        private void SaveText(string title,string filename,string text){using(var dialog=new SaveFileDialog{Title=title,FileName=filename,Filter="Text and JSON|*.txt;*.json|All files|*.*"})if(dialog.ShowDialog(this)==DialogResult.OK)try{File.WriteAllText(dialog.FileName,text,new System.Text.UTF8Encoding(false));footer.Text="Saved "+dialog.FileName;}catch(Exception e){MessageBox.Show(this,e.Message,"Could not save",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
        private void OnClosing(object sender,FormClosingEventArgs e){if(worker!=null&&worker.IsAlive){e.Cancel=true;closeWhenStopped=true;stop=true;connected=false;footer.Text="Finishing any verification / restoration, then closing the serial port...";UpdateButtons();}else ageTimer.Stop();}
    }
}
