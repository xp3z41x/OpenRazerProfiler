using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRazerProfiler.Protocolo
{
    // IDs de efeito da classe 0x0F (matriz estendida).
    public static class EffectId
    {
        public const byte Off = 0x00;
        public const byte Static = 0x01;
        public const byte Breathing = 0x02;
        public const byte Spectrum = 0x03;
        public const byte Wave = 0x04;
        public const byte Reactive = 0x05;
        public const byte Starlight = 0x07;
        public const byte Custom = 0x08;
    }

    // Modos do breathing (Param1).
    public static class BreathingMode
    {
        public const byte Single = 0x01;
        public const byte Dual = 0x02;
        public const byte Random = 0x03;
    }

    public struct Rgb : IEquatable<Rgb>
    {
        public Rgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public bool Equals(Rgb other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object obj) => obj is Rgb other && Equals(other);
        public override int GetHashCode() => R << 16 | G << 8 | B;
        public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
    }

    // Efeito do firmware como o teclado guarda: [efeito, p1, p2, nº de cores, RGB...].
    // p1: modo do breathing ou direção do wave. p2: velocidade (wave, reactive, starlight).
    public sealed class LightingEffect : IEquatable<LightingEffect>
    {
        public LightingEffect(byte effect, byte param1, byte param2, IEnumerable<Rgb> colors)
        {
            Effect = effect;
            Param1 = param1;
            Param2 = param2;
            Colors = (colors ?? Enumerable.Empty<Rgb>()).ToArray();
            if (Colors.Count > 24)
                throw new ArgumentException("Cores demais para um efeito.");
        }

        public byte Effect { get; }
        public byte Param1 { get; }
        public byte Param2 { get; }
        public IReadOnlyList<Rgb> Colors { get; }

        public static LightingEffect Off() => new LightingEffect(EffectId.Off, 0, 0, null);
        public static LightingEffect Static(Rgb c) => new LightingEffect(EffectId.Static, 0, 0, new[] { c });
        public static LightingEffect BreathingSingle(Rgb c) => new LightingEffect(EffectId.Breathing, BreathingMode.Single, 0, new[] { c });
        public static LightingEffect BreathingDual(Rgb c1, Rgb c2) => new LightingEffect(EffectId.Breathing, BreathingMode.Dual, 0, new[] { c1, c2 });
        public static LightingEffect BreathingRandom() => new LightingEffect(EffectId.Breathing, BreathingMode.Random, 0, null);
        public static LightingEffect Spectrum() => new LightingEffect(EffectId.Spectrum, 0, 0, null);
        public static LightingEffect Wave(byte direction, byte speed) => new LightingEffect(EffectId.Wave, direction, speed, null);
        public static LightingEffect Reactive(byte speed, Rgb c) => new LightingEffect(EffectId.Reactive, 0, speed, new[] { c });
        public static LightingEffect Starlight(byte speed, params Rgb[] colors) => new LightingEffect(EffectId.Starlight, 0, speed, colors);

        // data = argumentos da resposta de 0F 82: [onde, led, efeito, p1, p2, n, RGB...]
        public static LightingEffect FromResponse(byte[] data) => FromBytes(data.Skip(2).ToArray());

        // bytes = [efeito, p1, p2, n, RGB...]
        public static LightingEffect FromBytes(byte[] bytes)
        {
            if (bytes.Length < 1)
                throw new FormatException("Efeito vazio.");
            byte p1 = bytes.Length > 1 ? bytes[1] : (byte)0;
            byte p2 = bytes.Length > 2 ? bytes[2] : (byte)0;
            int n = bytes.Length > 3 ? bytes[3] : 0;
            var colors = new List<Rgb>();
            for (int i = 0; i < n && 4 + 3 * i + 2 < bytes.Length; i++)
                colors.Add(new Rgb(bytes[4 + 3 * i], bytes[5 + 3 * i], bytes[6 + 3 * i]));
            return new LightingEffect(bytes[0], p1, p2, colors);
        }

        public byte[] ToBytes()
        {
            var b = new byte[4 + 3 * Colors.Count];
            b[0] = Effect;
            b[1] = Param1;
            b[2] = Param2;
            b[3] = (byte)Colors.Count;
            for (int i = 0; i < Colors.Count; i++)
            {
                b[4 + 3 * i] = Colors[i].R;
                b[5 + 3 * i] = Colors[i].G;
                b[6 + 3 * i] = Colors[i].B;
            }
            return b;
        }

        // Argumentos de 0F 02: [onde, led, efeito, p1, p2, n, RGB...] (tamanho 6 + 3n).
        public byte[] ToArgs(byte storage, byte ledId)
        {
            var body = ToBytes();
            var args = new byte[2 + body.Length];
            args[0] = storage;
            args[1] = ledId;
            Buffer.BlockCopy(body, 0, args, 2, body.Length);
            return args;
        }

        public Rgb ColorOr(int index, Rgb fallback) => index < Colors.Count ? Colors[index] : fallback;

        public bool Equals(LightingEffect other) =>
            other != null && Effect == other.Effect && Param1 == other.Param1 && Param2 == other.Param2 &&
            Colors.SequenceEqual(other.Colors);

        public override bool Equals(object obj) => Equals(obj as LightingEffect);
        public override int GetHashCode() => Effect << 16 | Param1 << 8 | Param2;
        public override string ToString() => Hex.Format(ToBytes());
    }
}
