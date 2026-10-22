using System.Drawing;
using System.Windows.Forms;
using OpenRazerProfiler.Protocolo;

namespace OpenRazerProfiler.Interface
{
    // Pede o nome de um perfil.
    sealed class NameDialog : Form
    {
        readonly TextBox box;

        NameDialog(string title, string initial)
        {
            Text = title;
            Font = Theme.Base;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(16);
            BackColor = Theme.Surface;

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
            layout.Controls.Add(Theme.Label("Nome do perfil", Theme.Base, Theme.MutedText));
            var field = new TextField { Width = 300, Margin = new Padding(0, 6, 0, 14) };
            box = field.Box;
            box.Text = initial;
            box.MaxLength = ProfileMetadata.MaxNameLength;
            layout.Controls.Add(field);
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0) };
            var ok = Theme.PrimaryButton("OK");
            var cancel = Theme.SecondaryButton("Cancelar");
            ok.DialogResult = DialogResult.OK;
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons);
            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = cancel;
            box.TextChanged += (s, e) => ok.Enabled = box.Text.Trim().Length > 0;
        }

        public static string Ask(IWin32Window owner, string title, string initial)
        {
            using (var d = new NameDialog(title, initial))
            {
                d.box.SelectAll();
                return d.ShowDialog(owner) == DialogResult.OK ? d.box.Text.Trim() : null;
            }
        }
    }
}
