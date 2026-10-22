using System;
using System.Diagnostics;
using System.Threading;

namespace OpenRazerProfiler.Protocolo
{
    // Envia um pacote com SetFeature, espera ~2 ms e lê a resposta com GetFeature.
    // Repete a leitura enquanto o teclado responde "ocupado" ou devolve uma resposta antiga.
    public sealed class RazerTransport : IDisposable
    {
        const int ReportLength = RazerPacket.Length + 1;   // report ID 0 + 90 bytes

        readonly HidFeatureChannel channel;
        readonly object gate = new object();

        public RazerTransport(string devicePath, byte transactionId)
        {
            channel = new HidFeatureChannel(devicePath);
            TransactionId = transactionId;
        }

        public byte TransactionId { get; }
        public string DevicePath => channel.Path;

        // Quanto esperar pela resposta de cada envio, e quantos envios tentar.
        public int ResponseTimeoutMs { get; set; } = 1500;
        public int Attempts { get; set; } = 3;

        // Diagnóstico opcional: recebe cada par pedido/resposta.
        public Action<RazerPacket, RazerPacket> Trace { get; set; }

        // Envia e devolve a resposta, qualquer que seja o status.
        // echo = quantos bytes iniciais dos argumentos o teclado repete na resposta (para descartar respostas antigas).
        public RazerPacket Send(byte commandClass, byte command, byte[] args = null, int dataSize = -1, int echo = 0)
        {
            args = args ?? Array.Empty<byte>();
            if (dataSize < 0)
                dataSize = args.Length;
            var request = RazerPacket.Request(TransactionId, commandClass, command, args, dataSize);
            var report = new byte[ReportLength];
            request.CopyTo(report, 1);

            lock (gate)
            {
                for (int attempt = 0; attempt < Attempts; attempt++)
                {
                    if (!channel.SetFeature(report))
                    {
                        Thread.Sleep(20);
                        continue;
                    }
                    Thread.Sleep(2);
                    var clock = Stopwatch.StartNew();
                    while (clock.ElapsedMilliseconds < ResponseTimeoutMs)
                    {
                        var buffer = new byte[ReportLength];
                        if (!channel.GetFeature(buffer))
                        {
                            Thread.Sleep(5);
                            continue;
                        }
                        var response = RazerPacket.FromReport(buffer, 1);
                        if (response.Status == RazerStatus.Busy || response.Status == RazerStatus.New)
                        {
                            Thread.Sleep(2);
                            continue;
                        }
                        if (response.Class != commandClass || response.Command != command ||
                            (response.Status == RazerStatus.Ok && !EchoMatches(response, args, echo)))
                        {
                            Thread.Sleep(2);   // resposta de outro pedido; espera a nossa
                            continue;
                        }
                        Trace?.Invoke(request, response);
                        return response;
                    }
                }
            }
            throw new RazerException(commandClass, command, null);
        }

        // Envia e exige status "ok".
        public RazerPacket Execute(byte commandClass, byte command, byte[] args = null, int dataSize = -1, int echo = 0)
        {
            var response = Send(commandClass, command, args, dataSize, echo);
            if (response.Status != RazerStatus.Ok)
                throw new RazerException(commandClass, command, response.Status);
            return response;
        }

        static bool EchoMatches(RazerPacket response, byte[] args, int echo)
        {
            for (int i = 0; i < echo && i < args.Length; i++)
                if (response.Arg(i) != args[i])
                    return false;
            return true;
        }

        public void Dispose() => channel.Dispose();
    }
}
