using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenRazerProfiler.Interface
{
    sealed class ProfileSlotItem
    {
        public byte Slot;
        public string Name;           // null = slot vazio
        public bool Active;           // perfil ativo (já contando uma troca ainda não gravada)
        public bool Pending;          // tem alterações não gravadas
    }

    // Lista dos slots de perfil, desenhada à mão. Seleção e hover em pílula arredondada, como nos menus.
    sealed class ProfileList : ListBox
    {
        int hover = -1;

        public ProfileList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            BackColor = Theme.Surface;
            Font = Theme.Base;
            ItemHeight = 46;
        }

        // Linha selecionada, nas coordenadas da lista (para abrir o menu pelo teclado).
        public Rectangle SelectedRowBounds => SelectedIndex >= 0 ? GetItemRectangle(SelectedIndex) : Rectangle.Empty;

        // O clique direito também seleciona a linha, para o menu agir sobre ela.
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = IndexFromPoint(e.Location);
                if (index >= 0)
                    SelectedIndex = index;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SetHover(IndexFromPoint(e.Location));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHover(-1);
        }

        void SetHover(int index)
        {
            if (index == hover)
                return;
            if (hover >= 0 && hover < Items.Count)
                Invalidate(GetItemRectangle(hover));
            hover = index;
            if (hover >= 0 && hover < Items.Count)
                Invalidate(GetItemRectangle(hover));
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count)
                return;
            var item = (ProfileSlotItem)Items[e.Index];
            var g = e.Graphics;
            float s = Theme.Scale(this);
            var r = e.Bounds;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var back = new SolidBrush(Theme.Surface))
                g.FillRectangle(back, r);
            Theme.Smooth(g);

            // pílula da linha, com o indicador verde à esquerda quando selecionada
            var pill = new RectangleF(r.X + 4 * s, r.Y + 2 * s, r.Width - 8 * s, r.Height - 4 * s);
            float radius = Theme.ControlRadius * s;
            if (selected)
            {
                Theme.FillAndStroke(g, pill, radius, Theme.AccentSoft, Color.Empty);
                Theme.FillAndStroke(g, new RectangleF(pill.X, r.Y + (r.Height - 16 * s) / 2, 3 * s, 16 * s), 1.5f * s, Theme.Accent, Color.Empty);
            }
            else if (e.Index == hover && Enabled)
            {
                Theme.FillAndStroke(g, pill, radius, Theme.Hover, Color.Empty);
            }
            if ((e.State & DrawItemState.Focus) != 0 && (e.State & DrawItemState.NoFocusRect) == 0)
                Theme.DrawFocusRing(g, pill, radius, s, false);

            // número do slot num círculo: cheio no perfil ativo, tracejado no slot vazio
            float d = 26 * s;
            var circle = new RectangleF(pill.X + 10 * s, r.Y + (r.Height - d) / 2, d, d);
            if (item.Active)
            {
                using (var fill = new SolidBrush(Theme.Accent))
                    g.FillEllipse(fill, circle);
            }
            else
            {
                using (var pen = new Pen(item.Name == null ? Theme.Border : Theme.KeyEdge, 1.5f * s))
                {
                    if (item.Name == null)
                        pen.DashStyle = DashStyle.Dash;
                    g.DrawEllipse(pen, RectangleF.Inflate(circle, -0.75f * s, -0.75f * s));
                }
            }
            Theme.DrawCenteredGlyphs(g, item.Slot.ToString(), Theme.Bold, Theme.Scale(this),
                new PointF(circle.X + d / 2, circle.Y + d / 2),
                item.Active ? Color.White : item.Name == null ? Theme.MutedText : Theme.Text);

            // nome; embaixo, "Ativo" só no perfil ativo; ponto verde quando há alteração não gravada
            int x = (int)Math.Ceiling(circle.Right + 12 * s), right = (int)(pill.Right - 10 * s), dot = Theme.Px(this, 6);
            var nameFont = item.Name == null ? Theme.Base : Theme.Bold;
            string name = item.Name ?? "Vazio";
            int nameWidth = Math.Min(TextRenderer.MeasureText(g, name, nameFont, Size.Empty, Theme.TextFlags).Width,
                                     right - x - (item.Pending ? 2 * dot : 0));
            int nameHeight = TextRenderer.MeasureText(g, name, nameFont, Size.Empty, Theme.TextFlags).Height;
            int statusHeight = item.Active ? TextRenderer.MeasureText(g, "Ativo", Theme.Small, Size.Empty, Theme.TextFlags).Height : 0;
            int top = r.Y + (r.Height - nameHeight - statusHeight) / 2;
            TextRenderer.DrawText(g, name, nameFont, new Rectangle(x, top, nameWidth, nameHeight),
                item.Name == null ? Theme.MutedText : Theme.Text, Theme.TextFlags | TextFormatFlags.EndEllipsis);
            if (item.Active)
                TextRenderer.DrawText(g, "Ativo", Theme.Small, new Rectangle(x, top + nameHeight, right - x, statusHeight), Theme.Accent, Theme.TextFlags);
            if (item.Pending)
                using (var fill = new SolidBrush(Theme.Accent))
                    g.FillEllipse(fill, x + nameWidth + dot, top + (nameHeight - dot) / 2f, dot, dot);
        }
    }

    // Abas simples: texto com sublinhado verde na selecionada. O texto da primeira começa na borda do controle,
    // alinhado com o cartão logo abaixo.
    sealed class TabStrip : Control
    {
        string[] items = Array.Empty<string>();
        int selected, hover = -1;
        Rectangle[] bounds = Array.Empty<Rectangle>();

        public TabStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
            Font = Theme.Heading;
            Height = 36;
        }

        public event EventHandler SelectedIndexChanged;

        public string[] Items
        {
            get => items;
            set
            {
                items = value;
                Invalidate();
            }
        }

        public int SelectedIndex
        {
            get => selected;
            set
            {
                if (selected == value)
                    return;
                selected = value;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Área de clique de cada aba: o texto com folga dos lados e a altura toda.
        Rectangle HitBounds(int index) => Rectangle.Inflate(bounds[index], Theme.Px(this, 8), 0);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(BackColor);
            Theme.Smooth(g);
            float line = 3 * s;
            bounds = new Rectangle[items.Length];
            int x = 0;
            for (int i = 0; i < items.Length; i++)
            {
                var size = TextRenderer.MeasureText(g, items[i], Font, Size.Empty, Theme.TextFlags);
                bounds[i] = new Rectangle(x, 0, size.Width, Height);
                var color = !Enabled ? Theme.DisabledText : i == selected || i == hover ? Theme.Text : Theme.MutedText;
                TextRenderer.DrawText(g, items[i], Font, new Rectangle(x, 0, size.Width, Height - (int)line), color,
                    Theme.TextFlags | TextFormatFlags.VerticalCenter);
                if (i == selected)
                    Theme.FillAndStroke(g, new RectangleF(x, Height - line, size.Width, line), line / 2,
                        Enabled ? Theme.Accent : Theme.Disabled, Color.Empty);
                x += size.Width + Theme.Px(this, 24);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            for (int i = 0; i < bounds.Length; i++)
                if (HitBounds(i).Contains(e.Location))
                    SelectedIndex = i;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int over = -1;
            for (int i = 0; i < bounds.Length; i++)
                if (HitBounds(i).Contains(e.Location))
                    over = i;
            Cursor = over >= 0 ? Cursors.Hand : Cursors.Default;
            if (over != hover)
            {
                hover = over;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            hover = -1;
            Invalidate();
        }
    }
}
