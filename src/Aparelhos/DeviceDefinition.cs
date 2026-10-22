using System;
using System.Collections.Generic;
using System.Linq;
using OpenRazerProfiler.Protocolo;

namespace OpenRazerProfiler.Aparelhos
{
    // Tecla física desenhada na tela. Posição e tamanho em unidades de tecla (1 = uma tecla comum).
    public sealed class PhysicalKey
    {
        public PhysicalKey(byte id, string label, float x, float y, float width, float height)
        {
            Id = id;
            Label = label;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public byte Id { get; }
        public string Label { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
    }

    // Tudo o que muda de um modelo de teclado para outro. Um modelo novo é só uma nova instância.
    public sealed class DeviceDefinition
    {
        public string Name;
        public ushort VendorId = 0x1532;
        public ushort ProductId;
        public int ControlInterface;          // MI_xx da interface de controle (preferida na busca)
        public byte TransactionId;
        public byte BacklightLed;             // LED do backlight na classe 0x0F

        // Iluminação: efeitos oferecidos (IDs de EffectId, na ordem da tela) e limites validados.
        public byte[] Effects;
        public byte WaveLeftToRight;
        public byte WaveRightToLeft;
        public byte[] WaveSpeeds;             // byte de velocidade, do mais lento ao mais rápido (menor = mais rápido)
        public byte WaveDefaultSpeed;
        public byte ReactiveSpeedMin, ReactiveSpeedMax;
        public byte StarlightSpeedMin, StarlightSpeedMax;

        // Taxa de polling: família de comandos do modelo (cada família tem sua tabela de códigos)
        // e as taxas oferecidas na tela, em Hz. Sem polling: PollingKind.None e nenhuma taxa.
        public PollingKind Polling;
        public int[] PollingRates = Array.Empty<int>();

        // Teclas
        public IReadOnlyList<PhysicalKey> Layout;
        public SortedDictionary<byte, KeyBinding> DefaultKeys;     // camada base de fábrica
        public SortedDictionary<byte, KeyBinding> DefaultFnKeys;   // camada Fn de fábrica
        public HashSet<byte> FixedKeys;                            // não podem ser remapeadas pela interface

        // O que o firmware grava num perfil recém-criado (05 02): mapa de fábrica com modificadores
        // em forma de máscara, mais esta iluminação.
        public LightingEffect NewProfileLighting;
        public byte NewProfileBrightness;
        public byte NewProfilePollingCode;    // só na família de polling por perfil

        public PhysicalKey FindKey(byte id) => Layout.FirstOrDefault(k => k.Id == id);

        public KeyBinding DefaultFor(byte id) => DefaultKeys.TryGetValue(id, out var b) ? b : null;

        // O código existe na família do modelo e a taxa está entre as oferecidas.
        public bool OffersPollingCode(byte code) =>
            PollingCodes.IsValid(Polling, code) && PollingRates.Contains(PollingCodes.ToHz(Polling, code));

        public ProfileState NewProfileTemplate(byte slot) => new ProfileState
        {
            Slot = slot,
            Lighting = NewProfileLighting,
            Brightness = NewProfileBrightness,
            PollingCode = PollingCodes.PerProfile(Polling) ? NewProfilePollingCode : (byte)0,
            Keys = Normalize(DefaultKeys),
            FnKeys = Normalize(DefaultFnKeys),
        };

        // Erros de montagem da definição; lista vazia = definição coerente.
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Name) || ProductId == 0)
                errors.Add("nome e PID são obrigatórios");
            if (Polling == PollingKind.None && PollingRates.Length > 0)
                errors.Add("oferece taxas de polling, mas a família de polling é \"nenhum\"");
            if (Polling != PollingKind.None && PollingRates.Length == 0)
                errors.Add($"a família de polling é {PollingCodes.Name(Polling)}, mas nenhuma taxa é oferecida");
            foreach (var hz in PollingRates.Where(hz => !PollingCodes.Supports(Polling, hz)))
                errors.Add($"{hz} Hz não existe na família de polling {PollingCodes.Name(Polling)}");
            if (PollingCodes.PerProfile(Polling) && !OffersPollingCode(NewProfilePollingCode))
                errors.Add($"a taxa de perfil novo (código 0x{NewProfilePollingCode:X2}) não está entre as oferecidas");
            if (Effects == null || Layout == null || DefaultKeys == null || DefaultFnKeys == null || FixedKeys == null)
                errors.Add("efeitos, mapa físico e mapas de fábrica são obrigatórios");
            else if (Layout.Any(k => !DefaultKeys.ContainsKey(k.Id)))
                errors.Add("há teclas no mapa físico sem ação de fábrica");
            if (WaveSpeeds == null || WaveSpeeds.Length == 0 || !WaveSpeeds.Contains(WaveDefaultSpeed))
                errors.Add("as velocidades do wave precisam incluir a velocidade padrão");
            return errors;
        }

        static SortedDictionary<byte, KeyBinding> Normalize(SortedDictionary<byte, KeyBinding> keys) =>
            new SortedDictionary<byte, KeyBinding>(keys.ToDictionary(kv => kv.Key, kv => kv.Value.Normalized()));
    }

    // Monta o mapa físico linha por linha.
    public sealed class LayoutBuilder
    {
        readonly List<PhysicalKey> keys = new List<PhysicalKey>();
        float x, y;

        public LayoutBuilder Row(float rowY)
        {
            x = 0;
            y = rowY;
            return this;
        }

        public LayoutBuilder At(float column)
        {
            x = column;
            return this;
        }

        public LayoutBuilder Gap(float width)
        {
            x += width;
            return this;
        }

        public LayoutBuilder Key(byte id, string label, float width = 1, float height = 1)
        {
            keys.Add(new PhysicalKey(id, label, x, y, width, height));
            x += width;
            return this;
        }

        public LayoutBuilder KeyAt(byte id, string label, float keyX, float keyY, float width, float height)
        {
            keys.Add(new PhysicalKey(id, label, keyX, keyY, width, height));
            return this;
        }

        public IReadOnlyList<PhysicalKey> Build() => keys.ToArray();
    }

    // Tabela compacta "id:tipo-tamanho-dados" separada por espaços (gerada a partir do backup do aparelho).
    public static class KeyTable
    {
        public static SortedDictionary<byte, KeyBinding> Parse(string table)
        {
            var keys = new SortedDictionary<byte, KeyBinding>();
            foreach (var item in table.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split(':');
                keys[System.Convert.ToByte(parts[0], 16)] = KeyBinding.FromHex(parts[1]);
            }
            return keys;
        }
    }
}
