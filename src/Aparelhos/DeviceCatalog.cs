using System;
using System.Collections.Generic;
using System.Linq;
using OpenRazerProfiler.Protocolo;

namespace OpenRazerProfiler.Aparelhos
{
    // Modelos conhecidos. Para suportar outro teclado, crie a definição e acrescente aqui.
    public static class DeviceCatalog
    {
        public static readonly IReadOnlyList<DeviceDefinition> All = new[]
        {
            HuntsmanV2.Definition,
        };

        public sealed class Connection
        {
            public DeviceDefinition Definition;
            public RazerKeyboard Keyboard;
            public HidDeviceInfo Interface;
        }

        // Recusa definições incoerentes (ex.: uma taxa de polling que a família do modelo não tem).
        public static void CheckDefinitions()
        {
            foreach (var def in All)
            {
                var errors = def.Validate();
                if (errors.Count > 0)
                    throw new InvalidOperationException($"Definição de aparelho inválida ({def.Name}): {string.Join("; ", errors)}.");
            }
        }

        // Procura um teclado conhecido e escolhe a interface em que a leitura do firmware (00 81) responde ok.
        public static Connection Connect()
        {
            CheckDefinitions();
            foreach (var def in All)
            {
                var candidates = HidEnumerator.Enumerate(def.VendorId, def.ProductId)
                    .Where(i => i.FeatureReportLength == 0 || i.FeatureReportLength >= RazerPacket.Length + 1)
                    .OrderBy(i => i.InterfaceNumber == def.ControlInterface ? 0 : 1)
                    .ThenBy(i => i.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var candidate in candidates)
                {
                    RazerTransport transport = null;
                    try
                    {
                        transport = new RazerTransport(candidate.Path, def.TransactionId) { ResponseTimeoutMs = 250, Attempts = 1 };
                        if (transport.Send(0x00, 0x81, null, 2).Status == RazerStatus.Ok)
                        {
                            transport.ResponseTimeoutMs = 1500;
                            transport.Attempts = 3;
                            var kb = new RazerKeyboard(transport, def.BacklightLed, def.Polling);
                            return new Connection { Definition = def, Keyboard = kb, Interface = candidate };
                        }
                    }
                    catch (Exception)
                    {
                        // interface sem o canal de controle; tenta a próxima
                    }
                    transport?.Dispose();
                }
            }
            return null;
        }
    }
}
