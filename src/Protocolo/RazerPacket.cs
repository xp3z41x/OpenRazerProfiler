using System;
using System.Text;

namespace OpenRazerProfiler.Protocolo
{
    public enum RazerStatus : byte
    {
        New = 0x00,
        Busy = 0x01,
        Ok = 0x02,
        Failed = 0x03,
        Timeout = 0x04,
        NotSupported = 0x05,
    }

    // Pacote de 90 bytes trocado por feature report (report ID 0):
    // [0] status | [1] transaction id | [2..3] pacotes restantes | [4] protocolo | [5] tamanho dos dados
    // [6] classe | [7] comando | [8..87] argumentos | [88] CRC (XOR de 2..87) | [89] 0
    public sealed class RazerPacket
    {
        public const int Length = 90;
        public const int MaxArgs = 80;

        readonly byte[] bytes;

        RazerPacket(byte[] bytes) => this.bytes = bytes;

        public static RazerPacket Request(byte transactionId, byte commandClass, byte command, byte[] args, int dataSize)
        {
            if (args.Length > MaxArgs)
                throw new ArgumentException("Argumentos maiores que 80 bytes.");
            var b = new byte[Length];
            b[1] = transactionId;
            b[5] = (byte)dataSize;
            b[6] = commandClass;
            b[7] = command;
            Buffer.BlockCopy(args, 0, b, 8, args.Length);
            b[88] = Crc(b);
            return new RazerPacket(b);
        }

        public static RazerPacket FromReport(byte[] report, int offset)
        {
            var b = new byte[Length];
            Buffer.BlockCopy(report, offset, b, 0, Length);
            return new RazerPacket(b);
        }

        public static byte Crc(byte[] packet)
        {
            byte crc = 0;
            for (int i = 2; i < 88; i++)
                crc ^= packet[i];
            return crc;
        }

        public RazerStatus Status => (RazerStatus)bytes[0];
        public byte TransactionId => bytes[1];
        public byte DataSize => bytes[5];
        public byte Class => bytes[6];
        public byte Command => bytes[7];

        public byte Arg(int index) => bytes[8 + index];

        public byte[] Args(int start, int count)
        {
            count = Math.Max(0, Math.Min(count, MaxArgs - start));
            var a = new byte[count];
            Buffer.BlockCopy(bytes, 8 + start, a, 0, count);
            return a;
        }

        // Os argumentos válidos segundo o campo de tamanho.
        public byte[] Data => Args(0, Math.Min((int)DataSize, MaxArgs));

        public void CopyTo(byte[] report, int offset) => Buffer.BlockCopy(bytes, 0, report, offset, Length);

        public override string ToString()
        {
            var sb = new StringBuilder();
            int n = 8 + Math.Min((int)DataSize, MaxArgs);
            for (int i = 0; i < n; i++)
                sb.Append(bytes[i].ToString("x2")).Append(i == n - 1 ? "" : " ");
            return sb.ToString();
        }
    }

    public sealed class RazerException : Exception
    {
        public RazerException(byte commandClass, byte command, RazerStatus? status, string detail = null)
            : base(BuildMessage(commandClass, command, status, detail))
        {
            CommandClass = commandClass;
            Command = command;
            Status = status;
        }

        public byte CommandClass { get; }
        public byte Command { get; }
        public RazerStatus? Status { get; }

        static string BuildMessage(byte commandClass, byte command, RazerStatus? status, string detail)
        {
            string what = status switch
            {
                RazerStatus.Failed => "o teclado recusou o comando",
                RazerStatus.NotSupported => "o teclado não suporta o comando",
                RazerStatus.Timeout => "o teclado informou tempo esgotado",
                RazerStatus.Busy => "o teclado continuou ocupado",
                null => detail ?? "o teclado não respondeu",
                _ => $"status inesperado 0x{(byte)status:X2}",
            };
            return $"Comando {commandClass:X2} {command:X2}: {what}.";
        }
    }
}
