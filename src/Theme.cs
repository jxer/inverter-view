using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace InverterDesk
{
    public static class Theme
    {
        public static bool Dark;
        private sealed class Colors { public Color Back,Fore; public Font Font,BoldFont,GridFont,GridBoldFont; public bool HooksAdded; }
        private static readonly ConditionalWeakTable<Control,Colors> originals=new ConditionalWeakTable<Control,Colors>();
        public static Color Text { get { return Dark?Color.FromArgb(228,234,242):Color.FromArgb(15,27,40); } }
        public static Color Muted { get { return Dark?Color.FromArgb(165,182,198):Color.FromArgb(38,49,61); } }
        public static Color Good { get { return Dark?Color.FromArgb(87,208,152):Color.FromArgb(0,133,78); } }
        public static Color Warning { get { return Dark?Color.FromArgb(245,192,98):Color.FromArgb(148,87,15); } }
        public static Color Bad { get { return Dark?Color.FromArgb(255,133,133):Color.Firebrick; } }
        public static Color Accent { get { return Dark?Color.FromArgb(79,205,200):Color.FromArgb(0,126,126); } }
        public static Color Background(Color original)
        {
            if(!Dark)
            {
                if(original.ToArgb()==Color.FromArgb(242,246,250).ToArgb())return Color.FromArgb(232,241,250);
                if(original==Color.White||original.ToArgb()==SystemColors.Window.ToArgb())return Color.FromArgb(247,251,255);
                return original;
            }
            if(original.ToArgb()==Color.FromArgb(17,35,53).ToArgb())return original;
            if(original==Color.White||original.ToArgb()==SystemColors.Window.ToArgb())return Color.FromArgb(32,43,56);
            if(original.ToArgb()==Color.FromArgb(255,239,199).ToArgb()||original.ToArgb()==Color.FromArgb(255,225,158).ToArgb())return Color.FromArgb(95,70,26);
            if(original.ToArgb()==Color.FromArgb(210,240,234).ToArgb()||original.ToArgb()==Color.FromArgb(190,226,220).ToArgb())return Color.FromArgb(24,76,69);
            if(original.ToArgb()==Color.FromArgb(225,234,242).ToArgb())return Color.FromArgb(48,65,82);
            if(original==Color.Transparent||original==Color.Empty)return original;
            return original.GetBrightness()>0.8f?Color.FromArgb(23,32,43):original;
        }
        private static Color Foreground(Color original)
        {
            if(!Dark)
            {
                if(original.ToArgb()==Color.FromArgb(90,107,123).ToArgb())return Muted;
                if(original.ToArgb()==Color.FromArgb(25,45,65).ToArgb())return Text;
                return original;
            }
            if(original.ToArgb()==Color.Firebrick.ToArgb())return Bad;
            if(original.ToArgb()==Color.FromArgb(148,87,15).ToArgb())return Warning;
            if(original.ToArgb()==Color.FromArgb(0,126,126).ToArgb())return Accent;
            if(original==Color.White)return original;
            return original.GetBrightness()<0.5f?Text:original;
        }
        public static void Apply(Control control)
        {
            Capture(control);ApplyCore(control);
        }
        private static void Capture(Control control)
        {
            originals.GetValue(control,c=>{
                var grid=c as DataGridView;Font gridFont=grid==null?c.Font:grid.DefaultCellStyle.Font??c.Font;
                return new Colors { Back=c.BackColor,Fore=c.ForeColor,Font=c.Font,BoldFont=c.Font.Bold?c.Font:new Font(c.Font,c.Font.Style|FontStyle.Bold),GridFont=gridFont,GridBoldFont=gridFont.Bold?gridFont:new Font(gridFont,gridFont.Style|FontStyle.Bold) };
            });
            foreach(Control child in control.Controls)Capture(child);
        }
        private static void ApplyCore(Control control)
        {
            Colors colors=originals.GetOrCreateValue(control);
            control.BackColor=Background(colors.Back);control.ForeColor=Foreground(colors.Fore);control.Font=Dark?colors.Font:colors.BoldFont;
            Form form=control as Form;
            if(form!=null){if(!colors.HooksAdded){form.HandleCreated+=(s,e)=>ApplyTitleBar(form);colors.HooksAdded=true;}ApplyTitleBar(form);}
            Button button=control as Button;if(button!=null){button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;}
            ComboBox combo=control as ComboBox;
            if(combo!=null&&!colors.HooksAdded)
            {
                combo.FlatStyle=FlatStyle.Flat;combo.DrawMode=DrawMode.OwnerDrawFixed;
                combo.DrawItem+=(sender,e)=>{
                    Color background=(e.State&DrawItemState.Selected)!=0?(Dark?Color.FromArgb(34,83,89):Color.FromArgb(216,241,240)):combo.BackColor;
                    using(var brush=new SolidBrush(background))e.Graphics.FillRectangle(brush,e.Bounds);
                    string label=e.Index>=0&&e.Index<combo.Items.Count?combo.GetItemText(combo.Items[e.Index]):combo.Text;
                    TextRenderer.DrawText(e.Graphics,label,e.Font,e.Bounds,combo.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                };colors.HooksAdded=true;
            }
            DataGridView grid=control as DataGridView;
            if(grid!=null)
            {
                grid.BackgroundColor=Background(Color.White);grid.GridColor=Dark?Color.FromArgb(65,82,99):Color.FromArgb(218,226,234);
                grid.DefaultCellStyle.BackColor=Background(Color.White);grid.DefaultCellStyle.ForeColor=Text;
                grid.DefaultCellStyle.Font=Dark?colors.GridFont:colors.GridBoldFont;grid.ColumnHeadersDefaultCellStyle.Font=control.Font;
                grid.DefaultCellStyle.SelectionBackColor=Dark?Color.FromArgb(34,83,89):Color.FromArgb(216,241,240);grid.DefaultCellStyle.SelectionForeColor=Text;
                grid.AlternatingRowsDefaultCellStyle.BackColor=Dark?Color.FromArgb(37,49,63):Color.FromArgb(235,244,253);
                grid.ColumnHeadersDefaultCellStyle.BackColor=Background(Color.FromArgb(225,234,242));grid.ColumnHeadersDefaultCellStyle.ForeColor=Text;
                if(!colors.HooksAdded){grid.EditingControlShowing+=(sender,e)=>Apply(e.Control);colors.HooksAdded=true;}
            }
            foreach(Control child in control.Controls)ApplyCore(child);
            control.Invalidate();
        }
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        private static void ApplyTitleBar(Form form)
        {
            if(!form.IsHandleCreated)return;
            // Windows 11 caption/text colors; older versions keep their native title bar.
            // https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
            try
            {
                int immersive=Dark?1:0;DwmSetWindowAttribute(form.Handle,20,ref immersive,4);
                Color color=Dark?Color.FromArgb(17,35,53):Color.FromArgb(43,85,124);
                int caption=color.R|(color.G<<8)|(color.B<<16),text=0xFFFFFF;
                DwmSetWindowAttribute(form.Handle,35,ref caption,4);DwmSetWindowAttribute(form.Handle,36,ref text,4);
            }
            catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        }
    }
}
