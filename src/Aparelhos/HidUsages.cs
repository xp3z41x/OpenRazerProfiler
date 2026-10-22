using System.Collections.Generic;
using System.Linq;
using OpenRazerProfiler.Protocolo;

namespace OpenRazerProfiler.Aparelhos
{
    public sealed class UsageInfo
    {
        public UsageInfo(byte usage, string shortName, string longName, string group)
        {
            Usage = usage;
            ShortName = shortName;
            LongName = longName;
            Group = group;
        }

        public byte Usage { get; }
        public string ShortName { get; }   // para a tecla desenhada
        public string LongName { get; }    // para listas
        public string Group { get; }
        public override string ToString() => LongName;
    }

    public sealed class MediaInfo
    {
        public MediaInfo(KeyBinding binding, string name)
        {
            Binding = binding;
            Name = name;
        }

        public KeyBinding Binding { get; }
        public string Name { get; }
        public override string ToString() => Name;
    }

    // Nomes em português das usages HID que o firmware aceita (comuns a todos os teclados).
    public static class HidUsages
    {
        public static readonly IReadOnlyList<UsageInfo> KeyboardKeys = BuildKeyboard();

        static readonly Dictionary<byte, UsageInfo> byUsage = KeyboardKeys.ToDictionary(u => u.Usage);

        // Teclas de mídia (Consumer, tipo 0x0A) e controle de sistema (tipo 0x09).
        public static readonly IReadOnlyList<MediaInfo> MediaKeys = new[]
        {
            new MediaInfo(KeyBinding.Consumer(0x00CD), "Reproduzir/Pausar"),
            new MediaInfo(KeyBinding.Consumer(0x00B5), "Próxima faixa"),
            new MediaInfo(KeyBinding.Consumer(0x00B6), "Faixa anterior"),
            new MediaInfo(KeyBinding.Consumer(0x00B7), "Parar"),
            new MediaInfo(KeyBinding.Consumer(0x00E2), "Mudo"),
            new MediaInfo(KeyBinding.Consumer(0x00E9), "Volume +"),
            new MediaInfo(KeyBinding.Consumer(0x00EA), "Volume −"),
            new MediaInfo(KeyBinding.Consumer(0x0192), "Calculadora"),
            new MediaInfo(KeyBinding.Consumer(0x0196), "Navegador"),
            new MediaInfo(KeyBinding.Consumer(0x018A), "E-mail"),
            new MediaInfo(KeyBinding.Consumer(0x0194), "Explorador de arquivos"),
            new MediaInfo(KeyBinding.SystemControl(0x82), "Suspender o computador"),
        };

        // Bits da máscara de modificadores.
        public static readonly (byte Bit, string Name)[] Modifiers =
        {
            (0x01, "Ctrl"), (0x02, "Shift"), (0x04, "Alt"), (0x08, "Win"),
            (0x10, "Ctrl dir."), (0x20, "Shift dir."), (0x40, "Alt dir."), (0x80, "Win dir."),
        };

        public static UsageInfo Find(byte usage) => byUsage.TryGetValue(usage, out var u) ? u : null;

        public static string KeyName(byte usage, bool shortForm)
        {
            var u = Find(usage);
            if (u != null)
                return shortForm ? u.ShortName : u.LongName;
            return $"0x{usage:X2}";
        }

        public static string MediaName(KeyBinding binding)
        {
            var m = MediaKeys.FirstOrDefault(x => x.Binding.Equals(binding));
            if (m != null)
                return m.Name;
            return binding.IsConsumer ? $"Mídia 0x{binding.ConsumerUsage:X4}" : $"Sistema 0x{binding.SystemUsage:X2}";
        }

        // Descrição de uma ação. shortForm = texto curto para caber na tecla desenhada.
        public static string Describe(KeyBinding b, bool shortForm)
        {
            if (b == null)
                return "?";
            switch (b.Type)
            {
                case KeyBinding.TypeDisabled:
                    return shortForm ? "—" : "Desativada";
                case KeyBinding.TypeKeyboard when b.IsKeyboard:
                {
                    var n = b.Normalized();
                    var parts = new List<string>();
                    foreach (var (bit, name) in Modifiers)
                        if ((n.Modifiers & bit) != 0)
                            parts.Add(name);
                    if (n.Usage != 0)
                        parts.Add(KeyName(n.Usage, shortForm));
                    return parts.Count == 0 ? (shortForm ? "—" : "Nada") : string.Join("+", parts);
                }
                case KeyBinding.TypeConsumer when b.IsConsumer:
                case KeyBinding.TypeSystem when b.IsSystem:
                    return MediaName(b);
                case KeyBinding.TypeSpecial when b.DataLength == 1 && b[0] == 0x01:
                    return "Fn";
                default:
                    return shortForm ? "Especial" : $"Função do firmware (tipo 0x{b.Type:X2}: {Hex.Format(b.ToEntry().Skip(2).ToArray())})";
            }
        }

