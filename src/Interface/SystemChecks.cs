using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;

namespace OpenRazerProfiler.Interface
{
    // Programas e configurações do Windows que disputam o controle do teclado.
    static class SystemChecks
    {
        static readonly string[] razerPrefixes = { "razer", "rzsdk", "rzchroma", "rzdevice", "rzengine", "rzactionsvc" };

        // Processos do Razer Synapse e afins em execução.
        public static List<string> RazerProcesses()
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string n = p.ProcessName;
                    if (n.IndexOf("synapse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        razerPrefixes.Any(x => n.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
                        names.Add(n);
                }
                catch (InvalidOperationException)
                {
                    // processo terminou durante a listagem
                }
                finally
                {
                    p.Dispose();
                }
            }
            return names.ToList();
        }

        // Iluminação dinâmica do Windows 11 ligada (ela controla os LEDs pela interface LampArray).
        public static bool DynamicLightingEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Lighting"))
                    return key?.GetValue("AmbientLightingEnabled") is int v && v != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
