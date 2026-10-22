using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OpenRazerProfiler.Interface
{
    // Campo com busca para listas longas (a tecla de destino). Digitar filtra a lista; a escolha só vale ao
    // confirmar (Enter, clique na lista ou sair do campo), para não escolher "C" no meio de "Ctrl".
    sealed class SearchPicker : Control, IMessageFilter
    {
        const int WM_LBUTTONDOWN = 0x201, WM_RBUTTONDOWN = 0x204, WM_MBUTTONDOWN = 0x207;
        const int WM_NCLBUTTONDOWN = 0xA1, WM_NCRBUTTONDOWN = 0xA4;

        readonly TextBox box;
        readonly PickerPopup popup = new PickerPopup();
        readonly List<object> items = new List<object>();
        Func<object, string> detail = item => null;
        object selected;
        Form ownerForm;
        bool settingText, typed;

        public SearchPicker()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Font = Theme.Base;
            Cursor = Cursors.IBeam;
            Size = new Size(240, 32);

            box = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text, Font = Theme.Base };
            box.TextChanged += (s, e) => OnBoxTextChanged();
            box.KeyDown += OnBoxKeyDown;
            // Ao entrar no campo, seleciona o texto para que digitar substitua a escolha atual.
            box.Enter += (s, e) =>
            {
                BeginInvoke(new Action(box.SelectAll));
                Invalidate();
            };
            box.Leave += (s, e) =>
            {
                CommitOnLeave();
                Invalidate();
            };
            box.MouseWheel += (s, e) =>
            {
                if (popup.Visible)
                    popup.Rows.ScrollBy(e.Delta);
            };
            Controls.Add(box);
            EditMenu.Attach(box);

            popup.Rows.ItemChosen += item =>
            {
                SelectedItem = item;
                box.SelectAll();
            };
        }

        public event EventHandler SelectedItemChanged;

        // Nome lido pelo Narrador para o campo.
        public string FieldName
        {
            get => box.AccessibleName;
            set => box.AccessibleName = value;
        }

        // detailOf: texto discreto à direita de cada item (ex.: o grupo da tecla).
        public void SetItems(IEnumerable values, Func<object, string> detailOf = null)
        {
            items.Clear();
            foreach (var value in values)
                items.Add(value);
            detail = detailOf ?? (item => null);
        }

        // Pelo código ou pela escolha do usuário; SelectedItemChanged só dispara quando muda.
        public object SelectedItem
        {
            get => selected;
            set
            {
                ClosePopup();
                typed = false;
                SetBoxText(value?.ToString() ?? "");
                if (Equals(value, selected))
                    return;
                selected = value;
                SelectedItemChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        void SetBoxText(string text)
        {
            settingText = true;
            box.Text = text;
            settingText = false;
        }

        void OnBoxTextChanged()
        {
            if (settingText)
                return;
            typed = true;
            OpenPopup(showAll: false);
        }

        void OnBoxKeyDown(object sender, KeyEventArgs e)
        {
            var rows = popup.Rows;
            switch (e.KeyCode)
            {
                case Keys.Down when e.Alt:
                case Keys.F4:
                    if (popup.Visible)
                        ClosePopup();
                    else
                        OpenPopup(showAll: !typed);
                    break;
                case Keys.Down:
                case Keys.Up:
                case Keys.PageDown:
                case Keys.PageUp:
                    if (!popup.Visible)
                    {
                        OpenPopup(showAll: !typed);
                        break;
                    }
                    rows.MoveHighlight(e.KeyCode == Keys.Down ? 1 : e.KeyCode == Keys.Up ? -1
                        : e.KeyCode == Keys.PageDown ? rows.VisibleRows : -rows.VisibleRows);
                    // O campo mostra o item destacado, como no ComboBox (e o Narrador lê o texto).
                    if (rows.HighlightedItem != null)
                    {
                        SetBoxText(rows.HighlightedItem.ToString());
                        box.SelectAll();
                    }
                    break;
                case Keys.Enter:
                    if (popup.Visible && rows.HighlightedItem != null)
                        SelectedItem = rows.HighlightedItem;
                    else
                        CommitExact();
                    box.SelectAll();
                    break;
                case Keys.Escape:
                    if (!popup.Visible && !typed)
                        return;
                    Revert();
                    break;
                default:
                    return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        void CommitOnLeave()
        {
            if (typed && popup.Visible && popup.Rows.HighlightedItem != null)
                SelectedItem = popup.Rows.HighlightedItem;
            else if (typed)
                CommitExact();
            ClosePopup();
        }

        // Texto igual ao nome de um item: escolhe; senão, volta ao que estava.
        void CommitExact()
        {
            var text = box.Text.Trim();
            var match = items.FirstOrDefault(i => string.Equals(i.ToString(), text, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                SelectedItem = match;
            else
                Revert();
        }

        void Revert()
        {
            ClosePopup();
            typed = false;
            SetBoxText(selected?.ToString() ?? "");
            box.SelectAll();
        }

        // Começo do nome primeiro, depois começo de palavra, depois qualquer trecho e, por último, o grupo.
        List<object> Filter(string text)
        {
            string query = Fold(text);
            if (query.Length == 0)
                return items.ToList();
            var starts = new List<object>();
            var words = new List<object>();
            var contains = new List<object>();
            var groups = new List<object>();
            foreach (var item in items)
            {
                string name = Fold(item.ToString());
                if (name.StartsWith(query, StringComparison.Ordinal))
                    starts.Add(item);
                else if (name.Contains(" " + query) || name.Contains("(" + query))
                    words.Add(item);
                else if (name.Contains(query))
                    contains.Add(item);
                else if (Fold(detail(item)).StartsWith(query, StringComparison.Ordinal))
                    groups.Add(item);
            }
            return starts.Concat(words).Concat(contains).Concat(groups).ToList();
        }

        // Minúsculas e sem acentos: "espaco" acha "Espaço".
        static string Fold(string text)
        {
            var decomposed = (text ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var folded = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    folded.Append(c);
            return folded.ToString();
        }

        void OpenPopup(bool showAll)
        {
            if (!Enabled || !Visible)
                return;
            var list = showAll ? items.ToList() : Filter(box.Text);
            popup.Rows.SetItems(list, detail, selected);
            Place(list.Count);
            popup.Rows.Highlighted = showAll ? list.IndexOf(selected) : list.Count > 0 ? 0 : -1;
            if (popup.Visible)
                return;
            if (ownerForm == null)
            {
                ownerForm = FindForm();
                if (ownerForm != null)
                {
                    popup.Owner = ownerForm;
                    ownerForm.Deactivate += CloseFromOwner;
                    ownerForm.Move += CloseFromOwner;
                    ownerForm.Resize += CloseFromOwner;
                }
            }
            popup.Show();
            Application.AddMessageFilter(this);
            Invalidate();
        }

        void ClosePopup()
        {
            if (!popup.Visible)
                return;
            popup.Hide();
            Application.RemoveMessageFilter(this);
            Invalidate();
        }

        void CloseFromOwner(object sender, EventArgs e) => ClosePopup();

        // Abaixo do campo; se não couber na tela, acima.
        void Place(int count)
        {
            int inset = Theme.Px(this, 4), gap = Theme.Px(this, 4);
            int height = Math.Max(1, Math.Min(10, count)) * popup.Rows.ItemHeight + 2 * inset;
            int width = Math.Max(Width, Theme.Px(this, 300));
            var area = Screen.FromControl(this).WorkingArea;
            var below = PointToScreen(new Point(0, Height + gap));
            int y = below.Y + height <= area.Bottom ? below.Y : PointToScreen(Point.Empty).Y - gap - height;
            popup.Padding = new Padding(inset);
            popup.Bounds = new Rectangle(Math.Min(below.X, area.Right - width), y, width, height);
        }

        // Clique fora do campo e da lista fecha a lista.
        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_LBUTTONDOWN:
                case WM_RBUTTONDOWN:
                case WM_MBUTTONDOWN:
                case WM_NCLBUTTONDOWN:
                case WM_NCRBUTTONDOWN:
                    var target = FromHandle(m.HWnd);
                    if (!IsWithin(target, this) && !IsWithin(target, popup))
                        ClosePopup();
                    break;
            }
            return false;
        }

        static bool IsWithin(Control control, Control ancestor)
        {
            for (; control != null; control = control.Parent)
                if (control == ancestor)
                    return true;
            return false;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (box == null)
                return;   // o construtor ainda não criou o campo
            int padding = Theme.Px(this, 12), arrow = Theme.Px(this, 32);
            box.SetBounds(padding, (Height - box.Height) / 2, Math.Max(10, Width - padding - arrow), box.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.DrawFieldFrame(e.Graphics, this, box.Focused);
            Theme.DrawChevron(e.Graphics, new PointF(Width - Theme.Px(this, 16), Height / 2f), Theme.Scale(this), Enabled ? Theme.MutedText : Theme.DisabledText);
        }

        // Clicar na seta abre ou fecha a lista completa; em qualquer ponto, o foco vai para o texto.
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            box.Focus();
            if (e.X < Width - Theme.Px(this, 32))
                return;
            if (popup.Visible)
                ClosePopup();
            else
                OpenPopup(showAll: !typed);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible)
                ClosePopup();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled)
                ClosePopup();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClosePopup();
                if (ownerForm != null)
                {
                    ownerForm.Deactivate -= CloseFromOwner;
                    ownerForm.Move -= CloseFromOwner;
                    ownerForm.Resize -= CloseFromOwner;
                }
                popup.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Janela da lista: não ativa (o foco fica no campo de texto), sem ícone na barra de tarefas e com os cantos do Windows 11.
    sealed class PickerPopup : Form
    {
        const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x80, CS_DROPSHADOW = 0x20000;
        const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;

        public readonly PickerList Rows = new PickerList { Dock = DockStyle.Fill };

        public PickerPopup()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Surface;
            Controls.Add(Rows);
        }

        internal bool RoundedByWindows { get; private set; }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RoundedByWindows = Dwm.RoundCorners(Handle, Theme.Border);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!RoundedByWindows)
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    // Itens da busca, desenhados. Não recebe foco: as setas chegam pelo campo de texto.
    sealed class PickerList : Control
    {
        List<object> items = new List<object>();
        Func<object, string> detail = item => null;
        object current;
        int top, highlighted = -1, dragStartY = -1, dragStartTop;

        public PickerList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Font = Theme.Base;
        }

        public event Action<object> ItemChosen;

        public int ItemHeight => Theme.Px(this, 32);
        public int VisibleRows => Math.Max(1, Height / ItemHeight);
        public object HighlightedItem => highlighted >= 0 && highlighted < items.Count ? items[highlighted] : null;
        bool Scrolls => items.Count > VisibleRows;
        int ScrollBarWidth => Scrolls ? Theme.Px(this, 10) : 0;

        // current: o item escolhido hoje, marcado com ✓.
        public void SetItems(List<object> list, Func<object, string> detailOf, object currentItem)
        {
            items = list;
            detail = detailOf;
            current = currentItem;
            top = 0;
            highlighted = -1;
            Invalidate();
        }

        public int Highlighted
        {
            get => highlighted;
            set
            {
                highlighted = value;
                if (value >= 0 && value < top)
                    top = value;
                else if (value >= top + VisibleRows)
                    top = value - VisibleRows + 1;
                ClampTop();
                Invalidate();
            }
        }

        public void MoveHighlight(int step)
        {
            if (items.Count == 0)
                return;
            int from = highlighted >= 0 ? highlighted : step > 0 ? -1 : items.Count;
            Highlighted = Math.Max(0, Math.Min(items.Count - 1, from + step));
        }

        public void ScrollBy(int wheelDelta)
        {
            top -= Math.Sign(wheelDelta) * 3;
            ClampTop();
            Invalidate();
        }

        void ClampTop() => top = Math.Max(0, Math.Min(top, items.Count - VisibleRows));

        RectangleF Thumb()
        {
            float s = Theme.Scale(this);
            float length = Math.Max(24 * s, Height * VisibleRows / (float)items.Count);
            float y = (Height - length) * top / Math.Max(1, items.Count - VisibleRows);
            return new RectangleF(Width - 6 * s, y, 4 * s, length);
        }

        int IndexAt(Point p)
        {
            if (p.X >= Width - ScrollBarWidth || p.Y < 0)
                return -1;
            int row = p.Y / ItemHeight, index = top + row;
            return row < VisibleRows && index < items.Count ? index : -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(Theme.Surface);
            Theme.Smooth(g);
            // mesmas medidas dos itens de menu com ✓ (MenuEntry)
            int h = ItemHeight, check = Theme.Px(this, 32), right = Width - ScrollBarWidth;
            if (items.Count == 0)
                TextRenderer.DrawText(g, "Nada encontrado.", Font, new Rectangle(check, 0, right - check, h), Theme.MutedText,
                    Theme.TextFlags | TextFormatFlags.VerticalCenter);
            for (int row = 0; row < VisibleRows && top + row < items.Count; row++)
            {
                int i = top + row;
                var r = new Rectangle(0, row * h, right, h);
                if (i == highlighted)
                    Theme.FillAndStroke(g, r, 4 * s, Theme.Hover, Color.Empty);
                if (Equals(items[i], current))
                    Theme.DrawCheck(g, new PointF(Theme.Px(this, 17), r.Y + h / 2f), s, Theme.Accent);
                int textRight = r.Right - Theme.Px(this, 12);
                string group = detail(items[i]);
                if (!string.IsNullOrEmpty(group))
                {
                    int width = TextRenderer.MeasureText(g, group, Theme.Small, Size.Empty, Theme.TextFlags).Width;
                    TextRenderer.DrawText(g, group, Theme.Small, new Rectangle(textRight - width, r.Y, width, h), Theme.MutedText,
                        Theme.TextFlags | TextFormatFlags.VerticalCenter);
                    textRight -= width + Theme.Px(this, 12);
                }
                TextRenderer.DrawText(g, items[i].ToString(), Font, new Rectangle(check, r.Y, Math.Max(0, textRight - check), h), Theme.Text,
                    Theme.TextFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            if (Scrolls)
                Theme.FillAndStroke(g, Thumb(), 2 * s, Theme.Disabled, Color.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragStartY >= 0)
            {
                float travel = Height - Thumb().Height;
                top = dragStartTop + (int)Math.Round((e.Y - dragStartY) * (items.Count - VisibleRows) / Math.Max(1f, travel));
                ClampTop();
                Invalidate();
                return;
            }
            int index = IndexAt(e.Location);
            if (index >= 0 && index != highlighted)
            {
                highlighted = index;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            if (Scrolls && e.X >= Width - ScrollBarWidth)
            {
                var thumb = Thumb();
                if (e.Y >= thumb.Top && e.Y <= thumb.Bottom)
                {
                    dragStartY = e.Y;
                    dragStartTop = top;
                    Capture = true;
                }
                else
                {
                    top += e.Y < thumb.Top ? -VisibleRows : VisibleRows;   // fora do polegar: uma página
                    ClampTop();
                    Invalidate();
                }
                return;
            }
            int index = IndexAt(e.Location);
            if (index >= 0)
                ItemChosen?.Invoke(items[index]);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragStartY < 0)
                return;
            dragStartY = -1;
            Capture = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollBy(e.Delta);
        }
    }
}