        static IReadOnlyList<UsageInfo> BuildKeyboard()
        {
            var list = new List<UsageInfo>();
            void Add(byte usage, string shortName, string longName, string group) =>
                list.Add(new UsageInfo(usage, shortName, longName ?? shortName, group));

            for (int i = 0; i < 26; i++)
                Add((byte)(0x04 + i), ((char)('A' + i)).ToString(), null, "Letras");
            for (int i = 0; i < 10; i++)
                Add((byte)(0x1E + i), ((i + 1) % 10).ToString(), null, "Números");
            for (int i = 0; i < 12; i++)
                Add((byte)(0x3A + i), $"F{i + 1}", null, "Funções");
            for (int i = 0; i < 12; i++)
                Add((byte)(0x68 + i), $"F{i + 13}", null, "Funções");

            Add(0x29, "Esc", null, "Edição");
            Add(0x28, "Enter", null, "Edição");
            Add(0x2A, "Backspace", null, "Edição");
            Add(0x2B, "Tab", null, "Edição");
            Add(0x2C, "Espaço", null, "Edição");
            Add(0x39, "Caps Lock", null, "Edição");
            Add(0x49, "Ins", "Insert", "Edição");
            Add(0x4C, "Del", "Delete", "Edição");
            Add(0x65, "Menu", "Menu (aplicativo)", "Edição");

            Add(0x4A, "Home", null, "Navegação");
            Add(0x4D, "End", null, "Navegação");
            Add(0x4B, "PgUp", "Page Up", "Navegação");
            Add(0x4E, "PgDn", "Page Down", "Navegação");
            Add(0x52, "↑", "Seta para cima", "Navegação");
            Add(0x51, "↓", "Seta para baixo", "Navegação");
            Add(0x50, "←", "Seta para a esquerda", "Navegação");
            Add(0x4F, "→", "Seta para a direita", "Navegação");
            Add(0x46, "PrtSc", "Print Screen", "Navegação");
            Add(0x47, "ScrLk", "Scroll Lock", "Navegação");
            Add(0x48, "Pause", "Pause/Break", "Navegação");

            Add(0x35, "`", "` (acento grave)", "Símbolos");
            Add(0x2D, "-", "- (menos)", "Símbolos");
            Add(0x2E, "=", "= (igual)", "Símbolos");
            Add(0x2F, "[", "[ (colchete)", "Símbolos");
            Add(0x30, "]", "] (colchete)", "Símbolos");
            Add(0x31, "\\", "\\ (barra invertida)", "Símbolos");
            Add(0x33, ";", "; (ponto e vírgula)", "Símbolos");
            Add(0x34, "'", "' (apóstrofo)", "Símbolos");
            Add(0x36, ",", ", (vírgula)", "Símbolos");
            Add(0x37, ".", ". (ponto)", "Símbolos");
            Add(0x38, "/", "/ (barra)", "Símbolos");

            Add(0x53, "Num", "Num Lock", "Teclado numérico");
            for (int i = 0; i < 9; i++)
                Add((byte)(0x59 + i), $"Num {i + 1}", null, "Teclado numérico");
            Add(0x62, "Num 0", null, "Teclado numérico");
            Add(0x63, "Num .", null, "Teclado numérico");
            Add(0x54, "Num /", null, "Teclado numérico");
            Add(0x55, "Num *", null, "Teclado numérico");
            Add(0x56, "Num -", null, "Teclado numérico");
            Add(0x57, "Num +", null, "Teclado numérico");
            Add(0x58, "Num Enter", null, "Teclado numérico");
            Add(0x85, "Num ,", "Num , (vírgula, ABNT2)", "Teclado numérico");

            Add(0xE0, "Ctrl", "Ctrl esquerdo", "Modificadores");
            Add(0xE1, "Shift", "Shift esquerdo", "Modificadores");
            Add(0xE2, "Alt", "Alt esquerdo", "Modificadores");
            Add(0xE3, "Win", "Win esquerdo", "Modificadores");
            Add(0xE4, "Ctrl dir.", "Ctrl direito", "Modificadores");
            Add(0xE5, "Shift dir.", "Shift direito", "Modificadores");
            Add(0xE6, "Alt dir.", "Alt direito (AltGr)", "Modificadores");
            Add(0xE7, "Win dir.", "Win direito", "Modificadores");

            Add(0x87, "/ ?", "/ ? (ABNT2, Intl1)", "Internacionais");
            Add(0x32, "# ~", "# ~ (ISO)", "Internacionais");
            Add(0x64, "\\ |", "\\ | (ISO, ao lado do Shift)", "Internacionais");
            Add(0x89, "¥", "¥ (JIS, Intl3)", "Internacionais");
            Add(0x88, "Kana", "Katakana/Hiragana (JIS)", "Internacionais");
            Add(0x8A, "Henkan", "Henkan (JIS)", "Internacionais");
            Add(0x8B, "Muhenkan", "Muhenkan (JIS)", "Internacionais");
            Add(0x90, "Lang1", "Lang1 (Hangul)", "Internacionais");
            Add(0x91, "Lang2", "Lang2 (Hanja)", "Internacionais");
            return list;
        }
    }
}
