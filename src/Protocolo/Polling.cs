using System;
using System.Linq;

namespace OpenRazerProfiler.Protocolo
{
    // Famílias de comando de taxa de polling dos aparelhos Razer. Cada modelo declara a sua na definição.
    public enum PollingKind
    {
        // Sem controle de polling: o utilitário não lê nem grava a taxa.
        None,

        // 00 C0 [onde] / 00 40 [onde, código], até 8000 Hz, uma taxa em cada perfil
        // ("onde" como na iluminação: 0 = em uso, 1..5 = perfis). Validada no Huntsman V2.
        HyperPollingPerProfile,

        // 00 85 / 00 05 [código], até 1000 Hz, uma taxa só para o aparelho. Usada por modelos mais
        // antigos segundo o OpenRazer; não testada aqui por falta de aparelho.
        Classic,
    }

    // Tabela de códigos de cada família.
    public static class PollingCodes
    {
        static readonly (byte Code, int Hz)[] hyperPolling =
        {
            (0x01, 8000), (0x02, 4000), (0x04, 2000), (0x08, 1000), (0x10, 500), (0x20, 250), (0x40, 125),
        };

        static readonly (byte Code, int Hz)[] classic =
        {
            (0x01, 1000), (0x02, 500), (0x08, 125),
        };

        static (byte Code, int Hz)[] Table(PollingKind kind) => kind switch
        {
            PollingKind.HyperPollingPerProfile => hyperPolling,
            PollingKind.Classic => classic,
            _ => Array.Empty<(byte, int)>(),
        };

        // A taxa fica guardada em cada perfil (e não uma só para o aparelho).
        public static bool PerProfile(PollingKind kind) => kind == PollingKind.HyperPollingPerProfile;

        public static int ToHz(PollingKind kind, byte code)
        {
            foreach (var (c, hz) in Table(kind))
                if (c == code)
                    return hz;
            return 0;
        }

        public static bool IsValid(PollingKind kind, byte code) => ToHz(kind, code) != 0;

        public static bool Supports(PollingKind kind, int hz) => Table(kind).Any(t => t.Hz == hz);

        public static byte ToCode(PollingKind kind, int hz)
        {
            foreach (var (c, h) in Table(kind))
                if (h == hz)
                    return c;
            throw new ArgumentException($"{hz} Hz não existe na família de polling {Name(kind)}.");
        }

        public static string Name(PollingKind kind) => kind switch
        {
            PollingKind.HyperPollingPerProfile => "hyperpolling-por-perfil",
            PollingKind.Classic => "classico",
            _ => "nenhum",
        };

        public static PollingKind Parse(string name) =>
            name == Name(PollingKind.HyperPollingPerProfile) ? PollingKind.HyperPollingPerProfile
            : name == Name(PollingKind.Classic) ? PollingKind.Classic
            : PollingKind.None;
    }
}
