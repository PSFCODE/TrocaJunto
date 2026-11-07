using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrocaJunto;

/// <summary>Uma "coleção" HID do Windows (cada aparelho tem várias).</summary>
public sealed record HidInfo(string Path, ushort Vid, ushort Pid, ushort UsagePage, ushort Usage, int InLen, int OutLen);

/// <summary>Acesso direto ao HID do Windows (hid.dll + setupapi), sem pacotes de fora.</summary>
public static class Hid
{
    const int DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;
    const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    [StructLayout(LayoutKind.Sequential)]
    struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        public fixed ushort Reserved[17];
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("hid.dll", SetLastError = true)] static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll", SetLastError = true)] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr pp);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr pp);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr pp, out HIDP_CAPS caps);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr hwnd, int flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid g, int idx, ref SP_DEVICE_INTERFACE_DATA d);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, int size, out int required, IntPtr devInfo);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sa, uint disp, uint flags, IntPtr tmpl);

    /// <summary>Lista as coleções HID presentes de um fabricante (0x046D = Logitech).</summary>
    public static List<HidInfo> Enumerate(ushort vid)
    {
        var list = new List<HidInfo>();
        HidD_GetHidGuid(out var g);
        var set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == IntPtr.Zero || set == new IntPtr(-1)) return list;
        try
        {
            for (int i = 0; ; i++)
            {
                var d = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d)) break;
                SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out int req, IntPtr.Zero);
                if (req <= 0) continue;
                var buf = Marshal.AllocHGlobal(req);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref d, buf, req, out _, IntPtr.Zero)) continue;
                    var path = Marshal.PtrToStringUni(buf + 4);
                    if (path is null || !path.Contains(vid.ToString("x4"), StringComparison.OrdinalIgnoreCase)) continue;
                    var info = Describe(path);
                    if (info is not null && info.Vid == vid) list.Add(info);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return list;
    }

    static HidInfo? Describe(string path)
    {
        // Acesso 0 = só consulta; funciona até em teclado/mouse, que o Windows tranca.
        using var h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h.IsInvalid) return null;
        var a = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HIDD_ATTRIBUTES>() };
        if (!HidD_GetAttributes(h, ref a)) return null;
        if (!HidD_GetPreparsedData(h, out var pp)) return null;
        try
        {
            HidP_GetCaps(pp, out var caps);
            return new HidInfo(path, a.VendorID, a.ProductID, caps.UsagePage, caps.Usage, caps.InputReportByteLength, caps.OutputReportByteLength);
        }
        finally { HidD_FreePreparsedData(pp); }
    }

    /// <summary>Abre o aparelho (modo simples), compartilhando com o Logi Options+.</summary>
    public static SafeFileHandle Open(string path)
    {
        var h = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h.IsInvalid) throw new IOException($"Não abriu o aparelho (erro {Marshal.GetLastWin32Error()}).");
        return h;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern unsafe bool ReadFile(SafeFileHandle h, byte* buf, int len, out int read, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern unsafe bool WriteFile(SafeFileHandle h, byte* buf, int len, out int written, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CancelIoEx(SafeFileHandle h, IntPtr ov);
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_SetOutputReport(SafeFileHandle h, byte[] buf, int len);
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetInputReport(SafeFileHandle h, byte[] buf, int len);

    public static unsafe int Read(SafeFileHandle h, byte[] buf)
    {
        fixed (byte* p = buf)
            return ReadFile(h, p, buf.Length, out int n, IntPtr.Zero) ? n : -Marshal.GetLastWin32Error();
    }

    public static unsafe int Write(SafeFileHandle h, byte[] buf)
    {
        fixed (byte* p = buf)
            return WriteFile(h, p, buf.Length, out int n, IntPtr.Zero) ? n : -Marshal.GetLastWin32Error();
    }
}
