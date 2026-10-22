using System.Collections.Generic;
using OpenRazerProfiler.Protocolo;

namespace OpenRazerProfiler.Aparelhos
{
    // Razer Huntsman V2 (teclado completo, layout US). Valores conferidos no aparelho, firmware 1.00.
    public static class HuntsmanV2
    {
        public static readonly DeviceDefinition Definition = new DeviceDefinition
        {
            Name = "Razer Huntsman V2",
            ProductId = 0x026C,
            ControlInterface = 3,
            TransactionId = 0x1F,
            BacklightLed = 0x05,

            Effects = new[]
            {
                EffectId.Off, EffectId.Static, EffectId.Breathing, EffectId.Spectrum,
                EffectId.Wave, EffectId.Reactive, EffectId.Starlight,
            },
            WaveLeftToRight = 0x01,
            WaveRightToLeft = 0x02,
            WaveSpeeds = new byte[] { 0x80, 0x60, 0x48, 0x38, 0x28, 0x20, 0x18, 0x10, 0x08 },
            WaveDefaultSpeed = 0x28,
            ReactiveSpeedMin = 1,
            ReactiveSpeedMax = 4,
            StarlightSpeedMin = 1,
            StarlightSpeedMax = 3,

            // 8000 Hz exige USB High Speed; conferido: endpoints com intervalo de 125 µs.
            Polling = PollingKind.HyperPollingPerProfile,
            PollingRates = new[] { 125, 250, 500, 1000, 2000, 4000, 8000 },

            Layout = BuildLayout(),
            DefaultKeys = KeyTable.Parse(DefaultBase),
            DefaultFnKeys = BuildFnLayer(),
            FixedKeys = new HashSet<byte> { 0x3B },   // Fn

            NewProfileLighting = new LightingEffect(EffectId.Spectrum, 0, 0, new[] { new Rgb(0x6E, 0xDA, 0xFD) }),
            NewProfileBrightness = 0xFF,
            NewProfilePollingCode = 0x01,   // 8000 Hz
        };

        // Camada base de fábrica (perfil 1), lida do aparelho. key_id:tipo tamanho dados.
        // 0E, 2A, 2D, 38, 6B e 96–9A são teclas ISO/JIS que não existem no layout US.
        // A0–AF são funções internas do firmware (tipo 0x11, significado desconhecido).
        const string DefaultBase =
            "01:02020035 02:0202001e 03:0202001f 04:02020020 05:02020021 06:02020022 07:02020023 08:02020024 09:02020025 0A:02020026 0B:02020027 0C:0202002d " +
            "0D:0202002e 0E:02020089 0F:0202002a 10:0202002b 11:02020014 12:0202001a 13:02020008 14:02020015 15:02020017 16:0202001c 17:02020018 18:0202000c " +
            "19:02020012 1A:02020013 1B:0202002f 1C:02020030 1D:02020031 1E:02020039 1F:02020004 20:02020016 21:02020007 22:02020009 23:0202000a 24:0202000b " +
            "25:0202000d 26:0202000e 27:0202000f 28:02020033 29:02020034 2A:02020032 2B:02020028 2C:020200e1 2D:02020064 2E:0202001d 2F:0202001b 30:02020006 " +
            "31:02020019 32:02020005 33:02020011 34:02020010 35:02020036 36:02020037 37:02020038 38:02020087 39:020200e5 3A:020200e0 3B:110101 3C:020200e2 " +
            "3D:0202002c 3E:020200e6 40:020200e4 4B:02020049 4C:0202004c 4F:02020050 50:0202004a 51:0202004d 53:02020052 54:02020051 55:0202004b 56:0202004e " +
            "59:0202004f 5A:02020053 5B:0202005f 5C:0202005c 5D:02020059 5F:02020054 60:02020060 61:0202005d 62:0202005a 63:02020062 64:02020055 65:02020061 " +
            "66:0202005e 67:0202005b 68:02020063 69:02020056 6A:02020057 6B:02020085 6C:02020058 6E:02020029 70:0202003a 71:0202003b 72:0202003c 73:0202003d " +
            "74:0202003e 75:0202003f 76:02020040 77:02020041 78:02020042 79:02020043 7A:02020044 7B:02020045 7C:02020046 7D:02020047 7E:02020048 7F:02020800 " +
            "81:02020065 96:0202008b 97:02020088 98:02020091 99:02020090 9A:0202008a A0:110120 A1:110121 A2:110122 A3:110123 A4:110124 A9:110125 " +
            "AA:110126 AB:110127 AC:110128 AD:110129 AE:11012a AF:11012b C8:0a0200e2 C9:0a0200b6 CA:0a0200b5 CC:0a0200e9 CD:0a0200cd CE:0a0200ea";

