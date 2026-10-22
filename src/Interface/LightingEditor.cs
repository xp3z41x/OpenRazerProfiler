using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenRazerProfiler.Aparelhos;
using OpenRazerProfiler.Protocolo;

namespace OpenRazerProfiler.Interface
{
    // Edita o efeito de luz e o brilho de um perfil, só com efeitos do próprio firmware.
    sealed class LightingEditor : UserControl
    {
        static readonly Rgb DefaultColor1 = new Rgb(0x44, 0xD6, 0x2C);
        static readonly Rgb DefaultColor2 = new Rgb(0x00, 0x78, 0xFF);

        readonly Dictionary<byte, ChipRadio> effectButtons = new Dictionary<byte, ChipRadio>();
        readonly FlowLayoutPanel effectPanel;
        readonly TableLayoutPanel grid;
        readonly Label modeLabel, colorLabel, directionLabel, speedLabel, brightnessLabel;
        readonly SegmentedControl modeSelector, directionSelector;
        readonly ColorSwatch color1, color2;
        readonly FlowLayoutPanel colorRow;
        readonly Slider speedSlider, brightnessSlider;
        readonly Label speedMin, speedMax, brightnessValue;
        readonly TableLayoutPanel speedRow;

        ChipRadio otherEffect;   // efeito gravado que não está na lista (ex.: um personalizado do Synapse)
        DeviceDefinition def;
        LightingEffect original;
        byte originalBrightness;
        byte effect;
        bool loading, speedTouched, brightnessTouched;

