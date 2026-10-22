using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenRazerProfiler.Aparelhos;
using OpenRazerProfiler.Protocolo;

namespace OpenRazerProfiler.Interface
{
    sealed class MainForm : Form
    {
        const string AppName = "OpenRazerProfiler";
        const int Gutter = 16;   // margem lateral da janela: cabeçalho, avisos, cartões e rodapé começam na mesma coluna

        readonly Session session = new Session();
        readonly ToolTip tips = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300 };
        readonly Timer previewTimer = new Timer { Interval = 80 };

        // cabeçalho
        readonly Label titleLabel, infoLabel;
        readonly Button readButton, backupButton, saveButton;
        readonly ModernMenu backupMenu;
        // avisos
        readonly TableLayoutPanel banners;
        readonly List<Label> bannerLabels = new List<Label>();
        // perfis
        readonly ProfileList profileList;
        readonly ModernMenu profileMenu;
        readonly Button activateButton, renameButton, createButton, deleteButton;
        // conteúdo
        readonly TabStrip tabs;
        readonly Panel contentHost;
        readonly LightingEditor lighting;
        readonly Panel keysPage, emptyPage, offlinePage;
        readonly KeyboardView keyboard;
        readonly KeyEditor keyEditor;
        readonly Button restoreAllButton;
        readonly Label offlineLabel;
        readonly Button emptyCreateButton, offlineRetryButton;
        // rodapé
        readonly Label pollingLabel;
        readonly DropDownButton pollingPicker;
        readonly Label statusLabel;
        readonly Button discardButton;

        bool busy, updatingUi, previewRunning;
        byte selectedSlot = 1;

