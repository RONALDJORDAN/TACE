using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TACEUI.Core
{
    public static class UsbPortResolver
    {
        private const uint DIGCF_PRESENT = 0x02;
        private const uint DIGCF_ALLCLASSES = 0x04;
        private const uint SPDRP_LOCATION_INFORMATION = 0x0D;

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string? enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint property, out uint propertyRegDataType, byte[]? propertyBuffer, uint propertyBufferSize, out uint requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        public static string ObterPorta(string? serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return string.Empty;
            string alvo = serial.Trim();

            IntPtr set = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
            if (set == IntPtr.Zero || set == new IntPtr(-1)) return string.Empty;

            try
            {
                string fallback = string.Empty;
                var data = new SP_DEVINFO_DATA();

                for (uint i = 0; ; i++)
                {
                    data.cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>();
                    if (!SetupDiEnumDeviceInfo(set, i, ref data))
                    {
                        if (i == 0) return string.Empty;
                        break;
                    }

                    var instanceId = new StringBuilder(512);
                    if (!SetupDiGetDeviceInstanceId(set, ref data, instanceId, instanceId.Capacity, out _)) continue;

                    string id = instanceId.ToString();
                    bool exato = id.EndsWith("\\" + alvo, StringComparison.OrdinalIgnoreCase);
                    if (!exato && id.IndexOf(alvo, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    string local = ObterLocationInfo(set, ref data);
                    if (string.IsNullOrWhiteSpace(local)) continue;
                    if (exato) return local;
                    if (string.IsNullOrEmpty(fallback)) fallback = local;
                }

                return fallback;
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(set);
            }
        }

        private static string ObterLocationInfo(IntPtr set, ref SP_DEVINFO_DATA data)
        {
            SetupDiGetDeviceRegistryProperty(set, ref data, SPDRP_LOCATION_INFORMATION, out _, null, 0, out uint tamanho);
            if (tamanho == 0) return string.Empty;

            var buffer = new byte[tamanho];
            if (!SetupDiGetDeviceRegistryProperty(set, ref data, SPDRP_LOCATION_INFORMATION, out _, buffer, tamanho, out tamanho))
                return string.Empty;

            return Encoding.Unicode.GetString(buffer, 0, (int)tamanho).TrimEnd('\0').Trim();
        }
    }
}
