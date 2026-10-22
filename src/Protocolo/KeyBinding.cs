using System;
using System.Linq;

namespace OpenRazerProfiler.Protocolo
{
    // O que uma tecla faz, no formato gravado no teclado: [tipo, tamanho, dados...].
    public sealed class KeyBinding : IEquatable<KeyBinding>
    {
        public const byte TypeDisabled = 0x00;    // tecla desativada (tamanho 0)
        public const byte TypeKeyboard = 0x02;    // [máscara de modificadores, HID usage do teclado]
        public const byte TypeSpecial07 = 0x07;   // função do firmware de significado desconhecido
        public const byte TypeSystem = 0x09;      // [usage de System Control], ex.: 0x82 = suspender
        public const byte TypeConsumer = 0x0A;    // [usage de Consumer, 16 bits big-endian], teclas de mídia
        public const byte TypeSpecial = 0x11;     // função especial do firmware (0x01 = Fn)

        public const int MaxDataLength = 5;       // cada entrada do mapa tem 8 bytes: id + tipo + tamanho + 5

        readonly byte[] data;

        public KeyBinding(byte type, byte[] data)
        {
            if (data.Length > MaxDataLength)
                throw new ArgumentException("Dados da tecla maiores que 5 bytes.");
            Type = type;
            this.data = (byte[])data.Clone();
        }

        public byte Type { get; }
        public int DataLength => data.Length;
        public byte this[int index] => data[index];

        public static KeyBinding Disabled() => new KeyBinding(TypeDisabled, Array.Empty<byte>());
        public static KeyBinding Keyboard(byte modifiers, byte usage) => new KeyBinding(TypeKeyboard, new[] { modifiers, usage });
        public static KeyBinding Consumer(ushort usage) => new KeyBinding(TypeConsumer, new[] { (byte)(usage >> 8), (byte)usage });
        public static KeyBinding SystemControl(byte usage) => new KeyBinding(TypeSystem, new[] { usage });

        public bool IsDisabled => Type == TypeDisabled;
        public bool IsKeyboard => Type == TypeKeyboard && data.Length == 2;
        public bool IsConsumer => Type == TypeConsumer && data.Length == 2;
        public bool IsSystem => Type == TypeSystem && data.Length == 1;
        public byte Modifiers => IsKeyboard ? data[0] : (byte)0;
        public byte Usage => IsKeyboard ? data[1] : (byte)0;
        public ushort ConsumerUsage => IsConsumer ? (ushort)(data[0] << 8 | data[1]) : (ushort)0;
        public byte SystemUsage => IsSystem ? data[0] : (byte)0;

        // Lê [tipo, tamanho, dados...] a partir de buffer[offset].
        public static KeyBinding Parse(byte[] buffer, int offset)
        {
            byte type = buffer[offset];
            int size = Math.Min((int)buffer[offset + 1], MaxDataLength);
            size = Math.Min(size, buffer.Length - offset - 2);
            var d = new byte[Math.Max(0, size)];
            Buffer.BlockCopy(buffer, offset + 2, d, 0, d.Length);
            return new KeyBinding(type, d);
        }

        public byte[] ToEntry()
        {
            var e = new byte[2 + data.Length];
            e[0] = Type;
            e[1] = (byte)data.Length;
            Buffer.BlockCopy(data, 0, e, 2, data.Length);
            return e;
        }

        public string ToHex() => string.Concat(ToEntry().Select(b => b.ToString("x2")));

        public static KeyBinding FromHex(string hex)
        {
            var bytes = Hex.Parse(hex);
            if (bytes.Length < 2 || bytes[1] != bytes.Length - 2)
                throw new FormatException($"Entrada de tecla inválida: {hex}");
            return Parse(bytes, 0);
        }

        // A fábrica grava modificadores como usage 0xE0–0xE7 e o firmware/Synapse como bit da máscara
        // com usage 0. A forma normalizada usa sempre a máscara.
        public KeyBinding Normalized()
        {
            if (IsKeyboard && data[1] >= 0xE0 && data[1] <= 0xE7)
                return Keyboard((byte)(data[0] | 1 << (data[1] - 0xE0)), 0);
            return this;
        }

        // Mesma ação, ignorando a forma de gravar modificadores.
        public bool SameAction(KeyBinding other) => other != null && Normalized().Equals(other.Normalized());

        public bool Equals(KeyBinding other) =>
            other != null && Type == other.Type && data.SequenceEqual(other.data);

        public override bool Equals(object obj) => Equals(obj as KeyBinding);

        public override int GetHashCode()
        {
            int h = Type;
            foreach (var b in data)
                h = h * 31 + b;
            return h;
        }

        public override string ToString() => ToHex();
    }

    public static class Hex
    {
        public static byte[] Parse(string hex)
        {
            hex = (hex ?? "").Replace(" ", "");
            if (hex.Length % 2 != 0)
                throw new FormatException($"Hexadecimal inválido: {hex}");
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return b;
        }

        public static string Format(byte[] bytes) => string.Concat(bytes.Select(b => b.ToString("x2")));
    }
}
