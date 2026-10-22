using System;
using System.Collections.Generic;
using System.Linq;
using OpenRazerProfiler.Aparelhos;
using OpenRazerProfiler.Protocolo;

// Testes do polling por modelo. Só leituras no aparelho; gravações inválidas devem parar antes de enviar.
static partial class Program
{
    static int falhas;

    static void Confere(string nome, bool ok, string detalhe = "")
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FALHOU")}] {nome}{(detalhe.Length > 0 ? " — " + detalhe : "")}");
        if (!ok)
            falhas++;
    }

    static string Erro(Action a)
    {
        try
        {
            a();
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    static DeviceDefinition Copia(DeviceDefinition b, PollingKind kind, int[] rates, byte newCode = 0) => new DeviceDefinition
    {
        Name = "Modelo de teste", ProductId = 0x9999, ControlInterface = b.ControlInterface, TransactionId = b.TransactionId,
        BacklightLed = b.BacklightLed, Effects = b.Effects, WaveLeftToRight = b.WaveLeftToRight, WaveRightToLeft = b.WaveRightToLeft,
        WaveSpeeds = b.WaveSpeeds, WaveDefaultSpeed = b.WaveDefaultSpeed, ReactiveSpeedMin = b.ReactiveSpeedMin,
        ReactiveSpeedMax = b.ReactiveSpeedMax, StarlightSpeedMin = b.StarlightSpeedMin, StarlightSpeedMax = b.StarlightSpeedMax,
        Layout = b.Layout, DefaultKeys = b.DefaultKeys, DefaultFnKeys = b.DefaultFnKeys, FixedKeys = b.FixedKeys,
        NewProfileLighting = b.NewProfileLighting, NewProfileBrightness = b.NewProfileBrightness,
        Polling = kind, PollingRates = rates, NewProfilePollingCode = newCode,
    };

    static int Validar(DeviceCatalog.Connection conn)
    {
        var hv2 = conn.Definition;
        Console.WriteLine("Definições do catálogo:");
        Confere("todas coerentes", Erro(DeviceCatalog.CheckDefinitions) == null);
        Confere("Huntsman V2 declara HyperPolling por perfil", hv2.Polling == PollingKind.HyperPollingPerProfile);

        Console.WriteLine("Definições de teste:");
        var casos = new (string Nome, DeviceDefinition Def, bool Valida)[]
        {
            ("clássica com 125/500/1000 Hz", Copia(hv2, PollingKind.Classic, new[] { 125, 500, 1000 }), true),
            ("clássica oferecendo 8000 Hz", Copia(hv2, PollingKind.Classic, new[] { 1000, 8000 }), false),
            ("sem polling mas oferecendo 1000 Hz", Copia(hv2, PollingKind.None, new[] { 1000 }), false),
            ("sem polling e sem taxas", Copia(hv2, PollingKind.None, Array.Empty<int>()), true),
            ("HyperPolling sem taxas", Copia(hv2, PollingKind.HyperPollingPerProfile, Array.Empty<int>(), 0x01), false),
            ("HyperPolling com taxa de perfil novo inválida", Copia(hv2, PollingKind.HyperPollingPerProfile, new[] { 1000 }, 0x03), false),
        };
        foreach (var (nome, def, valida) in casos)
        {
            var erros = def.Validate();
            Confere(nome + (valida ? " é aceita" : " é recusada"), (erros.Count == 0) == valida, string.Join("; ", erros));
        }

        Console.WriteLine("Tabelas de códigos:");
        Confere("código 0x01 = 8000 Hz no HyperPolling", PollingCodes.ToHz(PollingKind.HyperPollingPerProfile, 0x01) == 8000);
        Confere("código 0x01 = 1000 Hz na clássica", PollingCodes.ToHz(PollingKind.Classic, 0x01) == 1000);
        Confere("8000 Hz não tem código na clássica", Erro(() => PollingCodes.ToCode(PollingKind.Classic, 8000)) != null);
        Confere("0x03 é inválido no HyperPolling", !PollingCodes.IsValid(PollingKind.HyperPollingPerProfile, 0x03));
        Confere("perfil novo da clássica não guarda polling", Copia(hv2, PollingKind.Classic, new[] { 1000 }).NewProfileTemplate(3).PollingCode == 0);

        Console.WriteLine("No aparelho (só leituras):");
        var t = conn.Keyboard.Transport;
        var semPolling = new RazerKeyboard(t, hv2.BacklightLed, PollingKind.None);
        DeviceState s = null;
        Confere("lê o teclado inteiro como modelo sem polling", Erro(() => s = DeviceSync.ReadAll(semPolling)) == null);
        if (s != null)
        {
            Confere("nenhuma taxa lida", s.PollingCode == 0 && s.Profiles.Values.All(p => p.PollingCode == 0));
            var header = new BackupFile.Header { DeviceName = "teste", VendorId = 0x1532, ProductId = 0x026C };
            var json = BackupFile.Serialize(s, header);
            var back = BackupFile.Deserialize(json, out _);
            Confere("backup sem polling vai e volta igual", StateDiff.Compare(s, back).Count == 0 && !json.Contains("\"codigo\""));
            var outro = s.Clone();
            outro.PollingCode = 0x08;
            Confere("sem polling, nenhuma diferença de taxa é gravada", StateDiff.Compare(s, outro).Count == 0);
        }
        var real = DeviceSync.ReadAll(conn.Keyboard);
        var classicoNoHuntsman = new RazerKeyboard(t, hv2.BacklightLed, PollingKind.Classic);
        var e1 = Erro(() => classicoNoHuntsman.GetPollingCode());
        Confere("família errada (clássica) no Huntsman: o teclado recusa a leitura 00 85", e1 != null && e1.Contains("não suporta"), e1 ?? "");
        Confere("clássica não aceita taxa por perfil (para antes de enviar)",
            Erro(() => classicoNoHuntsman.SetPollingCode(1, 0x08))?.Contains("aparelho todo") == true);
        Confere("código inválido para a família é recusado antes de enviar",
            Erro(() => conn.Keyboard.SetPollingCode(1, 0x03))?.StartsWith("ArgumentException") == true);
        var depois = DeviceSync.ReadAll(conn.Keyboard);
        Confere("o teclado não mudou durante os testes", StateDiff.Compare(real, depois).Count == 0 &&
            real.Profiles.Values.Zip(depois.Profiles.Values, (a, b) => a.PollingCode == b.PollingCode).All(x => x));

        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : $"{falhas} TESTE(S) FALHARAM");
        return falhas == 0 ? 0 : 1;
    }
}
