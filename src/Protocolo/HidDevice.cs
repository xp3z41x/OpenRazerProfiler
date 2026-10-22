using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OpenRazerProfiler.Protocolo
{
    // Uma interface HID (uma coleção de topo) encontrada no sistema.
    public sealed class HidDeviceInfo
    {
        public HidDeviceInfo(string path, ushort vendorId, ushort productId, int interfaceNumber,
            ushort usagePage, ushort usage, int featureReportLength)
        {
            Path = path;
            VendorId = vendorId;
            ProductId = productId;
            InterfaceNumber = interfaceNumber;
            UsagePage = usagePage;
            Usage = usage;
            FeatureReportLength = featureReportLength;
        }

        public string Path { get; }
        public ushort VendorId { get; }
        public ushort ProductId { get; }
        public int InterfaceNumber { get; }   // MI_xx do caminho; -1 se o aparelho não for composto
        public ushort UsagePage { get; }
        public ushort Usage { get; }
        public int FeatureReportLength { get; }

        public override string ToString() =>
            $"MI_{InterfaceNumber:X2} página 0x{UsagePage:X4} uso 0x{Usage:X2} feature {FeatureReportLength} B";
    }

    public static class HidEnumerator
    {
        public static List<HidDeviceInfo> Enumerate(ushort vendorId, ushort productId)
        {
            var found = new List<HidDeviceInfo>();
            NativeHid.HidD_GetHidGuid(out Guid hidGuid);
            IntPtr set = NativeHid.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
                NativeHid.DIGCF_PRESENT | NativeHid.DIGCF_DEVICEINTERFACE);
            if (set == NativeHid.INVALID_HANDLE_VALUE)
                return found;
            try
            {
                string idFilter = $"vid_{vendorId:x4}&pid_{productId:x4}";
                var data = new NativeHid.SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf(typeof(NativeHid.SP_DEVICE_INTERFACE_DATA)) };
                for (int i = 0; NativeHid.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref data); i++)
                {
                    string path = GetPath(set, ref data);
                    if (path == null || path.IndexOf(idFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    var info = Describe(path);
                    if (info != null && info.VendorId == vendorId && info.ProductId == productId)
                        found.Add(info);
                }
            }
            finally
            {
                NativeHid.SetupDiDestroyDeviceInfoList(set);
            }
            return found;
        }

        static string GetPath(IntPtr set, ref NativeHid.SP_DEVICE_INTERFACE_DATA data)
        {
            NativeHid.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out int size, IntPtr.Zero);
            if (size <= 0)
                return null;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                // cbSize de SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 em 64 bits, 6 em 32 bits.
                Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                if (!NativeHid.SetupDiGetDeviceInterfaceDetail(set, ref data, buffer, size, out _, IntPtr.Zero))
                    return null;
                return Marshal.PtrToStringUni(IntPtr.Add(buffer, 4));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        static HidDeviceInfo Describe(string path)
        {
            using (var handle = HidFeatureChannel.OpenHandle(path, false))
            {
                if (handle.IsInvalid)
                    return null;
                var attributes = new NativeHid.HIDD_ATTRIBUTES { Size = Marshal.SizeOf(typeof(NativeHid.HIDD_ATTRIBUTES)) };
                if (!NativeHid.HidD_GetAttributes(handle, ref attributes))
                    return null;
                ushort usagePage = 0, usage = 0;
                int featureLength = 0;
                if (NativeHid.HidD_GetPreparsedData(handle, out IntPtr preparsed))
                {
                    try
                    {
                        if (NativeHid.HidP_GetCaps(preparsed, out var caps) == NativeHid.HIDP_STATUS_SUCCESS)
                        {
                            usagePage = caps.UsagePage;
                            usage = caps.Usage;
                            featureLength = caps.FeatureReportByteLength;
                        }
                    }
                    finally
                    {
                        NativeHid.HidD_FreePreparsedData(preparsed);
                    }
                }
                return new HidDeviceInfo(path, attributes.VendorID, attributes.ProductID,
                    ParseInterfaceNumber(path), usagePage, usage, featureLength);
            }
        }

        static int ParseInterfaceNumber(string path)
        {
            int i = path.IndexOf("&mi_", StringComparison.OrdinalIgnoreCase);
            if (i < 0 || i + 6 > path.Length)
                return -1;
            return int.TryParse(path.Substring(i + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int n) ? n : -1;
        }
    }

    // Canal de feature reports de uma interface HID.
    public sealed class HidFeatureChannel : IDisposable
    {
        readonly SafeFileHandle handle;

        public HidFeatureChannel(string path)
        {
            Path = path;
            // Teclados são abertos em modo exclusivo pelo Windows; sem acesso de leitura/escrita
            // os feature reports continuam funcionando.
            handle = OpenHandle(path, true);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                handle = OpenHandle(path, false);
            }
            if (handle.IsInvalid)
                throw new IOException($"Não foi possível abrir o dispositivo (erro {Marshal.GetLastWin32Error()}).");
        }

        public string Path { get; }

        internal static SafeFileHandle OpenHandle(string path, bool readWrite) =>
            NativeHid.CreateFile(path, readWrite ? NativeHid.GENERIC_READ | NativeHid.GENERIC_WRITE : 0,
                NativeHid.FILE_SHARE_READ | NativeHid.FILE_SHARE_WRITE, IntPtr.Zero, NativeHid.OPEN_EXISTING, 0, IntPtr.Zero);

        public bool SetFeature(byte[] report) => NativeHid.HidD_SetFeature(handle, report, report.Length);

        public bool GetFeature(byte[] report) => NativeHid.HidD_GetFeature(handle, report, report.Length);

        public void Dispose() => handle.Dispose();
    }
}
