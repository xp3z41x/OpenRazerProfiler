using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenRazerProfiler.Aparelhos;
using OpenRazerProfiler.Protocolo;

namespace OpenRazerProfiler.Interface
{
    // Escolhe o que a tecla selecionada faz: padrão, outra tecla (com modificadores), mídia ou nada.
    sealed class KeyEditor : UserControl
    {
        // posições no seletor de opções
        const int OptDefault = 0, OptKey = 1, OptMedia = 2, OptDisabled = 3;

        readonly Label title;
        readonly SegmentedControl options;
        readonly KeycapToggle ctrl, shift, alt, win;
        readonly SearchPicker keyPicker;
        readonly DropDownButton mediaPicker;
        readonly FlowLayoutPanel keyRow, mediaRow;

        DeviceDefinition def;
        byte? keyId;
        KeyBinding current, factory;
        byte rightModifiers;   // modificadores do lado direito vindos do teclado; preservados ao editar
        bool loading;

        public KeyEditor()
        {
            BackColor = Theme.Surface;
            Font = Theme.Base;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
            for (int i = 0; i < 4; i++)
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            title = Theme.Label("", Theme.Heading);
            title.Margin = new Padding(0, 0, 0, 10);
            root.Controls.Add(title);

            options = new SegmentedControl
            {
                Items = new[] { "Padrão", "Outra tecla ou combinação", "Mídia ou sistema", "Desativada" },
                Margin = new Padding(0, 0, 0, 10),
                AccessibleName = "O que a tecla faz",
            };
            options.SelectedIndexChanged += (s, e) => OptionChosen();
            root.Controls.Add(options);

            keyRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            ctrl = Modifier("Ctrl");
            shift = Modifier("Shift");
            alt = Modifier("Alt");
            win = Modifier("Win");
            var plus = Theme.Label("+", Theme.Base, Theme.MutedText);
            plus.Margin = new Padding(2, 8, 8, 0);
            // Lista com busca: digitar "ctrl" sugere "Ctrl esquerdo" e "Ctrl direito".
            keyPicker = new SearchPicker { Width = 240, Margin = new Padding(0), FieldName = "Tecla" };
            keyPicker.SetItems(HidUsages.KeyboardKeys, item => ((UsageInfo)item).Group);
            keyPicker.SelectedItemChanged += (s, e) => Rebuild();
            keyRow.Controls.AddRange(new Control[] { ctrl, shift, alt, win, plus, keyPicker });
            root.Controls.Add(keyRow);

            mediaRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            mediaPicker = new DropDownButton { Width = 240, Margin = new Padding(0), AccessibleName = "Mídia ou sistema" };
            mediaPicker.SetItems(HidUsages.MediaKeys);
            mediaPicker.SelectedIndexChanged += (s, e) => Rebuild();
            mediaRow.Controls.Add(mediaPicker);
            root.Controls.Add(mediaRow);

            ShowKey(null, null, null);
        }

        // Nova ação escolhida para a tecla atual.
        public event Action<byte, KeyBinding> BindingChosen;

        KeycapToggle Modifier(string text)
        {
            var key = new KeycapToggle { Text = text, Margin = new Padding(0, 1, 6, 1) };
            key.CheckedChanged += (s, e) => Rebuild();
            return key;
        }

        public void ShowKey(DeviceDefinition definition, byte? key, KeyBinding binding)
        {
            loading = true;
            try
            {
                def = definition;
                keyId = key;
                current = binding;
                var physical = key.HasValue ? def?.FindKey(key.Value) : null;
                factory = key.HasValue ? def?.DefaultFor(key.Value) : null;
                bool fixedKey = key.HasValue && def != null && def.FixedKeys.Contains(key.Value);
                bool editable = key.HasValue && binding != null && factory != null && !fixedKey;

                // Sem tecla escolhida, o editor fica vazio. A tecla fixa (Fn) mostra as opções desabilitadas.
                bool selected = key.HasValue && physical != null;
                title.Visible = options.Visible = selected;
                options.Enabled = editable;
                if (!selected)
                {
                    options.SetItemText(OptDefault, "Padrão");
                    SetOption(-1);
                    return;
                }

                title.Text = $"Tecla {KeyName(physical)}";
                options.SetItemText(OptDefault, factory != null ? $"Padrão ({HidUsages.Describe(factory, false)})" : "Padrão");

                rightModifiers = 0;
                if (!editable || binding.SameAction(factory))
                {
                    SetOption(editable ? OptDefault : -1);
                    LoadKey(factory);
                }
                else if (binding.IsDisabled)
                {
                    SetOption(OptDisabled);
                    LoadKey(factory);
                }
                else if (binding.IsKeyboard)
                {
                    SetOption(OptKey);
                    LoadKey(binding);
                }
                else if (HidUsages.MediaKeys.Any(m => m.Binding.Equals(binding)))
                {
                    SetOption(OptMedia);
                    mediaPicker.SelectedItem = HidUsages.MediaKeys.First(m => m.Binding.Equals(binding));
                    LoadKey(factory);
                }
                else
                {
                    SetOption(-1);   // função do firmware sem edição: escolher uma opção a substitui
                    LoadKey(factory);
                }
            }
            finally
            {
                loading = false;
            }
        }

