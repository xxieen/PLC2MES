using System;
using System.Windows.Forms;

namespace PLC2MES.Utils
{
 public static class ArrayEditorDialog
 {
 // Shows a simple modal dialog to edit JSON array text. Returns the edited text or null if cancelled.
 public static string ShowDialog(IWin32Window owner, string initialJson)
 {
 using (var form = new Form())
 using (var tb = new TextBox())
 using (var btnOk = new Button())
 using (var btnCancel = new Button())
 {
 form.Text = "Edit Array";
 form.StartPosition = FormStartPosition.CenterParent;
 form.Width =600;
 form.Height =400; 
 form.FormBorderStyle = FormBorderStyle.FixedDialog;
 form.MinimizeBox = false;
 form.MaximizeBox = false;

 tb.Multiline = true;
 tb.ScrollBars = ScrollBars.Both;
 tb.WordWrap = false;
 tb.Dock = DockStyle.Top;
 tb.Height = form.ClientSize.Height -50;
 tb.Text = initialJson ?? string.Empty;

 btnOk.Text = "OK";
 btnOk.Width =100;
 btnOk.Left = form.ClientSize.Width -220;
 btnOk.Top = tb.Bottom +6;
 btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
 btnOk.DialogResult = DialogResult.OK;

 btnCancel.Text = "Cancel";
 btnCancel.Width =100;
 btnCancel.Left = form.ClientSize.Width -110;
 btnCancel.Top = tb.Bottom +6;
 btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
 btnCancel.DialogResult = DialogResult.Cancel;

 form.Controls.Add(tb);
 form.Controls.Add(btnOk);
 form.Controls.Add(btnCancel);

 form.AcceptButton = btnOk;
 form.CancelButton = btnCancel;

 var dr = form.ShowDialog(owner);
 if (dr == DialogResult.OK)
 {
 return tb.Text;
 }
 return null;
 }
 }
 }
}
