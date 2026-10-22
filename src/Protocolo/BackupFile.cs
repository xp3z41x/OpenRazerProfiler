using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenRazerProfiler.Protocolo
{
    // Backup de todos os perfis em JSON. Guarda os bytes exatos lidos do teclado (em hexadecimal),
    // com campos legíveis ao lado só para consulta.
    public static class BackupFile
    {
        public const string Format = "openrazerprofiler-backup";
        public const int Version = 1;

        public sealed class Header
        {
            public string DeviceName;
            public ushort VendorId;
            public ushort ProductId;
            public string Firmware;
            public string Serial;
            public string CreatedAt;
        }

        public static string Serialize(DeviceState state, Header header)
        {
            var root = new JsonObject
            {
                { "formato", Format },
                { "versao", Version },
                { "criado_em", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture) },
                { "aparelho", new JsonObject
                    {
                        { "nome", header.DeviceName },
                        { "vid", header.VendorId.ToString("X4") },
                        { "pid", header.ProductId.ToString("X4") },
                        { "firmware", state.Firmware },
                        { "serie", state.Serial },
                    }
                },
                { "polling", PollingJson(state.PollingKind, state.PollingCode) },
                { "perfil_ativo", (int)state.ActiveProfile },
            };
            var profiles = new List<object>();
            foreach (var p in state.Profiles.Values)
            {
                var o = new JsonObject
                {
                    { "slot", (int)p.Slot },
                    { "nome", p.Name },
                    { "metadados", Hex.Format(p.Metadata) },
                    { "iluminacao", new JsonObject
                        {
                            { "efeito", EffectName(p.Lighting.Effect) },
                            { "dados", Hex.Format(p.Lighting.ToBytes()) },
                            { "brilho", (int)p.Brightness },
                        }
                    },
                };
                if (PollingCodes.PerProfile(state.PollingKind))
                    o["polling"] = PollingJson(state.PollingKind, p.PollingCode);
                o["teclas"] = KeysToJson(p.Keys);
                o["teclas_fn"] = KeysToJson(p.FnKeys);
                profiles.Add(o);
            }
            root["perfis"] = profiles;
            return Json.Write(root);
        }

        public static DeviceState Deserialize(string json, out Header header)
        {
            var root = Json.Parse(json) as JsonObject ?? throw new FormatException("O arquivo não contém um objeto JSON.");
            if (root["formato"] as string != Format)
                throw new FormatException("O arquivo não é um backup do OpenRazerProfiler.");
            if (root.Number("versao") > Version)
                throw new FormatException("O backup foi feito por uma versão mais nova do utilitário.");
            var device = root.Object("aparelho");
            header = new Header
            {
                DeviceName = device["nome"] as string ?? "",
                VendorId = ushort.Parse(device.String("vid"), NumberStyles.HexNumber),
                ProductId = ushort.Parse(device.String("pid"), NumberStyles.HexNumber),
                Firmware = device["firmware"] as string ?? "",
                Serial = device["serie"] as string ?? "",
                CreatedAt = root["criado_em"] as string ?? "",
            };

            // O tipo de polling do arquivo é só informativo: ao importar vale a família do teclado conectado.
            var polling = root["polling"] as JsonObject;
            var state = new DeviceState
            {
                PollingKind = PollingCodes.Parse(polling?["tipo"] as string),
                PollingCode = PollingCodeFrom(polling),
                ActiveProfile = checked((byte)root.Number("perfil_ativo")),
            };
            foreach (var item in root.Array("perfis"))
            {
                var o = item as JsonObject ?? throw new FormatException("Perfil inválido no backup.");
                var light = o.Object("iluminacao");
                var p = new ProfileState
                {
                    Slot = checked((byte)o.Number("slot")),
                    Metadata = Hex.Parse(o.String("metadados")),
                    Lighting = LightingEffect.FromBytes(Hex.Parse(light.String("dados"))),
                    Brightness = checked((byte)light.Number("brilho")),
                    // backups sem polling por perfil usam a taxa geral do arquivo
                    PollingCode = o["polling"] is JsonObject own ? PollingCodeFrom(own) : state.PollingCode,
                    Keys = KeysFromJson(o.Object("teclas")),
                    FnKeys = KeysFromJson(o.Object("teclas_fn")),
                };
                if (p.Slot == 0 || state.Profiles.ContainsKey(p.Slot))
                    throw new FormatException($"Slot de perfil inválido ou repetido: {p.Slot}.");
                if (p.Metadata.Length == 0)
                    throw new FormatException($"O perfil {p.Slot} está sem metadados.");
                state.Profiles[p.Slot] = p;
            }
            if (!state.Profiles.ContainsKey(state.ActiveProfile))
                throw new FormatException("O perfil ativo do backup não está na lista de perfis.");
            return state;
        }

        static JsonObject PollingJson(PollingKind kind, byte code)
        {
            var o = new JsonObject { { "tipo", PollingCodes.Name(kind) } };
            if (kind != PollingKind.None)
            {
                o["hz"] = PollingCodes.ToHz(kind, code);
                o["codigo"] = code.ToString("X2");
            }
            return o;
        }

        static byte PollingCodeFrom(JsonObject o) =>
            o?["codigo"] is string code ? byte.Parse(code, NumberStyles.HexNumber) : (byte)0;

        static JsonObject KeysToJson(SortedDictionary<byte, KeyBinding> keys)
        {
            var o = new JsonObject();
            foreach (var kv in keys)
                o[kv.Key.ToString("X2")] = kv.Value.ToHex();
            return o;
        }

        static SortedDictionary<byte, KeyBinding> KeysFromJson(JsonObject o)
        {
            var keys = new SortedDictionary<byte, KeyBinding>();
            foreach (var kv in o)
                keys[byte.Parse(kv.Key, NumberStyles.HexNumber)] = KeyBinding.FromHex(kv.Value as string);
            return keys;
        }

        public static string EffectName(byte effect) => effect switch
        {
            EffectId.Off => "desligado",
            EffectId.Static => "estatico",
            EffectId.Breathing => "breathing",
            EffectId.Spectrum => "spectrum",
            EffectId.Wave => "wave",
            EffectId.Reactive => "reactive",
            EffectId.Starlight => "starlight",
            EffectId.Custom => "personalizado",
            _ => "desconhecido-" + effect.ToString("x2"),
        };
    }
}
