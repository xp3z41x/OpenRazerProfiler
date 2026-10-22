using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenRazerProfiler.Protocolo
{
    // Comandos tipados de um teclado Razer com perfis na memória interna.
    // Os parâmetros específicos do modelo (transaction id, LED, família de polling) vêm da definição do aparelho.
    public sealed class RazerKeyboard : IDisposable
    {
        public const byte LiveStorage = 0x00;   // "onde guardar" = só RAM (efeito ao vivo)
        public const byte LiveProfile = 0x00;   // perfil 0 no mapa de teclas = mapa em uso
        const int MetadataChunk = 75;
        const int KeymapEntries = 9;

        readonly RazerTransport transport;

        public RazerKeyboard(RazerTransport transport, byte ledId, PollingKind polling)
        {
            this.transport = transport;
            LedId = ledId;
            Polling = polling;
        }

        public byte LedId { get; }
        public PollingKind Polling { get; }
        public RazerTransport Transport => transport;

        // ---- Aparelho (classe 0x00) ----

        public string GetFirmware()
        {
            var r = transport.Execute(0x00, 0x81, null, 2);
            return $"{r.Arg(0)}.{r.Arg(1):D2}";
        }

        public string GetSerial()
        {
            var r = transport.Execute(0x00, 0x82, null, 0x16);
            return Encoding.ASCII.GetString(r.Args(0, 0x16)).TrimEnd('\0', ' ');
        }

        // 0x00 = normal (o firmware controla), 0x03 = driver (software controla).
        public byte GetDeviceMode() => transport.Execute(0x00, 0x84, null, 2).Arg(0);

        public void SetDeviceMode(byte mode) => transport.Execute(0x00, 0x04, new byte[] { mode, 0x00 }, 2, echo: 2);

        public byte GetLayout() => transport.Execute(0x00, 0x86, null, 2).Arg(0);

        // Taxa de polling conforme a família do modelo (ver PollingKind). storage só existe na família por
        // perfil: 0 = valor em uso, 1..5 = perfis (gravar no perfil ativo também muda o valor em uso).
        public byte GetPollingCode(byte storage = LiveStorage)
        {
            switch (Polling)
            {
                case PollingKind.HyperPollingPerProfile:
                    return transport.Execute(0x00, 0xC0, new[] { storage }, 2, echo: 1).Arg(1);
                case PollingKind.Classic:
                    RequireDeviceWide(storage);
                    return transport.Execute(0x00, 0x85, null, 1).Arg(0);
                default:
                    throw new NotSupportedException("Este modelo não tem controle de taxa de polling.");
            }
        }

        public void SetPollingCode(byte storage, byte code)
        {
            if (Polling == PollingKind.None)
                throw new NotSupportedException("Este modelo não tem controle de taxa de polling.");
            if (!PollingCodes.IsValid(Polling, code))
                throw new ArgumentException($"Código de polling inválido para este modelo: 0x{code:X2}");   // o firmware aceitaria qualquer valor
            if (Polling == PollingKind.HyperPollingPerProfile)
            {
                transport.Execute(0x00, 0x40, new byte[] { storage, code }, 2, echo: 2);
            }
            else
            {
                RequireDeviceWide(storage);
                transport.Execute(0x00, 0x05, new[] { code }, 1, echo: 1);
            }
        }

        static void RequireDeviceWide(byte storage)
        {
            if (storage != LiveStorage)
                throw new InvalidOperationException("Neste modelo a taxa de polling vale para o aparelho todo, não por perfil.");
        }

        // ---- Perfis (classe 0x05) ----

        // Capacidade de slots (05 8A). Não confundir com 05 80, que conta os perfis gravados.
        public int GetProfileSlotCount() => transport.Execute(0x05, 0x8A, null, 1).Arg(0);

        public int GetProfileCount() => transport.Execute(0x05, 0x80, null, 1).Arg(0);

        public List<byte> GetProfiles()
        {
            var r = transport.Execute(0x05, 0x81, null, 0x10);
            int n = Math.Min((int)r.Arg(0), RazerPacket.MaxArgs - 1);
            return r.Args(1, n).ToList();
        }

        public byte GetActiveProfile() => transport.Execute(0x05, 0x84, null, 1).Arg(0);

        public void SetActiveProfile(byte profile) => transport.Execute(0x05, 0x04, new[] { profile }, 1, echo: 1);

        // Cria o perfil (ou reinicia um existente) com o modelo do firmware. Não mexe nos metadados.
        public void CreateProfile(byte profile) => transport.Execute(0x05, 0x02, new[] { profile }, 1, echo: 1);

        // Tira o perfil da lista. Os dados ficam residuais no slot.
        public void DeleteProfile(byte profile) => transport.Execute(0x05, 0x03, new[] { profile }, 1, echo: 1);

        // Metadados em pedaços de até 75 bytes: resposta [perfil, offset(2), total(2), dados].
        public byte[] ReadProfileMetadata(byte profile)
        {
            var data = new List<byte>();
            int offset = 0;
            while (true)
            {
                var r = transport.Execute(0x05, 0x88, new[] { profile, (byte)(offset >> 8), (byte)offset }, 0x50, echo: 3);
                int total = r.Arg(3) << 8 | r.Arg(4);
                int n = Math.Min(MetadataChunk, total - offset);
                if (n <= 0)
                    break;
                data.AddRange(r.Args(5, n));
                offset += n;
                if (offset >= total)
                    break;
            }
            return data.ToArray();
        }

        public void WriteProfileMetadata(byte profile, byte[] metadata)
        {
            if (metadata.Length == 0 || metadata.Length > 0xFFFF)
                throw new ArgumentException("Metadados vazios ou grandes demais.");
            int total = metadata.Length;
            for (int offset = 0; offset < total; offset += MetadataChunk)
            {
                int n = Math.Min(MetadataChunk, total - offset);
                var args = new byte[5 + n];
                args[0] = profile;
                args[1] = (byte)(offset >> 8);
                args[2] = (byte)offset;
                args[3] = (byte)(total >> 8);
                args[4] = (byte)total;
                Buffer.BlockCopy(metadata, offset, args, 5, n);
                transport.Execute(0x05, 0x08, args, args.Length, echo: 3);
            }
        }

        // ---- Iluminação (classe 0x0F); storage 0 = RAM, 1..5 = perfis ----

        public byte[] GetSupportedEffects()
        {
            var r = transport.Execute(0x0F, 0x81, new[] { LedId }, 0x10, echo: 1);
            return r.Args(1, r.DataSize - 1);
        }

        public LightingEffect GetEffect(byte storage)
        {
            var r = transport.Execute(0x0F, 0x82, new[] { storage, LedId }, 0x0C, echo: 2);
            return LightingEffect.FromResponse(r.Data);
        }

        public void SetEffect(byte storage, LightingEffect effect)
        {
            var args = effect.ToArgs(storage, LedId);
            transport.Execute(0x0F, 0x02, args, args.Length, echo: 3);
        }

        public byte GetBrightness(byte storage) => transport.Execute(0x0F, 0x84, new[] { storage, LedId }, 3, echo: 2).Arg(2);

        public void SetBrightness(byte storage, byte value) =>
            transport.Execute(0x0F, 0x04, new[] { storage, LedId, value }, 3, echo: 2);

        // ---- Mapa de teclas (classe 0x02); camada 0 = base, 1 = Fn ----

        // Lê o mapa inteiro, 9 teclas por pedido: [perfil, camada, entradas de 8 bytes].
        public SortedDictionary<byte, KeyBinding> ReadKeymap(byte profile, byte layer)
        {
            var keys = new SortedDictionary<byte, KeyBinding>();
            int start = 0;
            while (start <= 0xFF)
            {
                var r = transport.Execute(0x02, 0x8F, new[] { profile, layer, (byte)start }, 0x4A, echo: 2);
                var entries = new List<(byte Id, KeyBinding Binding)>();
                for (int i = 0; i < KeymapEntries; i++)
                {
                    int at = 2 + 8 * i;
                    byte id = r.Arg(at);
                    if (id == 0)
                        break;
                    entries.Add((id, KeyBinding.Parse(r.Args(at + 1, 7), 0)));
                }
                if (entries.Count == 0)
                    break;
                // Depois da última tecla o teclado devolve só [início, 00...], igual a uma tecla
                // desativada; a leitura individual diferencia (falha se a tecla não existe).
                if (entries.Count == 1 && entries[0].Id == start && entries[0].Binding.IsDisabled &&
                    entries[0].Binding.DataLength == 0 && !KeyExists(profile, (byte)start, layer))
                    break;
                foreach (var (id, binding) in entries)
                    keys[id] = binding;
                if (entries.Count < KeymapEntries)
                    break;
                start = entries[entries.Count - 1].Id + 1;
            }
            return keys;
        }

        bool KeyExists(byte profile, byte key, byte layer) =>
            transport.Send(0x02, 0x8D, new[] { profile, key, layer }, 0x0A, echo: 3).Status == RazerStatus.Ok;

        // Resposta: [perfil, tecla, camada, tipo, tamanho, dados...]
        public KeyBinding ReadKey(byte profile, byte key, byte layer)
        {
            var r = transport.Execute(0x02, 0x8D, new[] { profile, key, layer }, 0x0A, echo: 3);
            return KeyBinding.Parse(r.Data, 3);
        }

        public void WriteKey(byte profile, byte key, byte layer, KeyBinding binding)
        {
            var entry = binding.ToEntry();
            var args = new byte[3 + entry.Length];
            args[0] = profile;
            args[1] = key;
            args[2] = layer;
            Buffer.BlockCopy(entry, 0, args, 3, entry.Length);
            transport.Execute(0x02, 0x0D, args, args.Length, echo: 3);
        }

        public void Dispose() => transport.Dispose();
    }
}