        // Camada Fn de fábrica: igual à base, exceto estas teclas.
        // F9 0x11/04 (gravar macro), F10 0x11/03 (modo jogo), F11 0x11/09 (brilho −), F12 0x11/08 (brilho +),
        // Pause 0x09/82 (suspender), Menu 0x07/04 (desconhecido).
        const string FnOverrides = "78:110104 79:110103 7A:110109 7B:110108 7E:090182 81:070104";

        static SortedDictionary<byte, KeyBinding> BuildFnLayer()
        {
            var fn = KeyTable.Parse(DefaultBase);
            foreach (var kv in KeyTable.Parse(FnOverrides))
                fn[kv.Key] = kv.Value;
            return fn;
        }

        static IReadOnlyList<PhysicalKey> BuildLayout()
        {
            var b = new LayoutBuilder();

            b.Row(0).Key(0x6E, "Esc").Gap(1)
                .Key(0x70, "F1").Key(0x71, "F2").Key(0x72, "F3").Key(0x73, "F4").Gap(0.5f)
                .Key(0x74, "F5").Key(0x75, "F6").Key(0x76, "F7").Key(0x77, "F8").Gap(0.5f)
                .Key(0x78, "F9").Key(0x79, "F10").Key(0x7A, "F11").Key(0x7B, "F12")
                .At(15.25f).Key(0x7C, "PrtSc").Key(0x7D, "ScrLk").Key(0x7E, "Pause")
                .At(18.5f).Key(0xC9, "|◀", 0.75f).Key(0xCD, "▶‖", 0.75f).Key(0xCA, "▶|", 0.75f).Key(0xC8, "Mudo", 0.75f)
                .KeyAt(0xCC, "Vol +", 21.5f, 0, 1, 0.5f)
                .KeyAt(0xCE, "Vol −", 21.5f, 0.5f, 1, 0.5f);

            b.Row(1.5f).Key(0x01, "`").Key(0x02, "1").Key(0x03, "2").Key(0x04, "3").Key(0x05, "4").Key(0x06, "5")
                .Key(0x07, "6").Key(0x08, "7").Key(0x09, "8").Key(0x0A, "9").Key(0x0B, "0").Key(0x0C, "-").Key(0x0D, "=")
                .Key(0x0F, "Backspace", 2)
                .At(15.25f).Key(0x4B, "Ins").Key(0x50, "Home").Key(0x55, "PgUp")
                .At(18.5f).Key(0x5A, "Num").Key(0x5F, "/").Key(0x64, "*").Key(0x69, "-");

            b.Row(2.5f).Key(0x10, "Tab", 1.5f).Key(0x11, "Q").Key(0x12, "W").Key(0x13, "E").Key(0x14, "R").Key(0x15, "T")
                .Key(0x16, "Y").Key(0x17, "U").Key(0x18, "I").Key(0x19, "O").Key(0x1A, "P").Key(0x1B, "[").Key(0x1C, "]")
                .Key(0x1D, "\\", 1.5f)
                .At(15.25f).Key(0x4C, "Del").Key(0x51, "End").Key(0x56, "PgDn")
                .At(18.5f).Key(0x5B, "7").Key(0x60, "8").Key(0x65, "9").Key(0x6A, "+", 1, 2);

            b.Row(3.5f).Key(0x1E, "Caps Lock", 1.75f).Key(0x1F, "A").Key(0x20, "S").Key(0x21, "D").Key(0x22, "F")
                .Key(0x23, "G").Key(0x24, "H").Key(0x25, "J").Key(0x26, "K").Key(0x27, "L").Key(0x28, ";").Key(0x29, "'")
                .Key(0x2B, "Enter", 2.25f)
                .At(18.5f).Key(0x5C, "4").Key(0x61, "5").Key(0x66, "6");

            b.Row(4.5f).Key(0x2C, "Shift", 2.25f).Key(0x2E, "Z").Key(0x2F, "X").Key(0x30, "C").Key(0x31, "V").Key(0x32, "B")
                .Key(0x33, "N").Key(0x34, "M").Key(0x35, ",").Key(0x36, ".").Key(0x37, "/").Key(0x39, "Shift", 2.75f)
                .At(16.25f).Key(0x53, "↑")
                .At(18.5f).Key(0x5D, "1").Key(0x62, "2").Key(0x67, "3").Key(0x6C, "Enter", 1, 2);

            b.Row(5.5f).Key(0x3A, "Ctrl", 1.25f).Key(0x7F, "Win", 1.25f).Key(0x3C, "Alt", 1.25f).Key(0x3D, "Espaço", 6.25f)
                .Key(0x3E, "Alt", 1.25f).Key(0x3B, "Fn", 1.25f).Key(0x81, "Menu", 1.25f).Key(0x40, "Ctrl", 1.25f)
                .At(15.25f).Key(0x4F, "←").Key(0x54, "↓").Key(0x59, "→")
                .At(18.5f).Key(0x63, "0", 2).Key(0x68, ".");

            return b.Build();
        }
    }
}
