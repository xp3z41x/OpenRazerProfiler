using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenRazerProfiler.Interface
{
    // Cantos arredondados do Windows 11 em janelas de menu e listas. No Windows 10 o atributo não existe e nada muda.
    static class Dwm
    {
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWA_BORDER_COLOR = 34;
        const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // true = o Windows arredondou a janela e desenha a borda e a sombra dela.
        public static bool RoundCorners(IntPtr hwnd, Color border)
        {
            try
            {
                int corner = DWMWCP_ROUND;
                if (DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int)) != 0)
                    return false;
                int color = border.R | (border.G << 8) | (border.B << 16);
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref color, sizeof(int));
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }

    // Menu com a aparência do app: itens de 32 px com o texto no meio da altura, destaque arredondado e os
    // cantos do Windows 11. Herda de ToolStripDropDown, e não de ContextMenuStrip, porque o menu nativo
    // posiciona o texto por conta própria, troca o Padding e reserva à direita o lugar da seta de submenu.
    class ModernMenu : ToolStripDropDown
    {
        public ModernMenu()
        {
            Renderer = new MenuRenderer();
            Font = Theme.Base;
            LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow;
            Padding = new Padding(Theme.Px(this, 4));
        }

        // Reserva à esquerda a coluna do ✓ (listas de escolha).
        public bool ShowChecks { get; set; }

        internal bool RoundedByWindows { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RoundedByWindows = Dwm.RoundCorners(Handle, Theme.Border);
        }

        public MenuEntry Add(string text, EventHandler onClick, string shortcut = null)
        {
            var item = new MenuEntry(text, shortcut);
            if (onClick != null)
                item.Click += onClick;
            Items.Add(item);
            return item;
        }

        public void AddSeparator() => Items.Add(new MenuDivider());

        // Aberto pelo teclado, já destaca o primeiro item disponível, para as setas e o Enter.
        public void ShowFromKeyboard(Control control, Point location)
        {
            Show(control, location);
            Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Enabled && i.CanSelect)?.Select();
        }

        // Todos os itens com a largura do maior; o menu, com pelo menos a largura mínima pedida.
        protected override void OnOpening(CancelEventArgs e)
        {
            base.OnOpening(e);
            if (e.Cancel)
                return;
            int width = Math.Max(0, MinimumSize.Width - Padding.Horizontal);
            foreach (ToolStripItem item in Items)
                width = Math.Max(width, item.GetPreferredSize(Size.Empty).Width);
            foreach (ToolStripItem item in Items)
            {
                item.AutoSize = false;
                item.Size = new Size(width, item.GetPreferredSize(Size.Empty).Height);
            }
        }
    }

    // Item de menu desenhado pelo app: texto no meio da altura, atalho à direita e ✓ na coluna da esquerda.
    sealed class MenuEntry : ToolStripButton
    {
        public MenuEntry(string text, string shortcut) : base(text)
        {
            Shortcut = shortcut;
            AutoToolTip = false;
            DisplayStyle = ToolStripItemDisplayStyle.Text;
        }

        public string Shortcut { get; }

        int Px(float pixels) => Owner != null ? Theme.Px(Owner, pixels) : (int)Math.Round(pixels);
        int TextLeft => Px((Owner as ModernMenu)?.ShowChecks == true ? 32 : 12);

        public override Size GetPreferredSize(Size constrainingSize)
        {
            int width = TextLeft + TextRenderer.MeasureText(Text, Font, Size.Empty, Theme.TextFlags).Width + Px(12);
            if (!string.IsNullOrEmpty(Shortcut))
                width += Px(32) + TextRenderer.MeasureText(Shortcut, Font, Size.Empty, Theme.TextFlags).Width;
            return new Size(width, Px(32));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Owner != null ? Theme.Scale(Owner) : 1;
            Theme.Smooth(g);
            if (Selected && Enabled)
                Theme.FillAndStroke(g, new RectangleF(0, 0, Width, Height), 4 * s, Pressed ? Theme.Pressed : Theme.Hover, Color.Empty);
            if (Checked)
                Theme.DrawCheck(g, new PointF(Px(17), Height / 2f), s, Enabled ? Theme.Accent : Theme.DisabledText);
            int right = Width - Px(12);
            if (!string.IsNullOrEmpty(Shortcut))
            {
                int width = TextRenderer.MeasureText(g, Shortcut, Font, Size.Empty, Theme.TextFlags).Width;
                TextRenderer.DrawText(g, Shortcut, Font, new Rectangle(right - width, 0, width, Height),
                    Enabled ? Theme.MutedText : Theme.DisabledText, Theme.TextFlags | TextFormatFlags.VerticalCenter);
                right -= width + Px(16);
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(TextLeft, 0, Math.Max(0, right - TextLeft), Height),
                Enabled ? Theme.Text : Theme.DisabledText, Theme.TextFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new EntryAccessibleObject(this);

        sealed class EntryAccessibleObject : ToolStripItemAccessibleObject
        {
            readonly MenuEntry owner;

            public EntryAccessibleObject(MenuEntry owner) : base(owner) => this.owner = owner;

            public override AccessibleRole Role => AccessibleRole.MenuItem;
            public override AccessibleStates State => owner.Checked ? base.State | AccessibleStates.Checked : base.State;
        }
    }

    sealed class MenuDivider : ToolStripSeparator
    {
        public override Size GetPreferredSize(Size constrainingSize) => new Size(1, Owner != null ? Theme.Px(Owner, 9) : 9);

        protected override void OnPaint(PaintEventArgs e)
        {
            int y = Height / 2, inset = Owner != null ? Theme.Px(Owner, 8) : 8;
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawLine(pen, inset, y, Width - inset, y);
        }
    }

    // Os itens se desenham sozinhos; o renderizador só cuida do fundo e, sem os cantos do Windows 11, da borda.
    sealed class MenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(Theme.Surface))
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ModernMenu menu && menu.RoundedByWindows)
                return;
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    // Liga um ModernMenu ao clique direito de um controle, a Shift+F10 e à tecla Menu. Quem recebe o pedido do
    // Windows é um ContextMenuStrip vazio, que cancela a própria abertura e abre o nosso no lugar (numa caixa de
    // texto, isso também evita o menu nativo).
    static class ContextMenus
    {
        // keyboardLocation: onde abrir quando o pedido vem do teclado (o padrão é logo abaixo do controle).
        public static void Attach(Control control, ModernMenu menu, Func<Point> keyboardLocation = null)
        {
            var trigger = new ContextMenuStrip();
            trigger.Opening += (s, e) =>
            {
                e.Cancel = true;
                var mouse = control.PointToClient(Cursor.Position);
                if (control.ClientRectangle.Contains(mouse))
                    menu.Show(control, mouse);
                else
                    menu.ShowFromKeyboard(control, keyboardLocation?.Invoke() ?? new Point(0, control.Height));
            };
            control.ContextMenuStrip = trigger;
            control.Disposed += (s, e) =>
            {
                trigger.Dispose();
                menu.Dispose();
            };
        }
    }

    // Lista suspensa desenhada (substitui o ComboBox): mostra o item atual e abre um menu com ✓ no escolhido,
    // da largura do botão. Fechada, as setas trocam a escolha como no ComboBox; Espaço, Enter, F4 ou Alt+↓ abrem.
    sealed class DropDownButton : Control
    {
        readonly List<object> items = new List<object>();
        readonly ModernMenu menu = new ModernMenu { ShowChecks = true };
        int selected = -1;
        string emptyText = "";
        bool hover, suppressOpen;

        public DropDownButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Font = Theme.Base;
            Cursor = Cursors.Hand;
            Size = new Size(160, 32);
            menu.Closed += (s, e) =>
            {
                // Clicar no botão com o menu aberto só fecha o menu.
                if (e.CloseReason == ToolStripDropDownCloseReason.AppClicked && ClientRectangle.Contains(PointToClient(Cursor.Position)))
                    suppressOpen = true;
                Invalidate();
            };
        }

        public event EventHandler SelectedIndexChanged;

        // Troca os itens sem disparar SelectedIndexChanged; a escolha volta para nenhuma.
        public void SetItems(IEnumerable values)
        {
            items.Clear();
            foreach (var value in values)
                items.Add(value);
            selected = -1;
            Invalidate();
        }

        public int SelectedIndex
        {
            get => selected;
            set
            {
                if (value < -1 || value >= items.Count)
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (value == selected)
                    return;
                selected = value;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get => selected >= 0 ? items[selected] : null;
            set => SelectedIndex = value == null ? -1 : items.IndexOf(value);
        }

        // Texto mostrado quando nada está escolhido (ex.: "Varia por perfil").
        public string EmptyText
        {
            get => emptyText;
            set
            {
                emptyText = value ?? "";
                Invalidate();
            }
        }

        string DisplayText => selected >= 0 ? items[selected]?.ToString() ?? "" : emptyText;

        void BuildMenu()
        {
            foreach (var old in menu.Items.Cast<ToolStripItem>().ToList())
                old.Dispose();
            menu.Items.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                menu.Add(items[i]?.ToString() ?? "", (s, e) => SelectedIndex = index).Checked = i == selected;
            }
            menu.MinimumSize = new Size(Width, 0);
        }

        void OpenMenu(bool fromKeyboard)
        {
            if (items.Count == 0 || menu.Visible)
                return;
            BuildMenu();
            menu.Show(this, new Point(0, Height + Theme.Px(this, 4)));
            if (fromKeyboard)
                menu.Items[Math.Max(0, selected)].Select();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(Theme.BackgroundOf(this));
            Theme.Smooth(g);
            bool enabled = Enabled;
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            float radius = Theme.ControlRadius * s;
            var fill = !enabled ? Theme.Surface : menu.Visible ? Theme.Pressed : hover ? Theme.Hover : Theme.Surface;
            Theme.FillAndStroke(g, bounds, radius, fill, Theme.Border);
            int padding = Theme.Px(this, 12), arrow = Theme.Px(this, 32);
            var text = !enabled ? Theme.DisabledText : selected >= 0 ? Theme.Text : Theme.MutedText;
            TextRenderer.DrawText(g, DisplayText, Font, new Rectangle(padding, 0, Width - padding - arrow, Height), text,
                Theme.TextFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            Theme.DrawChevron(g, new PointF(Width - Theme.Px(this, 16), Height / 2f), s, enabled ? Theme.MutedText : Theme.DisabledText);
            if (Focused && ShowFocusCues)
                Theme.DrawFocusRing(g, bounds, radius, s, false);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            Focus();
            if (suppressOpen)
                suppressOpen = false;
            else
                OpenMenu(false);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            suppressOpen = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = false;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((e.KeyCode == Keys.Down && e.Alt) || e.KeyCode == Keys.F4 || e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                OpenMenu(true);
                e.Handled = true;
                return;
            }
            if (items.Count == 0)
                return;
            switch (e.KeyCode)
            {
                case Keys.Up:
                    SelectedIndex = Math.Max(0, selected - 1);
                    break;
                case Keys.Down:
                    SelectedIndex = Math.Min(items.Count - 1, selected + 1);
                    break;
                case Keys.Home:
                    SelectedIndex = 0;
                    break;
                case Keys.End:
                    SelectedIndex = items.Count - 1;
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                menu.Dispose();
            base.Dispose(disposing);
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new DropDownAccessibleObject(this);

        sealed class DropDownAccessibleObject : ControlAccessibleObject
        {
            readonly DropDownButton owner;

            public DropDownAccessibleObject(DropDownButton owner) : base(owner) => this.owner = owner;

            public override AccessibleRole Role => AccessibleRole.ComboBox;
            public override string DefaultAction => "Abrir";
            public override void DoDefaultAction() => owner.OpenMenu(true);

            public override string Value
            {
                get => owner.DisplayText;
                set { }
            }
        }
    }

    // Menu de clique direito das caixas de texto, no lugar do menu nativo do Windows.
    static class EditMenu
    {
        public static void Attach(TextBoxBase box)
        {
            var menu = new ModernMenu();
            var undo = menu.Add("Desfazer", (s, e) => box.Undo(), "Ctrl+Z");
            menu.AddSeparator();
            var cut = menu.Add("Recortar", (s, e) => box.Cut(), "Ctrl+X");
            var copy = menu.Add("Copiar", (s, e) => box.Copy(), "Ctrl+C");
            var paste = menu.Add("Colar", (s, e) => box.Paste(), "Ctrl+V");
            var delete = menu.Add("Excluir", (s, e) => box.SelectedText = "", "Del");
            menu.AddSeparator();
            var all = menu.Add("Selecionar tudo", (s, e) => box.SelectAll(), "Ctrl+A");
            menu.Opening += (s, e) =>
            {
                bool selection = box.SelectionLength > 0;
                undo.Enabled = box.CanUndo && !box.ReadOnly;
                cut.Enabled = delete.Enabled = selection && !box.ReadOnly;
                copy.Enabled = selection;
                paste.Enabled = !box.ReadOnly && ClipboardHasText();
                all.Enabled = box.TextLength > 0 && box.SelectionLength < box.TextLength;
            };
            ContextMenus.Attach(box, menu);
        }

        static bool ClipboardHasText()
        {
            try
            {
                return Clipboard.ContainsText();
            }
            catch (ExternalException)
            {
                return true;   // área de transferência ocupada por outro programa: deixa tentar
            }
        }
    }
}
