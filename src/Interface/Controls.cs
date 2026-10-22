using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenRazerProfiler.Interface
{
    enum ButtonKind
    {
        Primary,     // fundo verde: a ação principal
        Secondary,   // fundo branco com borda
        Ghost,       // só o texto, para ações discretas
    }

    // Botão desenhado pelo app. Herda de Button para manter DialogResult, AcceptButton, Espaço/Enter e acessibilidade.
    sealed class FlatButton : Button
    {
        readonly ButtonKind kind;
        ToolStripDropDown dropDownMenu;
        bool hover, pressed, suppressMenu;

        public FlatButton(ButtonKind kind)
        {
            this.kind = kind;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Font = Theme.Base;
            Cursor = Cursors.Hand;
            Margin = new Padding(6, 0, 0, 0);
        }

        // Menu aberto logo abaixo do botão, que ganha uma seta.
        public ToolStripDropDown DropDownMenu
        {
            get => dropDownMenu;
            set
            {
                dropDownMenu = value;
                if (value != null)
                    value.Closed += (s, e) =>
                    {
                        // Clicar no botão com o menu aberto só fecha o menu.
                        if (e.CloseReason == ToolStripDropDownCloseReason.AppClicked && ClientRectangle.Contains(PointToClient(Cursor.Position)))
                            suppressMenu = true;
                        Invalidate();
                    };
                Parent?.PerformLayout(this, nameof(DropDownMenu));
                Invalidate();
            }
        }

        int ArrowWidth => dropDownMenu != null ? Theme.Px(this, 16) : 0;

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(Text, Font, Size.Empty, Theme.TextFlags);
            int padding = Theme.Px(this, kind == ButtonKind.Ghost ? 10 : 14);
            return new Size(text.Width + 2 * padding + ArrowWidth, Theme.Px(this, kind == ButtonKind.Ghost ? 28 : 32));
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (dropDownMenu == null || suppressMenu)
                return;
            dropDownMenu.Show(this, new Point(0, Height + Theme.Px(this, 4)));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(Theme.BackgroundOf(this));
            Theme.Smooth(g);
            bool enabled = Enabled;
            bool down = enabled && (pressed || (dropDownMenu != null && dropDownMenu.Visible));
            Color fill, border = Color.Empty, text;
            switch (kind)
            {
                case ButtonKind.Primary:
                    fill = !enabled ? Theme.DisabledFill : down ? Theme.AccentPressed : hover ? Theme.AccentHover : Theme.Accent;
                    text = enabled ? Color.White : Theme.DisabledText;
                    break;
                case ButtonKind.Secondary:
                    fill = down ? Theme.Pressed : hover && enabled ? Theme.Hover : Theme.Surface;
                    border = Theme.Border;
                    text = enabled ? Theme.Text : Theme.DisabledText;
                    break;
                default:
                    fill = down ? Theme.Pressed : hover && enabled ? Theme.Hover : Color.Empty;
                    text = enabled ? Theme.Accent : Theme.DisabledText;
                    break;
            }
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            float radius = Theme.ControlRadius * s;
            Theme.FillAndStroke(g, bounds, radius, fill, border);

            // texto e seta centralizados juntos; a seta tem 8 px e fica 7 px depois do texto
            float end = Theme.DrawCenteredText(g, Text, Font, ClientRectangle, text, dropDownMenu != null ? 15 * s : 0);
            if (dropDownMenu != null)
                Theme.DrawChevron(g, new PointF(end + 11 * s, Height / 2f), s, text);
            if (Focused && ShowFocusCues)
                Theme.DrawFocusRing(g, bounds, radius, s, kind == ButtonKind.Primary && enabled);
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
            hover = pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);   // o Click sai daqui
            suppressMenu = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space)
            {
                pressed = true;
                Invalidate();
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnKeyUp(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            pressed = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }
    }

    // Campo de texto com a moldura arredondada do app; a caixa do Windows fica dentro, sem borda.
    sealed class TextField : Control
    {
        public readonly TextBox Box = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text, Font = Theme.Base };

        public TextField()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Cursor = Cursors.IBeam;
            Size = new Size(240, 32);
            Box.Enter += (s, e) => Invalidate();
            Box.Leave += (s, e) => Invalidate();
            Controls.Add(Box);
            EditMenu.Attach(Box);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int padding = Theme.Px(this, 12);
            Box.SetBounds(padding, (Height - Box.Height) / 2, Math.Max(10, Width - 2 * padding), Box.Height);
        }

        protected override void OnPaint(PaintEventArgs e) => Theme.DrawFieldFrame(e.Graphics, this, Box.Focused);

        // Clicar na moldura também põe o cursor no texto.
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Box.Focus();
        }
    }

    // Opção em forma de etiqueta (os efeitos de luz). Herda de RadioButton para manter o grupo e as setas.
    sealed class ChipRadio : RadioButton
    {
        bool hover, pressed;

        public ChipRadio()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            AutoSize = true;
            Font = Theme.Base;
            Cursor = Cursors.Hand;
        }

        // A largura não depende da seleção: marcar uma opção não empurra as vizinhas.
        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(Text, Font, Size.Empty, Theme.TextFlags);
            return new Size(text.Width + Theme.Px(this, 24), Theme.Px(this, 30));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(Theme.BackgroundOf(this));
            Theme.Smooth(g);
            bool on = Checked, enabled = Enabled;
            Color fill = on ? (enabled ? Theme.AccentSoft : Theme.DisabledFill)
                : !enabled ? Theme.Surface : pressed ? Theme.Pressed : hover ? Theme.Hover : Theme.Surface;
            Color border = on && enabled ? Theme.Accent : Theme.Border;
            Color text = !enabled ? Theme.DisabledText : on ? Theme.AccentText : Theme.Text;
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            float radius = Theme.ControlRadius * s;
            Theme.FillAndStroke(g, bounds, radius, fill, border);
            Theme.DrawCenteredText(g, Text, Font, ClientRectangle, text);
            if (Focused && ShowFocusCues)
                Theme.DrawFocusRing(g, bounds, radius, s, false);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
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
            hover = pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            pressed = e.Button == MouseButtons.Left;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }
    }

    // Modificador de atalho (Ctrl, Shift…) desenhado como uma tecla, acesa quando marcada.
    sealed class KeycapToggle : CheckBox
    {
        bool hover, pressed;

        public KeycapToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            AutoSize = true;
            Font = Theme.Base;
            Cursor = Cursors.Hand;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(Text, Font, Size.Empty, Theme.TextFlags);
            return new Size(Math.Max(text.Width + Theme.Px(this, 22), Theme.Px(this, 44)), Theme.Px(this, 30));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(Theme.BackgroundOf(this));
            Theme.Smooth(g);
            bool on = Checked, enabled = Enabled;
            Color edge = !enabled ? Theme.Border : on ? Theme.Accent : Theme.KeyEdge;
            Color face = !enabled ? Theme.DisabledFill : on ? Theme.AccentSoft : pressed ? Theme.Pressed : hover ? Theme.Hover : Theme.KeyFace;
            Color text = !enabled ? Theme.DisabledText : on ? Theme.AccentText : Theme.Text;

            // A borda mais grossa embaixo imita a lateral da tecla; marcada (pressionada), ela afina.
            float radius = Theme.ControlRadius * s, lip = (on || pressed ? 1 : 2) * s;
            Theme.FillAndStroke(g, new RectangleF(0, 0, Width, Height), radius, edge, Color.Empty);
            var faceRect = new RectangleF(s, s, Width - 2 * s, Height - s - lip);
            Theme.FillAndStroke(g, faceRect, radius - s, face, Color.Empty);
            Theme.DrawCenteredText(g, Text, Font, Rectangle.Round(faceRect), text);
            if (Focused && ShowFocusCues)
                Theme.DrawFocusRing(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), radius, s, false);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
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
            hover = pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            pressed = e.Button == MouseButtons.Left;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }
    }

    // Escolha entre poucas opções numa faixa só (substitui rádios e listas curtas). SelectedIndex = -1: nenhuma.
    sealed class SegmentedControl : Control
    {
        string[] items = Array.Empty<string>();
        int selected = -1, hover = -1, pressed = -1;

        public SegmentedControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            AutoSize = true;
            Font = Theme.Base;
            Cursor = Cursors.Hand;
        }

        public event EventHandler SelectedIndexChanged;

        public string[] Items
        {
            get => items;
            set
            {
                items = value ?? Array.Empty<string>();
                if (selected >= items.Length)
                    selected = -1;
                ItemsChanged();
            }
        }

        public void SetItemText(int index, string text)
        {
            if (items[index] == text)
                return;
            items[index] = text;
            ItemsChanged();
        }

        void ItemsChanged()
        {
            if (AutoSize)
                Parent?.PerformLayout(this, nameof(Items));
            Invalidate();
        }

        public int SelectedIndex
        {
            get => selected;
            set
            {
                if (value < -1 || value >= items.Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (value == selected)
                    return;
                selected = value;
                Invalidate();
                if (selected >= 0)
                {
                    AccessibilityNotifyClients(AccessibleEvents.Selection, selected);
                    if (Focused)
                        AccessibilityNotifyClients(AccessibleEvents.Focus, selected);
                }
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        float Inset => 3 * Theme.Scale(this);
        float Gap => 2 * Theme.Scale(this);

        float SegmentWidth(int index) =>
            TextRenderer.MeasureText(items[index], Font, Size.Empty, Theme.TextFlags).Width + 24 * Theme.Scale(this);

        public override Size GetPreferredSize(Size proposedSize)
        {
            float width = 2 * Inset + Math.Max(0, items.Length - 1) * Gap;
            for (int i = 0; i < items.Length; i++)
                width += SegmentWidth(i);
            return new Size((int)Math.Ceiling(width), Theme.Px(this, 32));
        }

        RectangleF SegmentBounds(int index)
        {
            float x = Inset;
            for (int i = 0; i < index; i++)
                x += SegmentWidth(i) + Gap;
            return new RectangleF(x, Inset, SegmentWidth(index), Height - 2 * Inset);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < items.Length; i++)
                if (SegmentBounds(i).Contains(p))
                    return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(Theme.BackgroundOf(this));
            Theme.Smooth(g);
            bool enabled = Enabled;
            float radius = (Theme.ControlRadius + 1) * s;
            var bounds = new RectangleF(0, 0, Width, Height);
            Theme.FillAndStroke(g, bounds, radius, enabled ? Theme.Hover : Theme.DisabledFill, Color.Empty);
            for (int i = 0; i < items.Length; i++)
            {
                var r = SegmentBounds(i);
                Color fill = Color.Empty, border = Color.Empty, text;
                if (i == selected)
                {
                    fill = enabled ? Theme.AccentSoft : Theme.Surface;
                    border = enabled ? Theme.Accent : Theme.Border;
                    text = enabled ? Theme.AccentText : Theme.DisabledText;
                }
                else
                {
                    if (enabled && i == pressed)
                        fill = Theme.Border;
                    else if (enabled && i == hover)
                        fill = Theme.Pressed;
                    text = !enabled ? Theme.DisabledText : i == hover ? Theme.Text : Theme.MutedText;
                }
                Theme.FillAndStroke(g, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), radius - Inset, fill, border);
                Theme.DrawCenteredText(g, items[i], Font, Rectangle.Round(r), text);
            }
            if (Focused && ShowFocusCues)
                Theme.DrawFocusRing(g, bounds, radius, s, false);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = HitTest(e.Location);
            if (index != hover)
            {
                hover = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = pressed = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            Focus();
            pressed = HitTest(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int index = HitTest(e.Location);
            if (e.Button == MouseButtons.Left && index >= 0 && index == pressed)
                SelectedIndex = index;
            pressed = -1;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        // Como num grupo de rádios, as setas já trocam a escolha.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (items.Length == 0)
                return;
            int next;
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Up:
                    next = Math.Max(0, selected - 1);
                    break;
                case Keys.Right:
                case Keys.Down:
                    next = Math.Min(items.Length - 1, selected + 1);
                    break;
                case Keys.Home:
                    next = 0;
                    break;
                case Keys.End:
                    next = items.Length - 1;
                    break;
                default:
                    return;
            }
            e.Handled = true;
            SelectedIndex = next;
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

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ItemsChanged();
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new SegmentedAccessibleObject(this);

        // Para o Narrador: um grupo com um botão de opção por segmento.
        sealed class SegmentedAccessibleObject : ControlAccessibleObject
        {
            readonly SegmentedControl owner;

            public SegmentedAccessibleObject(SegmentedControl owner) : base(owner) => this.owner = owner;

            public override AccessibleRole Role => AccessibleRole.Grouping;
            public override int GetChildCount() => owner.items.Length;

            public override AccessibleObject GetChild(int index) =>
                index >= 0 && index < owner.items.Length ? new SegmentAccessibleObject(owner, index) : null;

            public override AccessibleObject GetFocused() =>
                owner.Focused && owner.selected >= 0 ? GetChild(owner.selected) : base.GetFocused();

            public override AccessibleObject GetSelected() => owner.selected >= 0 ? GetChild(owner.selected) : null;
        }

        sealed class SegmentAccessibleObject : AccessibleObject
        {
            readonly SegmentedControl owner;
            readonly int index;

            public SegmentAccessibleObject(SegmentedControl owner, int index)
            {
                this.owner = owner;
                this.index = index;
            }

            public override string Name => owner.items[index];
            public override AccessibleRole Role => AccessibleRole.RadioButton;
            public override AccessibleObject Parent => owner.AccessibilityObject;
            public override Rectangle Bounds => owner.RectangleToScreen(Rectangle.Round(owner.SegmentBounds(index)));
            public override string DefaultAction => "Selecionar";
            public override void DoDefaultAction() => owner.SelectedIndex = index;

            public override AccessibleStates State
            {
                get
                {
                    var state = AccessibleStates.Focusable | AccessibleStates.Selectable;
                    if (index == owner.selected)
                        state |= AccessibleStates.Checked | AccessibleStates.Selected | (owner.Focused ? AccessibleStates.Focused : 0);
                    if (!owner.Enabled)
                        state |= AccessibleStates.Unavailable;
                    return state;
                }
            }
        }
    }

    // Barra deslizante: trilho fino preenchido até o valor e polegar redondo (substitui o TrackBar).
    // Como no TrackBar, ValueChanged também dispara quando o valor muda pelo código.
    sealed class Slider : Control
    {
        int minimum, maximum = 10, current;
        bool hover, dragging;

        public Slider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(240, 24);
        }

        public event EventHandler ValueChanged;

        public int SmallChange { get; set; } = 1;
        public int LargeChange { get; set; } = 5;

        public int Minimum
        {
            get => minimum;
            set
            {
                minimum = value;
                if (maximum < minimum)
                    maximum = minimum;
                SetValue(current);
                Invalidate();
            }
        }

        public int Maximum
        {
            get => maximum;
            set
            {
                maximum = value;
                if (minimum > maximum)
                    minimum = maximum;
                SetValue(current);
                Invalidate();
            }
        }

        public int Value
        {
            get => current;
            set => SetValue(value);
        }

        void SetValue(int value)
        {
            value = Math.Max(minimum, Math.Min(maximum, value));
            if (value == current)
                return;
            current = value;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        // O trilho ocupa a largura toda; o centro do polegar anda de um raio até a largura menos um raio.
        float ThumbRadius => 9 * Theme.Scale(this);

        float CenterOf(int value)
        {
            float r = ThumbRadius;
            return maximum == minimum ? r : r + (Width - 2 * r) * (value - minimum) / (maximum - minimum);
        }

        int ValueAt(int x)
        {
            float r = ThumbRadius;
            float t = Math.Max(0, Math.Min(1, (x - r) / Math.Max(1, Width - 2 * r)));
            return minimum + (int)Math.Round(t * (maximum - minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(Theme.BackgroundOf(this));
            Theme.Smooth(g);
            bool enabled = Enabled, focus = Focused && ShowFocusCues;
            float r = ThumbRadius, cy = Height / 2f, cx = CenterOf(current), track = 4 * s;
            Theme.FillAndStroke(g, new RectangleF(0, cy - track / 2, Width, track), track / 2, enabled ? Theme.Border : Theme.DisabledFill, Color.Empty);
            Theme.FillAndStroke(g, new RectangleF(0, cy - track / 2, cx, track), track / 2, enabled ? Theme.Accent : Theme.Disabled, Color.Empty);

            var thumb = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
            using (var brush = new SolidBrush(Theme.Surface))
                g.FillEllipse(brush, thumb);
            float ring = focus ? 2 * s : 1;
            using (var pen = new Pen(focus ? Theme.Text : enabled ? Theme.KeyEdge : Theme.Border, ring))
                g.DrawEllipse(pen, RectangleF.Inflate(thumb, -ring / 2, -ring / 2));
            float dot = (dragging ? 7 : hover ? 10 : 8) * s;
            using (var brush = new SolidBrush(enabled ? Theme.Accent : Theme.Disabled))
                g.FillEllipse(brush, cx - dot / 2, cy - dot / 2, dot, dot);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            Focus();
            dragging = true;
            Capture = true;
            Value = ValueAt(e.X);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
                Value = ValueAt(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging)
                return;
            dragging = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (dragging && !Capture)
            {
                dragging = false;
                Invalidate();
            }
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

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e is HandledMouseEventArgs handled)
                handled.Handled = true;
            Value += Math.Sign(e.Delta) * SmallChange;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Right:
                case Keys.Up:
                    Value += SmallChange;
                    break;
                case Keys.Left:
                case Keys.Down:
                    Value -= SmallChange;
                    break;
                case Keys.PageUp:
                    Value += LargeChange;
                    break;
                case Keys.PageDown:
                    Value -= LargeChange;
                    break;
                case Keys.Home:
                    Value = minimum;
                    break;
                case Keys.End:
                    Value = maximum;
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

        protected override AccessibleObject CreateAccessibilityInstance() => new SliderAccessibleObject(this);

        sealed class SliderAccessibleObject : ControlAccessibleObject
        {
            readonly Slider owner;

            public SliderAccessibleObject(Slider owner) : base(owner) => this.owner = owner;

            public override AccessibleRole Role => AccessibleRole.Slider;

            public override string Value
            {
                get => owner.Value.ToString();
                set
                {
                    if (int.TryParse(value, out int v))
                        owner.Value = v;
                }
            }
        }
    }
}