        public LightingEditor()
        {
            BackColor = Theme.Surface;
            Font = Theme.Base;
            Padding = new Padding(20, 16, 20, 16);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = false };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(Theme.Label("EFEITO", Theme.Caption, Theme.MutedText));
            effectPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 12), Dock = DockStyle.Top };
            root.Controls.Add(effectPanel);

            // Coluna dos rótulos com a largura do maior possível: trocar de efeito não desloca os controles.
            int labelColumn = new[] { "Cores", "Cor", "Direção", "Velocidade", "Duração", "Brilho" }
                .Max(t => TextRenderer.MeasureText(t, Theme.Base, Size.Empty, Theme.TextFlags).Width) + Theme.Px(this, 20);
            grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top, Margin = new Padding(0) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelColumn));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(grid);

            modeLabel = RowLabel("Cores");
            modeSelector = Selector("Cores", "Uma cor", "Duas cores", "Aleatórias");

            colorLabel = RowLabel("Cor");
            color1 = new ColorSwatch { Size = new Size(64, 32), Margin = new Padding(0, 3, 8, 3) };
            color2 = new ColorSwatch { Size = new Size(64, 32), Margin = new Padding(0, 3, 8, 3) };
            color1.ColorChanged += (s, e) => UserChanged();
            color2.ColorChanged += (s, e) => UserChanged();
            colorRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            colorRow.Controls.Add(color1);
            colorRow.Controls.Add(color2);

            directionLabel = RowLabel("Direção");
            directionSelector = Selector("Direção", "Esquerda → direita", "Direita → esquerda");

            speedLabel = RowLabel("Velocidade");
            speedSlider = new Slider { Margin = new Padding(0, 6, 0, 2), AccessibleName = "Velocidade" };
            speedSlider.ValueChanged += (s, e) =>
            {
                if (!loading)
                    speedTouched = true;
                UserChanged();
            };
            speedMin = Theme.Label("Lenta", Theme.Small, Theme.MutedText);
            speedMax = Theme.Label("Rápida", Theme.Small, Theme.MutedText);
            speedRow = SliderRow(speedSlider, null, speedMin, speedMax);
            // Com as legendas embaixo, a linha é mais alta: o rótulo acompanha o trilho, não o meio da linha.
            speedLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            speedLabel.Margin = new Padding(0, speedSlider.Margin.Top + (speedSlider.Height - speedLabel.PreferredHeight) / 2, 20, 0);

            brightnessLabel = RowLabel("Brilho");
            brightnessSlider = new Slider { Minimum = 0, Maximum = 100, LargeChange = 10, Margin = new Padding(0, 7, 0, 7), AccessibleName = "Brilho" };
            brightnessValue = Theme.Label("100 %", Theme.Base, Theme.Text);
            brightnessSlider.ValueChanged += (s, e) =>
            {
                brightnessValue.Text = $"{brightnessSlider.Value} %";
                if (!loading)
                    brightnessTouched = true;
                UserChanged();
            };
            var brightnessRow = SliderRow(brightnessSlider, brightnessValue, null, null);

            AddRow(modeLabel, modeSelector);
            AddRow(colorLabel, colorRow);
            AddRow(directionLabel, directionSelector);
            AddRow(speedLabel, speedRow);
            AddRow(brightnessLabel, brightnessRow);
        }

        public event EventHandler Changed;

        public LightingEffect Effect { get; private set; }
        public byte Brightness { get; private set; }

        Label RowLabel(string text)
        {
            var l = Theme.Label(text, Theme.Base, Theme.MutedText);
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(0, 6, 20, 6);
            return l;
        }

        SegmentedControl Selector(string name, params string[] items)
        {
            var selector = new SegmentedControl { Items = items, Margin = new Padding(0, 3, 0, 3), AccessibleName = name };
            selector.SelectedIndexChanged += (s, e) => UserChanged();
            return selector;
        }

        void AddRow(Label label, Control control)
        {
            grid.Controls.Add(label);
            grid.Controls.Add(control);
        }

        // Barra deslizante com o valor à direita e, se houver, legendas das pontas logo abaixo.
        static TableLayoutPanel SliderRow(Slider bar, Label value, Label minCaption, Label maxCaption)
        {
            var row = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 2, Margin = new Padding(0) };
            row.Controls.Add(bar, 0, 0);
            if (value != null)
            {
                value.Anchor = AnchorStyles.Left;
                value.Margin = new Padding(10, 0, 0, 0);
                row.Controls.Add(value, 1, 0);
            }
            if (minCaption != null)
            {
                var captions = new TableLayoutPanel
                {
                    ColumnCount = 2, RowCount = 1, AutoSize = false, Dock = DockStyle.Fill, Width = bar.Width,
                    Height = minCaption.PreferredHeight, Margin = new Padding(0, 0, 0, 4),
                };
                captions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                captions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                minCaption.Anchor = AnchorStyles.Left;
                maxCaption.Anchor = AnchorStyles.Right;
                minCaption.Margin = new Padding(0);
                maxCaption.Margin = new Padding(0);
                captions.Controls.Add(minCaption, 0, 0);
                captions.Controls.Add(maxCaption, 1, 0);
                row.Controls.Add(captions, 0, 1);
            }
            return row;
        }

        public void Bind(DeviceDefinition definition, ProfileState profile)
        {
            loading = true;
            try
            {
                if (def != definition)
                {
                    def = definition;
                    BuildEffectButtons();
                }
                original = profile.Lighting;
                originalBrightness = profile.Brightness;
                speedTouched = brightnessTouched = false;
                effect = original.Effect;
                color1.Color = ToColor(original.ColorOr(0, DefaultColor1));
                color2.Color = ToColor(original.ColorOr(1, DefaultColor2));
                // Efeito fora da lista: aparece numa etiqueta própria, marcada; escolher outro o substitui.
                bool known = def.Effects.Contains(effect);
                otherEffect.Visible = !known;
                if (!known)
                {
                    otherEffect.Tag = effect;
                    otherEffect.Text = EffectLabel(effect);
                }
                foreach (var kv in effectButtons)
                    kv.Value.Checked = kv.Key == effect;
                otherEffect.Checked = !known;
                LoadParameters(original);
                brightnessSlider.Value = (int)Math.Round(profile.Brightness * 100 / 255.0);
                brightnessValue.Text = $"{brightnessSlider.Value} %";
                Effect = original;
                Brightness = originalBrightness;
                UpdateLayoutForEffect();
            }
            finally
            {
                loading = false;
            }
        }

        void BuildEffectButtons()
        {
            effectPanel.Controls.Clear();
            effectButtons.Clear();
            foreach (var id in def.Effects)
                effectButtons[id] = AddChip(id);
            otherEffect = AddChip(0);
            otherEffect.Visible = false;
        }

        ChipRadio AddChip(byte id)
        {
            var chip = new ChipRadio { Text = EffectLabel(id), Margin = new Padding(0, 0, 6, 6), Tag = id };
            chip.CheckedChanged += (s, e) =>
            {
                if (chip.Checked && !loading)
                {
                    effect = (byte)chip.Tag;
                    loading = true;
                    try
                    {
                        LoadParameters(effect == original.Effect ? original : null);
                    }
                    finally
                    {
                        loading = false;
                    }
                    UserChanged();
                }
            };
            effectPanel.Controls.Add(chip);
            return chip;
        }

        public static string EffectLabel(byte id) => id switch
        {
            EffectId.Off => "Desligado",
            EffectId.Static => "Estático",
            EffectId.Breathing => "Breathing",
            EffectId.Spectrum => "Spectrum",
            EffectId.Wave => "Wave",
            EffectId.Reactive => "Reactive",
            EffectId.Starlight => "Starlight",
            EffectId.Custom => "Personalizado",
            _ => $"Efeito 0x{id:X2}",
        };

        // Carrega os controles a partir de um efeito; null = valores padrão do efeito selecionado.
        void LoadParameters(LightingEffect from)
        {
            speedTouched = false;
            if (effect == EffectId.Breathing)
            {
                int mode = from?.Param1 ?? BreathingMode.Single;
                modeSelector.SelectedIndex = mode == BreathingMode.Dual ? 1 : mode == BreathingMode.Random ? 2 : 0;
            }
            else if (effect == EffectId.Starlight)
            {
                int n = from?.Colors.Count ?? 1;
                modeSelector.SelectedIndex = n >= 2 ? 1 : n == 0 ? 2 : 0;
            }

            if (effect == EffectId.Wave)
            {
                directionSelector.SelectedIndex = from != null && from.Param1 == def.WaveRightToLeft ? 1 : 0;
                speedSlider.Minimum = 1;
                speedSlider.Maximum = def.WaveSpeeds.Length;
                speedSlider.Value = 1 + NearestWaveSpeed(from?.Param2 ?? def.WaveDefaultSpeed);
                speedLabel.Text = "Velocidade";
                speedMin.Text = "Lenta";
                speedMax.Text = "Rápida";
            }
            else if (effect == EffectId.Reactive || effect == EffectId.Starlight)
            {
                byte min = effect == EffectId.Reactive ? def.ReactiveSpeedMin : def.StarlightSpeedMin;
                byte max = effect == EffectId.Reactive ? def.ReactiveSpeedMax : def.StarlightSpeedMax;
                speedSlider.Minimum = min;
                speedSlider.Maximum = max;
                int value = from?.Param2 ?? (min + max) / 2;
                speedSlider.Value = Math.Max(min, Math.Min(max, value));
                speedLabel.Text = "Duração";
                speedMin.Text = "Curta";
                speedMax.Text = "Longa";
            }
            speedSlider.AccessibleName = speedLabel.Text;
        }

        int NearestWaveSpeed(byte value)
        {
            int best = 0;
            for (int i = 1; i < def.WaveSpeeds.Length; i++)
                if (Math.Abs(Math.Log(def.WaveSpeeds[i]) - Math.Log(Math.Max((byte)1, value))) <
                    Math.Abs(Math.Log(def.WaveSpeeds[best]) - Math.Log(Math.Max((byte)1, value))))
                    best = i;
            return best;
        }

        void UserChanged()
        {
            if (loading || def == null)
                return;
            Effect = BuildEffect();
            Brightness = brightnessTouched ? (byte)Math.Round(brightnessSlider.Value * 255 / 100.0) : originalBrightness;
            UpdateLayoutForEffect();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        LightingEffect BuildEffect()
        {
            bool same = original != null && original.Effect == effect;
            var c1 = FromColor(color1.Color);
            var c2 = FromColor(color2.Color);
            switch (effect)
            {
                case EffectId.Off:
                    return same ? original : LightingEffect.Off();
                case EffectId.Static:
                    return LightingEffect.Static(c1);
                case EffectId.Breathing:
                {
                    byte p2 = same ? original.Param2 : (byte)0;
                    return modeSelector.SelectedIndex switch
                    {
                        1 => new LightingEffect(EffectId.Breathing, BreathingMode.Dual, p2, new[] { c1, c2 }),
                        2 => new LightingEffect(EffectId.Breathing, BreathingMode.Random, p2, null),
                        _ => new LightingEffect(EffectId.Breathing, BreathingMode.Single, p2, new[] { c1 }),
                    };
                }
                case EffectId.Spectrum:
                    return same ? original : LightingEffect.Spectrum();
                case EffectId.Wave:
                {
                    byte dir = directionSelector.SelectedIndex == 1 ? def.WaveRightToLeft : def.WaveLeftToRight;
                    byte speed = !speedTouched && same ? original.Param2 : def.WaveSpeeds[speedSlider.Value - 1];
                    if (!speedTouched && !same)
                        speed = def.WaveDefaultSpeed;
                    return LightingEffect.Wave(dir, speed);
                }
                case EffectId.Reactive:
                    return LightingEffect.Reactive((byte)speedSlider.Value, c1);
                case EffectId.Starlight:
                    return modeSelector.SelectedIndex switch
                    {
                        1 => LightingEffect.Starlight((byte)speedSlider.Value, c1, c2),
                        2 => LightingEffect.Starlight((byte)speedSlider.Value),
                        _ => LightingEffect.Starlight((byte)speedSlider.Value, c1),
                    };
                default:
                    return original;
            }
        }

        void UpdateLayoutForEffect()
        {
            bool multi = effect == EffectId.Breathing || effect == EffectId.Starlight;
            bool random = multi && modeSelector.SelectedIndex == 2;
            bool hasColor = !random && (effect == EffectId.Static || effect == EffectId.Breathing || effect == EffectId.Reactive || effect == EffectId.Starlight);
            bool twoColors = multi && modeSelector.SelectedIndex == 1;
            SetRow(modeLabel, modeSelector, multi);
            SetRow(colorLabel, colorRow, hasColor);
            // Com o seletor "Cores" logo acima, as amostras ficam sem rótulo próprio, como parte dele.
            colorLabel.Text = multi ? "" : "Cor";
            color2.Visible = twoColors;
            SetRow(directionLabel, directionSelector, effect == EffectId.Wave);
            SetRow(speedLabel, speedRow, effect == EffectId.Wave || effect == EffectId.Reactive || effect == EffectId.Starlight);
        }

        static void SetRow(Control label, Control control, bool visible)
        {
            label.Visible = visible;
            control.Visible = visible;
        }

        static Color ToColor(Rgb c) => Color.FromArgb(c.R, c.G, c.B);
        static Rgb FromColor(Color c) => new Rgb(c.R, c.G, c.B);
    }
}
