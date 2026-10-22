using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenRazerProfiler.Interface
{
    // Cores, fontes e botões da interface (tema claro e discreto).
    static class Theme
    {
        public static readonly Color Background = Color.FromArgb(244, 245, 247);
        public static readonly Color Surface = Color.White;
        public static readonly Color Border = Color.FromArgb(220, 223, 228);
        public static readonly Color Text = Color.FromArgb(28, 30, 33);
        public static readonly Color MutedText = Color.FromArgb(100, 106, 114);
        public static readonly Color Accent = Color.FromArgb(28, 128, 58);
        public static readonly Color AccentHover = Color.FromArgb(22, 108, 48);
        public static readonly Color AccentPressed = Color.FromArgb(18, 92, 40);
        public static readonly Color AccentSoft = Color.FromArgb(229, 243, 233);
        public static readonly Color AccentText = Color.FromArgb(21, 95, 43);      // texto sobre AccentSoft
        public static readonly Color Hover = Color.FromArgb(240, 242, 245);
        public static readonly Color Pressed = Color.FromArgb(229, 232, 236);
        public static readonly Color Disabled = Color.FromArgb(214, 217, 222);
        public static readonly Color DisabledFill = Color.FromArgb(238, 240, 243);
        public static readonly Color DisabledText = Color.FromArgb(139, 145, 153);
        public static readonly Color WarningBack = Color.FromArgb(255, 244, 204);
        public static readonly Color WarningBorder = Color.FromArgb(240, 211, 120);
        public static readonly Color WarningText = Color.FromArgb(94, 70, 0);
        public static readonly Color Danger = Color.FromArgb(190, 40, 30);

        public static readonly Color KeyFace = Color.FromArgb(251, 251, 252);
        public static readonly Color KeyEdge = Color.FromArgb(196, 201, 208);
        public static readonly Color KeyHover = Color.FromArgb(240, 242, 245);
        public static readonly Color KeyRemapped = Color.FromArgb(218, 240, 224);
        public static readonly Color KeyDisabledFace = Color.FromArgb(224, 227, 231);
        public static readonly Color KeyDisabledText = Color.FromArgb(150, 155, 162);

        public static readonly Font Base = new Font("Segoe UI", 9f);
        public static readonly Font Bold = new Font("Segoe UI Semibold", 9f);
        public static readonly Font Small = new Font("Segoe UI", 8.25f);
        public static readonly Font Caption = new Font("Segoe UI Semibold", 8.25f);
        public static readonly Font Heading = new Font("Segoe UI Semibold", 10.5f);
        public static readonly Font Title = new Font("Segoe UI Semibold", 13f);

        // Medidas dos controles desenhados, em pixels a 96 DPI; Px converte para o DPI do controle.
        public const float ControlRadius = 6;
        public const float CardRadius = 8;   // o mesmo dos menus arredondados pelo Windows 11
        public const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        public static float Scale(Control c) => c.DeviceDpi / 96f;
        public static int Px(Control c, float pixels) => (int)Math.Round(pixels * c.DeviceDpi / 96f);

        public static FlatButton PrimaryButton(string text) => new FlatButton(ButtonKind.Primary) { Text = text };
        public static FlatButton SecondaryButton(string text) => new FlatButton(ButtonKind.Secondary) { Text = text };
        public static FlatButton GhostButton(string text) => new FlatButton(ButtonKind.Ghost) { Text = text };

        // Cor atrás do controle, usada para pintar os cantos arredondados.
        public static Color BackgroundOf(Control c)
        {
            var color = c.Parent?.BackColor ?? Surface;
            return color.A == 255 ? color : Surface;
        }

        // Suavização das formas na mesma grade de pixels do texto do Windows (GDI). Com PixelOffsetMode.Half, a
        // coordenada inteira é a borda do pixel: uma linha de 1 px em x,5 sai nítida (e não borrada em dois pixels
        // meio-tom), e as formas ficam alinhadas com o texto desenhado pelo TextRenderer.
        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
        }

        public static void FillAndStroke(Graphics g, RectangleF r, float radius, Color fill, Color border)
        {
            using (var path = RoundRect(r, radius))
            {
                if (!fill.IsEmpty)
                    using (var brush = new SolidBrush(fill))
                        g.FillPath(brush, path);
                if (!border.IsEmpty)
                    using (var pen = new Pen(border))
                        g.DrawPath(pen, path);
            }
        }

        // Moldura dos campos de texto (busca e nome): borda fina, verde e mais grossa com o cursor dentro.
        public static void DrawFieldFrame(Graphics g, Control field, bool focused)
        {
            float s = Scale(field), radius = ControlRadius * s;
            g.Clear(BackgroundOf(field));
            Smooth(g);
            FillAndStroke(g, new RectangleF(0.5f, 0.5f, field.Width - 1, field.Height - 1), radius, Surface, Border);
            if (focused)
                using (var path = RoundRect(new RectangleF(s, s, field.Width - 2 * s, field.Height - 2 * s), radius - s / 2))
                using (var pen = new Pen(Accent, 2 * s))
                    g.DrawPath(pen, path);
        }

        // Contorno de foco, que só aparece quando se navega pelo teclado. Sobre o fundo verde, em branco.
        // bounds é o contorno de 1 px do controle (em x,5); o anel fica logo dentro, em pixels inteiros.
        public static void DrawFocusRing(Graphics g, RectangleF bounds, float radius, float scale, bool onAccent)
        {
            float width = 2 * scale, inset = width / 2 + 0.5f + (onAccent ? scale : 0);
            using (var path = RoundRect(RectangleF.Inflate(bounds, -inset, -inset), Math.Max(1, radius - inset)))
            using (var pen = new Pen(onAccent ? Color.White : Text, width))
                g.DrawPath(pen, path);
        }

        // Texto curto centralizado pelo desenho das letras, e não pela caixa da fonte, que inclui as margens
        // laterais de cada caractere e o espaço de acentos e descendentes. Usado onde um algarismo fica dentro de
        // uma forma pequena (o número do perfil): ali a diferença, de até 1 px, aparece.
        public static void DrawCenteredGlyphs(Graphics g, string text, Font font, float scale, PointF center, Color color)
        {
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(color))
            {
                path.AddString(text, font.FontFamily, (int)font.Style, font.SizeInPoints * 96f / 72f * scale, PointF.Empty, StringFormat.GenericTypographic);
                var ink = path.GetBounds();
                using (var move = new Matrix())
                {
                    move.Translate(center.X - (ink.X + ink.Width / 2), center.Y - (ink.Y + ink.Height / 2));
                    path.Transform(move);
                }
                var smoothing = g.SmoothingMode;
                var offset = g.PixelOffsetMode;
                Smooth(g);
                g.FillPath(brush, path);
                g.SmoothingMode = smoothing;
                g.PixelOffsetMode = offset;
            }
        }

        // Texto de botões, etiquetas e teclas centralizado na horizontal pela tinta das letras: a caixa da fonte tem
        // margens laterais diferentes em cada letra, e o HorizontalCenter deixava o texto até 1 px à esquerda. Na
        // vertical, centraliza pela altura da linha, como os campos e rótulos, para a linha de base coincidir com a
        // dos vizinhos. trailing reserva espaço depois do texto, centralizado junto (a seta do botão de menu).
        // Devolve onde termina a tinta. Se não couber, corta com reticências.
        public static float DrawCenteredText(Graphics g, string text, Font font, Rectangle box, Color color, float trailing = 0)
        {
            var ink = InkOf(text, font);
            if (ink.IsEmpty || ink.Width + trailing > box.Width)
            {
                var room = new Rectangle(box.X, box.Y, Math.Max(0, box.Width - (int)Math.Ceiling(trailing)), box.Height);
                TextRenderer.DrawText(g, text, font, room, color,
                    TextFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return room.Right;
            }
            // O Windows só desenha texto em pixel inteiro: fica na posição mais perto do centro exato.
            int x = (int)Math.Round(box.X + (box.Width - ink.Width - trailing) / 2 - ink.X);
            TextRenderer.DrawText(g, text, font, Rectangle.FromLTRB(x, box.Top, Math.Max(x + 1, box.Right), box.Bottom), color,
                TextFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.NoClipping);
            return x + ink.Right;
        }

        static readonly Dictionary<(string, string, float, GraphicsUnit, FontStyle), RectangleF> inks =
            new Dictionary<(string, string, float, GraphicsUnit, FontStyle), RectangleF>();
        static readonly float[] linear = LinearTable();

        // Retângulo da tinta do texto que o TextRenderer desenha a partir da origem, medido uma vez por texto e fonte
        // numa imagem à parte (no mesmo tamanho em pixels da tela). Assim a centralização usa o desenho real das letras
        // sem perder a nitidez do ClearType. Cada borda fica, com fração de pixel, onde a cobertura de tinta cruza a
        // metade: as franjas do ClearType, que a imagem à parte não reproduz igual à tela, não deslocam a medida.
        public static RectangleF InkOf(string text, Font font)
        {
            var key = (text, font.Name, font.Size, font.Unit, font.Style);
            if (inks.TryGetValue(key, out var ink))
                return ink;
            const int margin = 4;
            var size = TextRenderer.MeasureText(text, font, Size.Empty, TextFlags);
            using (var bmp = new Bitmap(size.Width + 2 * margin, size.Height + 2 * margin, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    TextRenderer.DrawText(g, text, font, new Point(margin, margin), Color.Black, TextFlags);
                }
                // maior cobertura de cada coluna e de cada linha: 0 = papel, 1 = tinta (preto sobre branco, em luz linear)
                var columns = new float[bmp.Width];
                var rows = new float[bmp.Height];
                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        var c = bmp.GetPixel(x, y);
                        float cover = 1 - (linear[c.R] + linear[c.G] + linear[c.B]) / 3;
                        columns[x] = Math.Max(columns[x], cover);
                        rows[y] = Math.Max(rows[y], cover);
                    }
                float left = HalfCoverage(columns, false), right = HalfCoverage(columns, true);
                float top = HalfCoverage(rows, false), bottom = HalfCoverage(rows, true);
                ink = float.IsNaN(left) ? RectangleF.Empty : RectangleF.FromLTRB(left - margin, top - margin, right - margin, bottom - margin);
            }
            inks[key] = ink;
            return ink;
        }

        static float[] LinearTable()
        {
            var table = new float[256];
            for (int v = 0; v < 256; v++)
                table[v] = (float)Math.Pow(v / 255.0, 2.2);
            return table;
        }

        // Onde a cobertura cruza a metade, entrando pelo começo (ou pelo fim): interpolado entre os centros do último
        // pixel abaixo da metade e do primeiro acima. NaN se nada chega à metade.
        static float HalfCoverage(float[] profile, bool fromEnd)
        {
            int step = fromEnd ? -1 : 1;
            for (int i = fromEnd ? profile.Length - 1 : 0; i >= 0 && i < profile.Length; i += step)
            {
                if (profile[i] < 0.5f)
                    continue;
                int outside = i - step;
                float before = outside >= 0 && outside < profile.Length ? profile[outside] : 0;
                return outside + 0.5f + step * (0.5f - before) / (profile[i] - before);
            }
            return float.NaN;
        }

        public static void DrawChevron(Graphics g, PointF center, float scale, Color color)
        {
            using (var pen = new Pen(color, 1.4f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[]
                {
                    new PointF(center.X - 4 * scale, center.Y - 2 * scale),
                    new PointF(center.X, center.Y + 2 * scale),
                    new PointF(center.X + 4 * scale, center.Y - 2 * scale),
                });
        }

        public static void DrawCheck(Graphics g, PointF center, float scale, Color color)
        {
            using (var pen = new Pen(color, 1.6f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[]
                {
                    new PointF(center.X - 5 * scale, center.Y),
                    new PointF(center.X - 1.5f * scale, center.Y + 3.5f * scale),
                    new PointF(center.X + 5 * scale, center.Y - 3.5f * scale),
                });
        }

        public static Label Label(string text, Font font = null, Color? color = null) => new TextLabel
        {
            Text = text,
            AutoSize = true,
            Font = font ?? Base,
            ForeColor = color ?? Text,
            Margin = new Padding(0, 0, 0, 0),
        };

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // Rótulo que desenha o texto sem o recuo interno que o Windows põe dos lados (cerca de 1/6 da altura da
    // fonte): a primeira letra fica na borda do controle, na mesma coluna dos botões e campos.
    sealed class TextLabel : Label
    {
        TextFormatFlags Flags
        {
            get
            {
                var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                flags |= AutoEllipsis ? TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis : TextFormatFlags.WordBreak;
                switch (TextAlign)
                {
                    case ContentAlignment.MiddleLeft:
                    case ContentAlignment.MiddleCenter:
                    case ContentAlignment.MiddleRight:
                        flags |= TextFormatFlags.VerticalCenter;
                        break;
                    case ContentAlignment.BottomLeft:
                    case ContentAlignment.BottomCenter:
                    case ContentAlignment.BottomRight:
                        flags |= TextFormatFlags.Bottom;
                        break;
                }
                switch (TextAlign)
                {
                    case ContentAlignment.TopCenter:
                    case ContentAlignment.MiddleCenter:
                    case ContentAlignment.BottomCenter:
                        flags |= TextFormatFlags.HorizontalCenter;
                        break;
                    case ContentAlignment.TopRight:
                    case ContentAlignment.MiddleRight:
                    case ContentAlignment.BottomRight:
                        flags |= TextFormatFlags.Right;
                        break;
                }
                return flags;
            }
        }

        // Quebra linhas na largura máxima, como o Label do Windows.
        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = MaximumSize.Width > 0 ? MaximumSize.Width - Padding.Horizontal : int.MaxValue;
            var size = TextRenderer.MeasureText(Text.Length == 0 ? " " : Text, Font, new Size(width, int.MaxValue), Flags);
            return new Size((Text.Length == 0 ? 0 : size.Width) + Padding.Horizontal, size.Height + Padding.Vertical);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var bounds = new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical);
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Enabled ? ForeColor : Theme.DisabledText, Flags);
        }
    }

    // Cartão com cantos arredondados e borda fina; BackColor é a cor de dentro. O Padding de 4 px mantém os
    // filhos, que são retangulares, dentro da curva dos cantos.
    sealed class Card : Panel
    {
        public Card()
        {
            BackColor = Theme.Surface;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Padding = new Padding(4);
        }

        public Color BorderColor { get; set; } = Theme.Border;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackgroundOf(this));
            Theme.Smooth(g);
            Theme.FillAndStroke(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Theme.CardRadius * Theme.Scale(this), BackColor, BorderColor);
        }
    }

    // Botão que mostra uma cor; clicar abre o seletor de cores do Windows.
    sealed class ColorSwatch : Control
    {
        static readonly int[] customColors = new int[16];
        Color color = Color.White;

        public ColorSwatch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public event EventHandler ColorChanged;

        public Color Color
        {
            get => color;
            set
            {
                color = value;
                Invalidate();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            using (var dialog = new ColorDialog { Color = color, FullOpen = true, AnyColor = true, CustomColors = customColors })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;
                Array.Copy(dialog.CustomColors, customColors, Math.Min(16, dialog.CustomColors.Length));
                Color = dialog.Color;
                ColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
                OnClick(EventArgs.Empty);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(Theme.BackgroundOf(this));
            Theme.Smooth(g);
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            float radius = Theme.ControlRadius * s;
            Theme.FillAndStroke(g, bounds, radius, Enabled ? color : Theme.Disabled, Theme.KeyEdge);
            if (Focused && ShowFocusCues)
                Theme.DrawFocusRing(g, bounds, radius, s, false);
        }
    }
}
