using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OpenRazerProfiler.Aparelhos;
using OpenRazerProfiler.Protocolo;

static partial class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        NativeTimer.Begin();
        var conn = DeviceCatalog.Connect();
        if (conn == null) { Console.WriteLine("teclado não encontrado"); return 2; }
        var kb = conn.Keyboard;
        var def = conn.Definition;
        Console.WriteLine($"{def.Name} em {conn.Interface}");
        var header = new BackupFile.Header { DeviceName = def.Name, VendorId = def.VendorId, ProductId = def.ProductId };
        string cmd = args.Length > 0 ? args[0] : "ler";
        var sw = Stopwatch.StartNew();
        switch (cmd)
        {
            case "ler":
            {
                var s = DeviceSync.ReadAll(kb);
                Console.WriteLine($"leitura completa em {sw.ElapsedMilliseconds} ms: fw {s.Firmware} série {s.Serial} modo {s.DeviceMode} layout {s.LayoutCode} polling {PollingCodes.ToHz(s.PollingKind, s.PollingCode)} Hz ({PollingCodes.Name(s.PollingKind)}) slots {s.SlotCount} ativo {s.ActiveProfile}");
                foreach (var p in s.Profiles.Values)
                    Console.WriteLine($"  perfil {p.Slot} \"{p.Name}\" luz {p.Lighting} brilho {p.Brightness} teclas {p.Keys.Count}/{p.FnKeys.Count}");
                if (args.Length > 1)
                {
                    File.WriteAllText(args[1], BackupFile.Serialize(s, header), new UTF8Encoding(false));
                    var back = BackupFile.Deserialize(File.ReadAllText(args[1]), out _);
                    var diff = StateDiff.Compare(s, back);
                    Console.WriteLine($"backup salvo; ida e volta pelo JSON: {(diff.Count == 0 ? "igual" : diff.Count + " diferenças")}");
                }
                return 0;
            }
            case "comparar":
            {
                var s = DeviceSync.ReadAll(kb);
                var b = BackupFile.Deserialize(File.ReadAllText(args[1]), out _);
                var diff = StateDiff.Compare(s, b);
                Console.WriteLine(diff.Count == 0 ? "IGUAL AO BACKUP" : $"{diff.Count} diferenças:");
                foreach (var d in diff) Console.WriteLine("  " + d);
                return diff.Count == 0 ? 0 : 1;
            }
            case "aplicar":
            {
                var s = DeviceSync.ReadAll(kb);
                var b = BackupFile.Deserialize(File.ReadAllText(args[1]), out _);
                Console.WriteLine($"{StateDiff.Compare(s, b).Count} diferenças a gravar");
                var r = DeviceSync.Save(kb, s, b, def.NewProfileTemplate, m => Console.WriteLine("  " + m));
                Console.WriteLine($"gravações: {r.Writes}; divergências após reler: {r.Mismatches.Count} ({sw.ElapsedMilliseconds} ms)");
                foreach (var d in r.Mismatches) Console.WriteLine("  " + d);
                return r.Mismatches.Count == 0 ? 0 : 1;
            }
        }
        if (cmd == "validar")
            return Validar(conn);
        if (cmd == "ativar")
        {
            var s = DeviceSync.ReadAll(kb);
            var d = s.Clone();
            d.ActiveProfile = byte.Parse(args[1]);
            int w = DeviceSync.Apply(kb, s, d, def.NewProfileTemplate);
            var r = DeviceSync.ReadAll(kb);
            var diff = StateDiff.Compare(r, d);
            Console.WriteLine($"gravações {w}; divergências na 1ª releitura: {diff.Count}");
            foreach (var x in diff)
            {
                Console.WriteLine("  " + x);
                if (x.Kind == ChangeKind.Lighting || x.Kind == ChangeKind.Brightness)
                    Console.WriteLine($"     lido {r.Profiles[x.Slot].Lighting} br {r.Profiles[x.Slot].Brightness} | pedido {d.Profiles[x.Slot].Lighting} br {d.Profiles[x.Slot].Brightness}");
                if (x.Kind == ChangeKind.Key)
                    Console.WriteLine($"     lido {r.Profiles[x.Slot].Layer(x.Layer)[x.Key]} | pedido {d.Profiles[x.Slot].Layer(x.Layer)[x.Key]}");
            }
            return 0;
        }
        Console.WriteLine("comandos: validar | ler [saida.json] | comparar <backup.json> | aplicar <backup.json> (grava) | ativar <slot> (grava)");
        return 1;
    }
}

static class NativeTimer
{
    [System.Runtime.InteropServices.DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint p);
    public static void Begin() => timeBeginPeriod(1);
}