        public MainForm()
        {
            Text = AppName;
            Font = Theme.Base;
            BackColor = Theme.Background;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1120, 720);
            MinimumSize = new Size(960, 640);
            KeyPreview = true;
            // Com todos os tamanhos, o WinForms pega o exato da barra de título e o da barra de tarefas em qualquer escala.
            using (var icon = typeof(MainForm).Assembly.GetManifestResourceStream("OpenRazerProfiler.app.ico"))
                Icon = new Icon(icon, SystemInformation.IconSize);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0), Padding = new Padding(0) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            // ---- cabeçalho ----
            // 1 px a menos à esquerda: o desenho das letras grandes do título já começa um pouco para dentro.
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, BackColor = Theme.Surface, Padding = new Padding(Gutter - 1, 12, Gutter, 12), Margin = new Padding(0) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var titles = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            titleLabel = Theme.Label("Procurando o teclado…", Theme.Title);
            infoLabel = Theme.Label("", Theme.Base, Theme.MutedText);
            infoLabel.Margin = new Padding(0, 2, 0, 0);
            titles.Controls.Add(titleLabel);
            titles.Controls.Add(infoLabel);
            header.Controls.Add(titles, 0, 0);
            var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = new Padding(0) };
            backupMenu = new ModernMenu();
            backupMenu.Add("Exportar backup…", (s, e) => ExportBackup());
            backupMenu.Add("Importar backup…", (s, e) => ImportBackup());
            readButton = Theme.SecondaryButton("Ler do teclado");
            var backup = Theme.SecondaryButton("Backup");
            backup.DropDownMenu = backupMenu;
            backupButton = backup;
            saveButton = Theme.PrimaryButton("Gravar no teclado");
            actions.Controls.AddRange(new Control[] { readButton, backupButton, saveButton });
            header.Controls.Add(actions, 1, 0);
            root.Controls.Add(header, 0, 0);
            tips.SetToolTip(readButton, "Ler do teclado (F5)");
            tips.SetToolTip(saveButton, "Gravar no teclado (Ctrl+S)");

            readButton.Click += async (s, e) => await ReadKeyboard(askIfPending: true);
            saveButton.Click += async (s, e) => await SaveKeyboard();

            // ---- avisos ----
            banners = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, Margin = new Padding(0), Padding = new Padding(Gutter, 0, Gutter, 0) };
            banners.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(banners, 0, 1);

            // ---- corpo ----
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(Gutter, 12, Gutter, 12), Margin = new Padding(0) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(body, 0, 2);

            // Dentro do cartão, título, números dos perfis e botões começam a 18 px da borda.
            var profilesCard = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0) };
            var profilesLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Theme.Surface };
            profilesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            profilesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            profilesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var profilesCaption = Theme.Label("PERFIS NO TECLADO", Theme.Caption, Theme.MutedText);
            profilesCaption.Margin = new Padding(14, 12, 0, 6);
            profilesLayout.Controls.Add(profilesCaption);
            profileList = new ProfileList { Dock = DockStyle.Fill, Margin = new Padding(0) };
            profileList.ItemHeight = (int)(46 * DeviceDpi / 96f);
            profileList.SelectedIndexChanged += (s, e) => OnProfileSelected();
            profilesLayout.Controls.Add(profileList);
            var profileButtons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Padding = new Padding(11, 8, 11, 11), Margin = new Padding(0) };
            profileButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            profileButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            activateButton = SlotButton("Ativar");
            renameButton = SlotButton("Renomear…");
            createButton = SlotButton("Criar…");
            deleteButton = SlotButton("Apagar");
            profileButtons.Controls.Add(activateButton, 0, 0);
            profileButtons.Controls.Add(renameButton, 1, 0);
            profileButtons.Controls.Add(createButton, 0, 1);
            profileButtons.Controls.Add(deleteButton, 1, 1);
            profilesLayout.Controls.Add(profileButtons);
            profilesCard.Controls.Add(profilesLayout);
            body.Controls.Add(profilesCard, 0, 0);
            activateButton.Click += (s, e) => ActivateSlot();
            renameButton.Click += (s, e) => RenameSlot();
            createButton.Click += (s, e) => CreateSlot();
            deleteButton.Click += (s, e) => DeleteSlot();

            // Clique direito num perfil: as mesmas quatro ações dos botões.
            profileMenu = new ModernMenu();
            var menuCreate = profileMenu.Add("Criar…", (s, e) => CreateSlot());
            var menuRename = profileMenu.Add("Renomear…", (s, e) => RenameSlot());
            var menuActivate = profileMenu.Add("Ativar", (s, e) => ActivateSlot());
            var menuDelete = profileMenu.Add("Apagar", (s, e) => DeleteSlot());
            profileMenu.Opening += (s, e) =>
            {
                var mouse = profileList.PointToClient(Cursor.Position);
                if (profileList.ClientRectangle.Contains(mouse) && profileList.IndexFromPoint(mouse) < 0)
                {
                    e.Cancel = true;   // clique abaixo da última linha
                    return;
                }
                var can = SlotActions();
                menuCreate.Enabled = can.Create;
                menuRename.Enabled = can.Rename;
                menuActivate.Enabled = can.Activate;
                menuDelete.Enabled = can.Delete;
            };
            ContextMenus.Attach(profileList, profileMenu, () =>
            {
                var row = profileList.SelectedRowBounds;
                return new Point(row.X + Theme.Px(profileList, 48), row.Bottom);
            });

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(0) };
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tabs = new TabStrip { Dock = DockStyle.Fill, Items = new[] { "Iluminação", "Teclas" }, Margin = new Padding(0, 0, 0, 6) };
            tabs.Height = (int)(36 * DeviceDpi / 96f);
            tabs.SelectedIndexChanged += (s, e) => ShowPage();
            right.Controls.Add(tabs);
            var contentCard = new Card { Dock = DockStyle.Fill, Margin = new Padding(0) };
            contentHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
            contentCard.Controls.Add(contentHost);
            right.Controls.Add(contentCard);
            body.Controls.Add(right, 1, 0);

            // As duas páginas começam o conteúdo a 20 px da borda de dentro do cartão.
            lighting = new LightingEditor { Dock = DockStyle.Fill };
            lighting.Changed += (s, e) => OnLightingChanged();

            keysPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(20, 16, 20, 16) };
            keyEditor = new KeyEditor { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0) };
            keyEditor.BindingChosen += OnBindingChosen;
            var legend = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(0, 8, 0, 8) };
            legend.Controls.Add(new LegendItem("Alterada", Theme.KeyRemapped, Theme.Accent) { Margin = new Padding(0, 0, 18, 0) });
            legend.Controls.Add(new LegendItem("Desativada", Theme.KeyDisabledFace, Theme.KeyEdge) { Margin = new Padding(0, 0, 18, 0) });
            legend.Controls.Add(new LegendItem("Padrão", Theme.KeyFace, Theme.KeyEdge) { Margin = new Padding(0, 0, 18, 0) });
            restoreAllButton = Theme.GhostButton("Restaurar todas as teclas deste perfil");
            restoreAllButton.Margin = new Padding(0);
            restoreAllButton.Click += (s, e) => RestoreAllKeys();
            legend.Controls.Add(restoreAllButton);
            keyboard = new KeyboardView { Dock = DockStyle.Top, Height = 240 };
            keyboard.Describe = DescribeKey;
            keyboard.SelectedKeyChanged += (s, e) => ShowSelectedKey();
            var divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border, Margin = new Padding(0) };
            keysPage.Controls.Add(keyEditor);
            keysPage.Controls.Add(divider);
            keysPage.Controls.Add(legend);
            keysPage.Controls.Add(keyboard);
            keysPage.Resize += (s, e) => SizeKeyboard();

            emptyCreateButton = Theme.PrimaryButton("Criar perfil neste slot…");
            emptyCreateButton.Click += (s, e) => CreateSlot();
            emptyPage = CenteredPage(emptyCreateButton);
            offlineLabel = Theme.Label("", Theme.Heading, Theme.MutedText);
            offlineRetryButton = Theme.PrimaryButton("Tentar de novo");
            offlineRetryButton.Margin = new Padding(0, 16, 0, 0);
            offlineRetryButton.Click += async (s, e) => await ReadKeyboard(askIfPending: false);
            offlinePage = CenteredPage(offlineLabel, offlineRetryButton);

            // ---- rodapé ----
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true, BackColor = Theme.Surface, Padding = new Padding(Gutter, 8, Gutter, 8), Margin = new Padding(0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pollingLabel = Theme.Label("Taxa de polling", Theme.Base, Theme.MutedText);
            pollingLabel.Anchor = AnchorStyles.Left;
            pollingLabel.Margin = new Padding(0, 0, 10, 0);
            pollingPicker = new DropDownButton { Width = 140, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 20, 0), AccessibleName = "Taxa de polling" };
            pollingPicker.SelectedIndexChanged += (s, e) => OnPollingChanged();
            statusLabel = Theme.Label("", Theme.Base, Theme.MutedText);
            statusLabel.Anchor = AnchorStyles.Left;
            statusLabel.AutoEllipsis = true;
            statusLabel.AutoSize = false;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            discardButton = Theme.GhostButton("Descartar alterações");
            discardButton.Anchor = AnchorStyles.Right;
            discardButton.Margin = new Padding(0);
            discardButton.Click += (s, e) => DiscardChanges();
            footer.Controls.Add(pollingLabel, 0, 0);
            footer.Controls.Add(pollingPicker, 1, 0);
            footer.Controls.Add(statusLabel, 2, 0);
            footer.Controls.Add(discardButton, 3, 0);
            root.Controls.Add(footer, 0, 3);

            previewTimer.Tick += (s, e) => RunPreview();
            Shown += async (s, e) =>
            {
                RefreshBanners();
                await ReadKeyboard(askIfPending: false);
            };
            RefreshAll();
        }

        // ================= montagem =================

        static Button SlotButton(string text)
        {
            var b = Theme.SecondaryButton(text);
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(3);
            return b;
        }

        // Conteúdo empilhado no centro da página.
        static Panel CenteredPage(params Control[] content)
        {
            var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
            var stack = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Anchor = AnchorStyles.None };
            foreach (var control in content)
            {
                control.Anchor = AnchorStyles.None;
                stack.Controls.Add(control);
            }
            var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
            center.Controls.Add(stack);
            page.Controls.Add(center);
            return page;
        }

        void SizeKeyboard()
        {
            int width = keysPage.ClientSize.Width - keysPage.Padding.Horizontal;
            int ideal = (int)(width * keyboard.AspectRatio) + 2;
            keyboard.Height = Math.Max(120, Math.Min(ideal, (int)(keysPage.ClientSize.Height * 0.55)));
        }

        // ================= estado → tela =================

        DeviceDefinition Def => session.Definition;
        PollingKind Polling => Def?.Polling ?? PollingKind.None;
        int Hz(byte pollingCode) => PollingCodes.ToHz(Polling, pollingCode);
        ProfileState CurrentProfile => session.Working != null && session.Working.Profiles.TryGetValue(selectedSlot, out var p) ? p : null;
        bool IsLive(byte slot) => session.Device != null && session.Device.ActiveProfile == slot && session.Device.Profiles.ContainsKey(slot);

        void RefreshAll()
        {
            updatingUi = true;
            try
            {
                bool ready = session.Working != null && Def != null;
                if (ready)
                {
                    titleLabel.Text = Def.Name;
                    infoLabel.Text = $"Firmware {session.Device.Firmware}  ·  Série {session.Device.Serial}  ·  {session.Device.SlotCount} slots de perfil";
                }
                else if (!busy)
                {
                    titleLabel.Text = "Teclado não conectado";
                    infoLabel.Text = "";
                }
                RefreshProfileList();
                RefreshPolling();
                ShowPage();
                RefreshPending();
                RefreshBanners();
            }
            finally
            {
                updatingUi = false;
            }
        }

        // Cada linha mostra o estado que vai ser gravado; o ponto verde marca o que ainda não foi.
        void RefreshProfileList()
        {
            var w = session.Working;
            profileList.BeginUpdate();
            profileList.Items.Clear();
            if (w != null)
            {
                var pending = session.Pending;
                for (byte slot = 1; slot <= w.SlotCount; slot++)
                {
                    w.Profiles.TryGetValue(slot, out var p);
                    profileList.Items.Add(new ProfileSlotItem
                    {
                        Slot = slot,
                        Name = p == null ? null : string.IsNullOrEmpty(p.Name) ? $"Perfil {slot}" : p.Name,
                        Active = p != null && w.ActiveProfile == slot,
                        Pending = pending.Any(c => c.Slot == slot),
                    });
                }
                profileList.SelectedIndex = Math.Max(0, Math.Min(selectedSlot - 1, profileList.Items.Count - 1));
            }
            profileList.EndUpdate();
            RefreshSlotButtons();
        }

        // O que dá para fazer com o slot selecionado (botões e menu de clique direito).
        (bool Create, bool Rename, bool Activate, bool Delete) SlotActions()
        {
            var w = session.Working;
            bool exists = w != null && w.Profiles.ContainsKey(selectedSlot);
            bool inactive = exists && w.ActiveProfile != selectedSlot;
            return (!busy && w != null && !exists, !busy && exists, !busy && inactive, !busy && inactive);
        }

        void RefreshSlotButtons()
        {
            var can = SlotActions();
            createButton.Enabled = can.Create;
            renameButton.Enabled = can.Rename;
            activateButton.Enabled = can.Activate;
            deleteButton.Enabled = can.Delete;
        }

        // Um controle só para a taxa de polling. Na família por perfil ele mostra a taxa comum a todos
        // os perfis (ou "Varia por perfil"); na família clássica, a taxa do aparelho.
        void RefreshPolling()
        {
            bool available = session.Working != null && Def != null && Polling != PollingKind.None;
            pollingLabel.Visible = pollingPicker.Visible = available || session.Working == null;
            if (!available)
            {
                pollingPicker.SetItems(Array.Empty<string>());
                pollingPicker.EmptyText = "";
                pollingPicker.Enabled = false;
                return;
            }
            pollingPicker.SetItems(Def.PollingRates.Select(hz => $"{hz} Hz"));
            var codes = PollingCodes.PerProfile(Polling)
                ? session.Working.Profiles.Values.Select(p => p.PollingCode).Distinct().ToList()
                : new List<byte> { session.Working.PollingCode };
            int index = codes.Count == 1 && Def.OffersPollingCode(codes[0]) ? Array.IndexOf(Def.PollingRates, Hz(codes[0])) : -1;
            // Sem taxa da lista, nada fica marcado e o campo mostra o motivo.
            pollingPicker.EmptyText = codes.Count > 1 ? "Varia por perfil" : $"Desconhecida (0x{codes.FirstOrDefault():X2})";
            pollingPicker.SelectedIndex = index;
            pollingPicker.Enabled = !busy && codes.Count > 0;
        }

        void ShowPage()
        {
            Control page;
            if (session.Working == null)
            {
                offlineLabel.Text = busy ? "Lendo o teclado…" : "Não foi possível ler o teclado.";
                offlineRetryButton.Visible = !busy;
                page = offlinePage;
            }
            else if (CurrentProfile == null)
            {
                emptyCreateButton.Visible = !busy;
                page = emptyPage;
            }
            else
            {
                page = tabs.SelectedIndex == 0 ? (Control)lighting : keysPage;
            }
            tabs.Enabled = session.Working != null && CurrentProfile != null;
            if (contentHost.Controls.Count != 1 || contentHost.Controls[0] != page)
            {
                contentHost.Controls.Clear();
                contentHost.Controls.Add(page);
            }
            if (page == lighting)
                lighting.Bind(Def, CurrentProfile);
            else if (page == keysPage)
            {
                keyboard.KeyLayout = Def.Layout;
                SizeKeyboard();
                keyboard.Invalidate();
                ShowSelectedKey();
                restoreAllButton.Enabled = !busy && HasRemappedKeys(CurrentProfile);
            }
        }

        // Gravar e Descartar só ficam disponíveis com alterações pendentes; Restaurar, com alguma tecla fora do padrão.
        void RefreshPending()
        {
            bool pending = session.Pending.Count > 0;
            saveButton.Enabled = !busy && pending;
            discardButton.Visible = !busy && pending;
            readButton.Enabled = !busy;
            backupButton.Enabled = !busy && session.Device != null;
            restoreAllButton.Enabled = !busy && HasRemappedKeys(CurrentProfile);
        }

        void SetStatus(string text, Color? color = null)
        {
            statusLabel.ForeColor = color ?? Theme.MutedText;
            statusLabel.Text = text;
        }

        // Mudanças de polling iguais em vários perfis viram uma linha só.
        List<string> DescribeChanges(List<StateChange> changes)
        {
            var lines = new List<string>();
            var polling = changes.Where(c => c.Kind == ChangeKind.Polling && c.Slot != 0).ToList();
            var targets = polling.Select(c => session.Working.Profiles[c.Slot].PollingCode).Distinct().ToList();
            if (polling.Count > 1 && targets.Count == 1)
                lines.Add($"polling {Hz(targets[0])} Hz em {polling.Count} perfis");
            else
                lines.AddRange(polling.Select(DescribeChange));
            lines.AddRange(changes.Where(c => c.Kind == ChangeKind.Polling && c.Slot == 0).Select(DescribeChange));
            lines.AddRange(changes.Where(c => c.Kind != ChangeKind.Polling).Select(DescribeChange));
            return lines;
        }

        string DescribeChange(StateChange c)
        {
            var w = session.Working;
            var d = session.Device;
            switch (c.Kind)
            {
                case ChangeKind.Polling when c.Slot == 0:
                    return $"polling {Hz(d.PollingCode)} → {Hz(w.PollingCode)} Hz";
                case ChangeKind.Polling:
                {
                    var before = d.Profiles.TryGetValue(c.Slot, out var old) ? $"{Hz(old.PollingCode)} → " : "";
                    return $"perfil {c.Slot}: polling {before}{Hz(w.Profiles[c.Slot].PollingCode)} Hz";
                }
                case ChangeKind.ActiveProfile:
                    return $"perfil ativo {d.ActiveProfile} → {w.ActiveProfile}";
                case ChangeKind.CreateProfile:
                    return $"criar o perfil {c.Slot} ({w.Profiles[c.Slot].Name})";
                case ChangeKind.DeleteProfile:
                    return $"apagar o perfil {c.Slot}";
                case ChangeKind.Metadata:
                    return $"perfil {c.Slot}: nome \"{w.Profiles[c.Slot].Name}\"";
                case ChangeKind.Lighting:
                    return $"perfil {c.Slot}: iluminação {LightingEditor.EffectLabel(w.Profiles[c.Slot].Lighting.Effect).ToLowerInvariant()}";
                case ChangeKind.Brightness:
                    return $"perfil {c.Slot}: brilho {Math.Round(w.Profiles[c.Slot].Brightness * 100 / 255.0)} %";
                case ChangeKind.Key:
                {
                    var name = Def?.FindKey(c.Key)?.Label ?? $"0x{c.Key:X2}";
                    var to = w.Profiles[c.Slot].Layer(c.Layer)[c.Key];
                    return $"perfil {c.Slot}: tecla {name}{(c.Layer == 1 ? " (camada Fn)" : "")} → {HidUsages.Describe(to, false)}";
                }
                default:
                    return c.ToString();
            }
        }

        void RefreshBanners()
        {
            banners.SuspendLayout();
            foreach (Control old in banners.Controls.Cast<Control>().ToList())
                old.Dispose();
            banners.Controls.Clear();
            bannerLabels.Clear();
            var razer = SystemChecks.RazerProcesses();
            if (razer.Count > 0)
                AddBanner($"O Razer Synapse está em execução ({string.Join(", ", razer)}). Ele disputa o controle do teclado: feche-o antes de gravar.",
                    "Verificar de novo", (s, e) => RefreshBanners());
            if (SystemChecks.DynamicLightingEnabled())
                AddBanner("A Iluminação dinâmica do Windows está ligada e pode substituir os efeitos de luz do teclado. " +
                          "Para desligar: Configurações › Personalização › Iluminação dinâmica.", null, null);
            if (session.Device != null && session.Device.DeviceMode != 0x00)
                AddBanner($"O teclado está em modo driver (0x{session.Device.DeviceMode:X2}): um programa controla a luz e o firmware não aplica os efeitos dos perfis.",
                    "Voltar ao modo normal", async (s, e) => await SetNormalMode());
            banners.ResumeLayout();
        }

        // Aviso em cartão amarelo, da largura dos cartões de baixo.
        void AddBanner(string text, string action, EventHandler onAction)
        {
            var card = new Card
            {
                BackColor = Theme.WarningBack, BorderColor = Theme.WarningBorder, Dock = DockStyle.Fill,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 12, 0, 0), Padding = new Padding(14, 8, 8, 8),
            };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var label = Theme.Label("⚠  " + text, Theme.Base, Theme.WarningText);
            label.Anchor = AnchorStyles.Left;
            label.MaximumSize = new Size(BannerTextWidth, 0);
            bannerLabels.Add(label);
            layout.Controls.Add(label, 0, 0);
            if (action != null)
            {
                var b = Theme.SecondaryButton(action);
                b.Anchor = AnchorStyles.Right;
                b.Margin = new Padding(12, 0, 0, 0);
                b.Click += onAction;
                layout.Controls.Add(b, 1, 0);
            }
            card.Controls.Add(layout);
            banners.Controls.Add(card);
        }

        // Largura do texto do aviso: a janela menos as margens, o recuo do cartão e o botão.
        int BannerTextWidth => Math.Max(200, ClientSize.Width - 2 * Gutter - 260);

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            foreach (var label in bannerLabels)
                label.MaximumSize = new Size(BannerTextWidth, 0);
        }

        // ================= eventos de edição =================

        void OnProfileSelected()
        {
            if (updatingUi || profileList.SelectedIndex < 0)
                return;
            selectedSlot = (byte)(profileList.SelectedIndex + 1);
            keyboard.SelectedKey = null;
            RefreshSlotButtons();
            ShowPage();
        }

        void OnLightingChanged()
        {
            var p = CurrentProfile;
            if (p == null)
                return;
            p.Lighting = lighting.Effect;
            p.Brightness = lighting.Brightness;
            AfterEdit();
            if (IsLive(p.Slot))
            {
                previewTimer.Stop();
                previewTimer.Start();
            }
        }

        void RunPreview()
        {
            previewTimer.Stop();
            var p = CurrentProfile;
            if (p == null || busy || !IsLive(p.Slot))
                return;
            if (previewRunning)
            {
                previewTimer.Start();   // tenta de novo quando o envio anterior terminar
                return;
            }
            previewRunning = true;
            var effect = p.Lighting;
            var brightness = p.Brightness;
            Task.Run(() => session.Preview(effect, brightness)).ContinueWith(t =>
            {
                previewRunning = false;
                if (t.Exception != null)
                    SetStatus("Não foi possível mostrar a prévia no teclado: " + t.Exception.InnerException?.Message, Theme.Danger);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // A tecla Fn (fixa) aparece como padrão: o editor mostra que ela não muda.
        KeyLook DescribeKey(byte id)
        {
            var p = CurrentProfile;
            if (p == null || Def == null || Def.FixedKeys.Contains(id) || !p.Keys.TryGetValue(id, out var b))
                return KeyLook.Default;
            if (b.IsDisabled)
                return KeyLook.Disabled;
            var factory = Def.DefaultFor(id);
            return factory != null && b.SameAction(factory) ? KeyLook.Default : KeyLook.Remapped;
        }

        // Alguma tecla (fora a Fn) com função diferente da de fábrica.
        bool HasRemappedKeys(ProfileState p) =>
            p != null && Def != null && Def.DefaultKeys.Any(kv =>
                !Def.FixedKeys.Contains(kv.Key) && p.Keys.TryGetValue(kv.Key, out var b) && !b.SameAction(kv.Value));

        void ShowSelectedKey()
        {
            var p = CurrentProfile;
            byte? id = keyboard.SelectedKey;
            KeyBinding binding = null;
            if (p != null && id.HasValue)
                p.Keys.TryGetValue(id.Value, out binding);
            keyEditor.ShowKey(Def, id, binding);
        }

        void OnBindingChosen(byte key, KeyBinding binding)
        {
            var p = CurrentProfile;
            if (p == null)
                return;
            p.Keys[key] = binding;
            keyboard.Invalidate();
            AfterEdit();
        }

        void RestoreAllKeys()
        {
            var p = CurrentProfile;
            if (p == null)
                return;
            foreach (var kv in Def.DefaultKeys)
            {
                if (Def.FixedKeys.Contains(kv.Key) || !p.Keys.TryGetValue(kv.Key, out var b) || b.SameAction(kv.Value))
                    continue;
                p.Keys[kv.Key] = kv.Value;
            }
            keyboard.Invalidate();
            ShowSelectedKey();
            AfterEdit();
        }

        void OnPollingChanged()
        {
            if (updatingUi || session.Working == null || Polling == PollingKind.None ||
                pollingPicker.SelectedIndex < 0 || pollingPicker.SelectedIndex >= Def.PollingRates.Length)
                return;
            byte code = PollingCodes.ToCode(Polling, Def.PollingRates[pollingPicker.SelectedIndex]);
            if (PollingCodes.PerProfile(Polling))
            {
                // O teclado guarda a taxa em cada perfil; aqui ela vale para todos.
                foreach (var p in session.Working.Profiles.Values)
                    p.PollingCode = code;
            }
            else
            {
                session.Working.PollingCode = code;
            }
            AfterEdit();
            updatingUi = true;
            try
            {
                RefreshPolling();
            }
            finally
            {
                updatingUi = false;
            }
        }

        // Toda edição apaga a mensagem anterior do rodapé, que já não vale.
        void AfterEdit()
        {
            updatingUi = true;
            try
            {
                int index = profileList.SelectedIndex;
                RefreshProfileList();
                profileList.SelectedIndex = index;
                RefreshPending();
                SetStatus("");
            }
            finally
            {
                updatingUi = false;
            }
        }

        // ================= perfis =================

        void ActivateSlot()
        {
            if (!SlotActions().Activate)
                return;
            session.Working.ActiveProfile = selectedSlot;
            AfterEdit();
            RefreshSlotButtons();
        }

        void RenameSlot()
        {
            var p = CurrentProfile;
            if (p == null || !SlotActions().Rename)
                return;
            var name = NameDialog.Ask(this, $"Renomear o perfil {p.Slot}", p.Name);
            if (name == null || name == p.Name)
                return;
            p.Metadata = ProfileMetadata.EncodeName(name, p.Metadata);
            AfterEdit();
        }

        void CreateSlot()
        {
            if (!SlotActions().Create)
                return;
            var name = NameDialog.Ask(this, $"Criar perfil no slot {selectedSlot}", $"Perfil {selectedSlot}");
            if (name == null)
                return;
            // Recriar um slot que só foi apagado nesta sessão reaproveita o formato dos metadados dele.
            session.Device.Profiles.TryGetValue(selectedSlot, out var old);
            var p = Def.NewProfileTemplate(selectedSlot);
            p.Metadata = ProfileMetadata.EncodeName(name, old?.Metadata);
            var codes = session.Working.Profiles.Values.Select(x => x.PollingCode).Distinct().ToList();
            if (PollingCodes.PerProfile(Polling) && codes.Count == 1)
                p.PollingCode = codes[0];   // mantém a mesma taxa em todos os perfis
            session.Working.Profiles[selectedSlot] = p;
            AfterEdit();
            ShowPage();
        }

        // O perfil ativo não pode ser apagado: o botão e o item de menu ficam desabilitados.
        void DeleteSlot()
        {
            var p = CurrentProfile;
            if (p == null || !SlotActions().Delete)
                return;
            if (MessageBox.Show(this, $"Apagar o perfil {p.Slot} ({p.Name})?", AppName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            session.Working.Profiles.Remove(p.Slot);
            AfterEdit();
            ShowPage();
        }

        // ================= operações com o teclado =================

        async Task<bool> RunDeviceTask(string message, Action<Action<string>> work)
        {
            SetBusy(true);
            SetStatus(message);
            void Progress(string m) => BeginInvoke(new Action(() => SetStatus(m)));
            try
            {
                await Task.Run(() => work(Progress));
                return true;
            }
            catch (Exception ex)
            {
                SetBusy(false);
                SetStatus(ex.Message, Theme.Danger);
                MessageBox.Show(this, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            finally
            {
                SetBusy(false);
            }
        }

        void SetBusy(bool value)
        {
            busy = value;
            UseWaitCursor = value;
            profileList.Enabled = !value;
            lighting.Enabled = !value;
            keysPage.Enabled = !value;
            pollingPicker.Enabled = !value && session.Working != null;
            emptyCreateButton.Enabled = !value;
            RefreshSlotButtons();
            RefreshPending();
        }

        async Task ReadKeyboard(bool askIfPending)
        {
            if (busy)
                return;
            if (askIfPending && session.Pending.Count > 0 &&
                MessageBox.Show(this, "Descartar as alterações não gravadas e ler o teclado de novo?", AppName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            previewTimer.Stop();
            bool ok = await RunDeviceTask("Lendo o teclado…", progress =>
            {
                session.EndPreview();
                session.Read(progress);
            });
            if (ok && session.Device != null && !session.Device.Profiles.ContainsKey(selectedSlot))
                selectedSlot = session.Device.ActiveProfile;
            RefreshAll();
            if (ok)
                SetStatus("");
        }

        async Task SaveKeyboard()
        {
            if (busy || session.Pending.Count == 0)
                return;
            var razer = SystemChecks.RazerProcesses();
            if (razer.Count > 0 &&
                MessageBox.Show(this, $"O Razer Synapse está em execução ({string.Join(", ", razer)}) e pode desfazer ou atrapalhar a gravação.\n\nGravar mesmo assim?",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            previewTimer.Stop();
            DeviceSync.SaveResult result = null;
            bool ok = await RunDeviceTask("Gravando no teclado…", progress => result = session.Save(progress));
            if (!ok)
            {
                // a gravação parou no meio: relê para mostrar o que ficou no teclado
                await ReadKeyboard(askIfPending: false);
                return;
            }
            RefreshAll();
            if (result.Mismatches.Count == 0)
            {
                SetStatus($"Gravado e conferido às {DateTime.Now:HH:mm}.", Theme.Accent);
            }
            else
            {
                var lines = string.Join("\n", DescribeChanges(result.Mismatches).Take(20));
                SetStatus($"{result.Mismatches.Count} itens não conferiram depois de gravar.", Theme.Danger);
                MessageBox.Show(this, "Depois de gravar e reler, estes itens não ficaram como pedido:\n\n" + lines, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        async Task SetNormalMode()
        {
            if (await RunDeviceTask("Voltando o teclado ao modo normal…", p => session.SetNormalMode()))
                SetStatus("");
            RefreshBanners();
        }

        void DiscardChanges()
        {
            if (busy)
                return;
            previewTimer.Stop();
            session.Discard();
            if (session.PreviewActive)
                Task.Run(() => session.EndPreview());
            RefreshAll();
            SetStatus("");
        }

        // ================= backup =================

        void ExportBackup()
        {
            if (session.Device == null)
                return;
            using (var dialog = new SaveFileDialog
            {
                Title = "Exportar backup dos perfis",
                Filter = "Backup JSON (*.json)|*.json",
                FileName = $"backup-{Def.Name.Replace("Razer ", "").Replace(' ', '-').ToLowerInvariant()}-{DateTime.Now:yyyy-MM-dd-HHmm}.json",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    var header = new BackupFile.Header { DeviceName = Def.Name, VendorId = Def.VendorId, ProductId = Def.ProductId };
                    File.WriteAllText(dialog.FileName, BackupFile.Serialize(session.Device, header), new UTF8Encoding(false));
                    SetStatus($"Backup exportado: {Path.GetFileName(dialog.FileName)}.", Theme.Accent);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Não foi possível salvar o backup: " + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        void ImportBackup()
        {
            if (session.Device == null)
                return;
            using (var dialog = new OpenFileDialog
            {
                Title = "Importar backup dos perfis",
                Filter = "Backup JSON (*.json)|*.json|Todos os arquivos (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                DeviceState backup;
                BackupFile.Header header;
                try
                {
                    backup = BackupFile.Deserialize(File.ReadAllText(dialog.FileName, Encoding.UTF8), out header);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Este arquivo não pôde ser lido como backup: " + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (header.VendorId != Def.VendorId || header.ProductId != Def.ProductId)
                {
                    MessageBox.Show(this, $"Este backup é de outro modelo ({header.DeviceName}, PID {header.ProductId:X4}).", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (backup.Profiles.Keys.Any(slot => slot > session.Device.SlotCount))
                {
                    MessageBox.Show(this, "O backup usa slots que este teclado não tem.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var names = string.Join("\n", backup.Profiles.Values.Select(p => $"   {p.Slot}. {p.Name}"));
                if (MessageBox.Show(this, $"Carregar o backup de {header.CreatedAt} (série {header.Serial})?\n\nPerfis:\n{names}",
                        AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                try
                {
                    session.LoadBackup(backup);
                }
                catch (FormatException ex)
                {
                    MessageBox.Show(this, "Este backup não pode ser aplicado neste teclado: " + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                RefreshAll();
                SetStatus(session.Pending.Count == 0 ? "O teclado já está igual ao backup." : "Backup carregado.", Theme.Accent);
            }
        }

        // ================= janela =================

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.S)
            {
                e.SuppressKeyPress = true;
                _ = SaveKeyboard();
            }
            else if (e.KeyCode == Keys.F5)
            {
                e.SuppressKeyPress = true;
                _ = ReadKeyboard(askIfPending: true);
            }
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel)
                return;
            if (busy)
            {
                e.Cancel = true;
                return;
            }
            if (session.Pending.Count > 0)
            {
                var answer = MessageBox.Show(this, "Há alterações que ainda não foram gravadas no teclado.\n\nGravar antes de sair?", AppName,
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (answer == DialogResult.Yes)
                {
                    e.Cancel = true;
                    await SaveKeyboard();
                    if (session.Pending.Count == 0)
                        Close();
                    return;
                }
            }
            previewTimer.Stop();
            session.EndPreview();
            session.Dispose();
        }
    }
}