        static string KeyName(PhysicalKey k) => k.Label switch
        {
            "|◀" => "Faixa anterior",
            "▶‖" => "Reproduzir/Pausar",
            "▶|" => "Próxima faixa",
            "Vol +" => "Roda de volume (para cima)",
            "Vol −" => "Roda de volume (para baixo)",
            _ => k.Label,
        };

        // -1 = nenhuma opção marcada.
        void SetOption(int option)
        {
            options.SelectedIndex = option;
            UpdateRows();
        }

        // Preenche modificadores e tecla a partir de uma ação de teclado.
        void LoadKey(KeyBinding b)
        {
            bool wasLoading = loading;
            loading = true;
            try
            {
                LoadKeyControls(b);
            }
            finally
            {
                loading = wasLoading;
            }
        }

        void LoadKeyControls(KeyBinding b)
        {
            byte mods = 0, usage = 0;
            if (b != null && b.IsKeyboard)
            {
                mods = b.Modifiers;
                usage = b.Usage;
                // Um modificador sozinho na forma de máscara aparece como a própria tecla na lista.
                if (usage == 0 && mods != 0 && (mods & (mods - 1)) == 0)
                {
                    usage = (byte)(0xE0 + (int)Math.Log(mods, 2));
                    mods = 0;
                }
            }
            rightModifiers = (byte)(mods & 0xF0);
            ctrl.Checked = (mods & 0x01) != 0;
            shift.Checked = (mods & 0x02) != 0;
            alt.Checked = (mods & 0x04) != 0;
            win.Checked = (mods & 0x08) != 0;
            keyPicker.SelectedItem = HidUsages.Find(usage);
            if (mediaPicker.SelectedIndex < 0)
                mediaPicker.SelectedIndex = 0;
        }

        void OptionChosen()
        {
            UpdateRows();
            if (options.SelectedIndex == OptKey && keyPicker.SelectedItem == null)
                LoadKey(factory);
            Rebuild();
        }

        void UpdateRows()
        {
            keyRow.Visible = options.SelectedIndex == OptKey;
            mediaRow.Visible = options.SelectedIndex == OptMedia;
        }

        // Monta a ação a partir dos controles e avisa quem estiver ouvindo.
        void Rebuild()
        {
            if (loading || !keyId.HasValue || factory == null)
                return;
            KeyBinding chosen = null;
            int option = options.SelectedIndex;
            if (option == OptDefault)
                chosen = current != null && current.SameAction(factory) ? current : factory;
            else if (option == OptDisabled)
                chosen = KeyBinding.Disabled();
            else if (option == OptMedia && mediaPicker.SelectedItem is MediaInfo m)
                chosen = m.Binding;
            else if (option == OptKey && keyPicker.SelectedItem is UsageInfo u)
            {
                byte mods = (byte)(rightModifiers | (ctrl.Checked ? 0x01 : 0) | (shift.Checked ? 0x02 : 0) |
                                   (alt.Checked ? 0x04 : 0) | (win.Checked ? 0x08 : 0));
                chosen = KeyBinding.Keyboard(mods, u.Usage);
                if (chosen.SameAction(factory))
                    chosen = current != null && current.SameAction(factory) ? current : factory;
            }
            if (chosen == null || chosen.Equals(current))
                return;
            current = chosen;
            BindingChosen?.Invoke(keyId.Value, chosen);
        }
    }
}
