using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using OpenRazerProfiler.Aparelhos;

namespace OpenRazerProfiler.Interface
{
    enum KeyLook
    {
        Default,
        Remapped,
        Disabled,
    }

    // Desenho do teclado a partir do mapa físico da definição; clicar seleciona uma tecla. Cada tecla mostra só o
    // próprio nome: o estado aparece pela cor, e o que ela faz, no editor, ao clicar.
    sealed class KeyboardView : Control
    {
        IReadOnlyList<PhysicalKey> layout = Array.Empty<PhysicalKey>();
        readonly Dictionary<float, Font> fonts = new Dictionary<float, Font>();
        byte? selected, hover;
        float unitsWide = 1, unitsHigh = 1;

        public KeyboardView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
        }

        // Aparência de cada tecla.
        public Func<byte, KeyLook> Describe { get; set; }

        public event EventHandler SelectedKeyChanged;

        public IReadOnlyList<PhysicalKey> KeyLayout
        {
            get => layout;
            set
            {
                layout = value ?? Array.Empty<PhysicalKey>();
                unitsWide = layout.Count == 0 ? 1 : layout.Max(k => k.X + k.Width);
                unitsHigh = layout.Count == 0 ? 1 : layout.Max(k => k.Y + k.Height);
                Invalidate();
            }
        }

        public byte? SelectedKey
        {
            get => selected;
            set
            {
                if (selected == value)
                    return;
                selected = value;
                Invalidate();
                SelectedKeyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public float AspectRatio => unitsHigh / unitsWide;

        // Escala para caber no controle, presa ao canto superior esquerdo: a primeira tecla começa na mesma
        // coluna da legenda e do editor logo abaixo.
        float Metrics(out PointF origin)
        {
            float s = Math.Max(1, Math.Min((Width - 2) / unitsWide, (Height - 2) / unitsHigh));
            float gap = Gap(s);
            origin = new PointF(1 - gap / 2, 1 - gap / 2);
            return s;
        }

        static float Gap(float s) => Math.Max(2, s * 0.08f);

        static RectangleF KeyRect(PhysicalKey k, float s, PointF o)
        {
            float gap = Gap(s);
            return new RectangleF(o.X + k.X * s + gap / 2, o.Y + k.Y * s + gap / 2, k.Width * s - gap, k.Height * s - gap);
        }

        PhysicalKey HitTest(Point p)
        {
            float s = Metrics(out var o);
            return layout.FirstOrDefault(k => KeyRect(k, s, o).Contains(p));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var k = HitTest(e.Location)?.Id;
            Cursor = k.HasValue ? Cursors.Hand : Cursors.Default;
            if (k != hover)
            {
                hover = k;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = null;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var k = HitTest(e.Location);
            if (k != null)
                SelectedKey = k.Id;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);
            float s = Metrics(out var o);
            foreach (var k in layout)
            {
                var look = Describe?.Invoke(k.Id) ?? KeyLook.Default;
                var r = KeyRect(k, s, o);
                // A tecla ocupa pixels inteiros: a borda de 1 px fica no meio do pixel (nítida) e o nome centraliza
                // na mesma caixa. A tecla escolhida ganha uma borda de 2 px, também em pixels inteiros.
                var cell = Rectangle.FromLTRB((int)Math.Round(r.Left), (int)Math.Round(r.Top), (int)Math.Round(r.Right), (int)Math.Round(r.Bottom));
                var edge = new RectangleF(cell.X + 0.5f, cell.Y + 0.5f, cell.Width - 1, cell.Height - 1);
                float radius = s * 0.12f;
                bool isSelected = selected == k.Id;
                Color face = look == KeyLook.Remapped ? Theme.KeyRemapped
                    : look == KeyLook.Disabled ? Theme.KeyDisabledFace
                    : hover == k.Id ? Theme.KeyHover : Theme.KeyFace;
                Theme.FillAndStroke(g, edge, radius, face, isSelected ? Color.Empty : look == KeyLook.Remapped ? Theme.Accent : Theme.KeyEdge);
                if (isSelected)
                    using (var path = Theme.RoundRect(new RectangleF(cell.X + 1, cell.Y + 1, cell.Width - 2, cell.Height - 2), radius - 0.5f))
                    using (var pen = new Pen(Theme.Accent, 2))
                        g.DrawPath(pen, path);
                var text = look == KeyLook.Remapped ? Theme.AccentText : look == KeyLook.Disabled ? Theme.KeyDisabledText : Theme.Text;
                DrawFitted(g, k.Label, Rectangle.Inflate(cell, -(int)Math.Round(s * 0.06f), -(int)Math.Round(s * 0.04f)), s * 0.27f, text);
            }
        }

        // Desenha o nome no maior tamanho que couber (até sizePx), centralizado pelo desenho das letras: na
        // horizontal, pela tinta do próprio nome (a caixa da fonte inclui margens laterais, e "F1" ficava 1 px à
        // esquerda); na vertical, pela faixa das maiúsculas, a mesma em todas as teclas. Se nem o menor tamanho
        // couber, volta a centralizar pela caixa e corta com reticências.
        void DrawFitted(Graphics g, string text, Rectangle box, float sizePx, Color color)
        {
            if (string.IsNullOrEmpty(text) || box.Width <= 2 || box.Height <= 2)
                return;
            float size = Math.Max(6, sizePx);
            Font font = GetFont(size);
            while (size > 7 && TextRenderer.MeasureText(g, text, font, Size.Empty, Theme.TextFlags).Width > box.Width)
            {
                size -= 1;
                font = GetFont(size);
            }
            var ink = Theme.InkOf(text, font);
            var capitals = Theme.InkOf("H", font);
            if (ink.IsEmpty || ink.Width > box.Width)
            {
                TextRenderer.DrawText(g, text, font, box, color,
                    Theme.TextFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }
            // O Windows só desenha texto em pixel inteiro: arredonda para a posição mais perto do centro exato.
            var origin = new Point((int)Math.Round(box.X + box.Width / 2f - (ink.X + ink.Width / 2)),
                                   (int)Math.Round(box.Y + box.Height / 2f - (capitals.Y + capitals.Height / 2)));
            TextRenderer.DrawText(g, text, font, origin, color, Theme.TextFlags);
        }

        Font GetFont(float sizePx)
        {
            float key = (float)Math.Round(sizePx);
            if (!fonts.TryGetValue(key, out var f))
            {
                f = new Font("Segoe UI", key, GraphicsUnit.Pixel);
                fonts[key] = f;
            }
            return f;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var f in fonts.Values)
                    f.Dispose();
            base.Dispose(disposing);
        }
    }

    // Item da legenda do teclado: amostra no formato de tecla e o nome, centralizados na mesma linha.
    sealed class LegendItem : Control
    {
        readonly Color face, edge;

        public LegendItem(string text, Color face, Color edge)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Text = text;
            this.face = face;
            this.edge = edge;
            Font = Theme.Small;
            AutoSize = true;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(Text, Font, Size.Empty, Theme.TextFlags);
            return new Size(Theme.Px(this, 1 + 16 + 6) + text.Width, Theme.Px(this, 28));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this), size = 16 * s;
            g.Clear(Theme.BackgroundOf(this));
            Theme.Smooth(g);
            var swatch = new RectangleF(1.5f, (Height - size) / 2, size, size);
            Theme.FillAndStroke(g, swatch, 3 * s, face, edge);
            int x = (int)Math.Ceiling(swatch.Right + 6 * s);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Width - x, Height), Theme.MutedText,
                Theme.TextFlags | TextFormatFlags.VerticalCenter);
        }
    }
}
