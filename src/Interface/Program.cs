using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using OpenRazerProfiler.Aparelhos;
using OpenRazerProfiler.Protocolo;

namespace OpenRazerProfiler.Interface
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // Pausas de 1–2 ms entre envio e leitura dos pacotes precisam do timer de 1 ms do Windows.
            NativeHid.timeBeginPeriod(1);
            try
            {
                if (args.Length == 2 && args[0] == "--exportar")
                    return ExportFromCommandLine(args[1]);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => ShowFatal(e.Exception);
                Application.Run(new MainForm());
                return 0;
            }
            finally
            {
                NativeHid.timeEndPeriod(1);
            }
        }

        static void ShowFatal(Exception ex) =>
            MessageBox.Show("Erro inesperado: " + ex.Message, "OpenRazerProfiler", MessageBoxButtons.OK, MessageBoxIcon.Error);

        // OpenRazerProfiler.exe --exportar arquivo.json : lê o teclado e salva o backup, sem abrir a janela.
        static int ExportFromCommandLine(string file)
        {
            AttachConsole(-1);
            try
            {
                var conn = DeviceCatalog.Connect();
                if (conn == null)
                {
                    Console.Error.WriteLine("Nenhum teclado compatível encontrado.");
                    return 2;
                }
                using (conn.Keyboard)
                {
                    var state = DeviceSync.ReadAll(conn.Keyboard);
                    var def = conn.Definition;
                    var header = new BackupFile.Header { DeviceName = def.Name, VendorId = def.VendorId, ProductId = def.ProductId };
                    File.WriteAllText(file, BackupFile.Serialize(state, header), new UTF8Encoding(false));
                    Console.WriteLine($"Backup de {state.Profiles.Count} perfis salvo em {Path.GetFullPath(file)}");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Erro: " + ex.Message);
                return 1;
            }
        }

        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int processId);
    }
}
