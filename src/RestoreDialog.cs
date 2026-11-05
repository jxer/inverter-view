using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace InverterDesk
{
    public sealed class RestoreDialog : Form
    {
        public int[] SelectedAddresses { get; private set; }
        public RestoreDialog(Status saved,Status current)
        {
            SettingsFile.CheckCompatible(saved,current);
            Text="Restore settings";Size=new Size(940,720);MinimumSize=new Size(780,550);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(242,246,250);
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(18),RowCount=3,ColumnCount=1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,87));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,55));Controls.Add(root);
            root.Controls.Add(new Label { Dock=DockStyle.Fill,Text="Choose which saved values to restore. Matching values are skipped. Calibration is experimental and must be selected separately.\n\nEach change is checked. If one fails, the app tries to revert that change and stops. Earlier successful changes remain applied." },0,0);
            var grid=new DataGridView { Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,EditMode=DataGridViewEditMode.EditOnEnter };
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name="selected",HeaderText="Restore",FillWeight=13 });
            foreach(string name in new[]{"Setting","Current","Saved","Status"})grid.Columns.Add(new DataGridViewTextBoxColumn { Name=name,HeaderText=name,ReadOnly=true,FillWeight=name=="Setting"?65:name=="Status"?50:28 });
            foreach(DataGridViewColumn column in grid.Columns){column.SortMode=DataGridViewColumnSortMode.NotSortable;column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;}
            grid.RowTemplate.Height=40;
            foreach(Setting setting in Catalog.All)
            {
                int value=saved.Value(setting),original=current.Value(setting);bool valid=value>=setting.Min&&value<=setting.Max&&original>=setting.Min&&original<=setting.Max;
                bool different=value!=original;
                string state=!valid?"Outside supported range":!different?"Already matches":setting.Calibration?(setting.Address==50?"Calibration: no effect observed":"Calibration: not tested"):"Ready to restore";
                int index=grid.Rows.Add(valid&&different&&!setting.Calibration,setting.Name+(setting.Calibration?" (calibration)":""),setting.Text(original,current.Units),setting.Text(value,saved.Units),state);
                grid.Rows[index].Tag=setting;grid.Rows[index].Cells[0].ReadOnly=!valid||!different;
            }
            root.Controls.Add(grid,0,1);
            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,12,0,0) };
            var cancel=new Button { Text="Cancel",Width=90,Height=32,DialogResult=DialogResult.Cancel };
            var apply=new Button { Text="Restore selected settings",Width=205,Height=32 };
            actions.Controls.Add(cancel);actions.Controls.Add(apply);root.Controls.Add(actions,0,2);CancelButton=cancel;
            Action update=()=>{apply.Enabled=grid.Rows.Cast<DataGridViewRow>().Any(row=>Convert.ToBoolean(row.Cells[0].Value));};
            grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};grid.CellValueChanged+=(s,e)=>update();update();
            apply.Click+=(s,e)=>{grid.EndEdit();SelectedAddresses=grid.Rows.Cast<DataGridViewRow>().Where(row=>Convert.ToBoolean(row.Cells[0].Value)).Select(row=>((Setting)row.Tag).Address).ToArray();if(SelectedAddresses.Length>0){DialogResult=DialogResult.OK;Close();}};
            Theme.Apply(this);
        }
    }
}
