using System;
using System.Linq;
using System.Text;

namespace OpenRazerProfiler.Protocolo
{
    // Metadados de perfil (05 88 / 05 08). O firmware guarda um bloco opaco; o nome é interpretado aqui.
    // Formatos encontrados:
    //   - fábrica: nome em UTF-16BE ("default", 14 bytes);
    //   - Synapse: GUID (16) + nome UTF-8 com zeros (100) + hash hexadecimal (64) + zeros, 250 bytes no total.
    public static class ProfileMetadata
    {
        public const int MaxNameLength = 32;
        const int SynapseLength = 250;
        const int SynapseNameOffset = 16;
        const int SynapseNameLength = 100;
        const int SynapseHashOffset = 116;
        const int SynapseHashLength = 64;

        public static bool IsSynapseFormat(byte[] metadata)
        {
            if (metadata == null || metadata.Length != SynapseLength)
                return false;
            for (int i = SynapseHashOffset; i < SynapseHashOffset + SynapseHashLength; i++)
            {
                char c = (char)metadata[i];
                bool hex = c == 0 || (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                    return false;
            }
            return true;
        }

        public static string DecodeName(byte[] metadata)
        {
            if (metadata == null || metadata.Length == 0)
                return "";
            if (IsSynapseFormat(metadata))
            {
                int end = Array.IndexOf(metadata, (byte)0, SynapseNameOffset, SynapseNameLength);
                int len = (end < 0 ? SynapseNameOffset + SynapseNameLength : end) - SynapseNameOffset;
                return Clean(Encoding.UTF8.GetString(metadata, SynapseNameOffset, len));
            }
            if (metadata.Length % 2 == 0)
            {
                string s = Encoding.BigEndianUnicode.GetString(metadata).TrimEnd('\0');
                if (s.Length > 0 && s.All(ch => !char.IsControl(ch)))
                    return s;
            }
            return "";
        }

        // Grava o nome mantendo o formato existente (Synapse); senão usa o formato de fábrica (UTF-16BE).
        public static byte[] EncodeName(string name, byte[] existing)
        {
            name = Clean(name ?? "");
            if (name.Length > MaxNameLength)
                name = name.Substring(0, MaxNameLength);
            if (IsSynapseFormat(existing))
            {
                var m = (byte[])existing.Clone();
                Array.Clear(m, SynapseNameOffset, SynapseNameLength);
                var utf8 = Encoding.UTF8.GetBytes(name);   // 32 caracteres sempre cabem nos 100 bytes
                Buffer.BlockCopy(utf8, 0, m, SynapseNameOffset, Math.Min(utf8.Length, SynapseNameLength - 1));
                return m;
            }
            return Encoding.BigEndianUnicode.GetBytes(name);
        }

        static string Clean(string s) => new string(s.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
    }
}
