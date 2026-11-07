using System.Collections.Concurrent;

namespace TrocaJunto;

/// <summary>
/// Conversa HID++ 2.0 (o protocolo da Logitech) com um aparelho ligado por Bluetooth.
/// Mensagem longa: [0x11, aparelho, índice do recurso, (função &lt;&lt; 4) | id, parâmetros...].
/// </summary>
public sealed class HidppDevice : IDisposable
{
    public const ushort F_ROOT = 0x0000, F_FEATURE_SET = 0x0001, F_DEVICE_NAME = 0x0005,
        F_CHANGE_HOST = 0x1814, F_REPROG_V4 = 0x1B04;

    const byte SW_ID = 0x0B;          // nossa "assinatura" nas respostas (o Options+ usa outras)
    const byte DEV_BLE = 0xFF;        // Bluetooth direto: aparelho sempre 0xFF

    public HidInfo Info { get; }
    public bool Alive { get; private set; } = true;
    public string? LastError { get; private set; }
    public int ReportsSeen;
    public static bool Debug;
    public event Action<byte[]>? Notification;
    public event Action? Disconnected;

    // Duas "alças": uma só lê (fica parada esperando) e outra só escreve.
    readonly Microsoft.Win32.SafeHandles.SafeFileHandle rh, wh;
    readonly BlockingCollection<byte[]> responses = new();
    readonly CancellationTokenSource cts = new();
    readonly Dictionary<ushort, byte> featureCache = new();
    readonly object writeLock = new();

    public HidppDevice(HidInfo info)
    {
        Info = info;
        rh = Hid.Open(info.Path);
        wh = Hid.Open(info.Path);
        new Thread(ReadLoop) { IsBackground = true, Name = "hidpp-" + info.Pid.ToString("X4") }.Start();
    }

    void ReadLoop()
    {
        var buf = new byte[Math.Max(Info.InLen, 20)];
        try
        {
            while (!cts.IsCancellationRequested)
            {
                int n = Hid.Read(rh, buf);
                if (n <= 0) { LastError = $"leitura: erro {-n}"; break; }
                var r = buf[..n];
                Interlocked.Increment(ref ReportsSeen);
                if (Debug) Console.WriteLine("    <- " + Convert.ToHexString(r));
                if (r[0] != 0x11 && r[0] != 0x10) continue;
                bool isError = r[2] == 0xFF || r[2] == 0x8F;
                if (isError || (r[3] & 0x0F) == SW_ID) responses.Add(r);
                else Notification?.Invoke(r);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { LastError = "leitura: " + ex.Message; }
        Alive = false;
        Disconnected?.Invoke();
    }

    /// <summary>Manda um pedido e espera a resposta. Devolve null se o aparelho respondeu erro ou sumiu.</summary>
    public byte[]? Request(byte featIndex, byte function, params byte[] args) => RequestWithTimeout(featIndex, function, 1500, args);

    public byte[]? RequestWithTimeout(byte featIndex, byte function, int timeoutMs, byte[] args)
    {
        if (!Alive) return null;
        var msg = new byte[Math.Max(Info.OutLen, 20)];
        msg[0] = 0x11; msg[1] = DEV_BLE; msg[2] = featIndex; msg[3] = (byte)((function << 4) | SW_ID);
        Array.Copy(args, 0, msg, 4, Math.Min(args.Length, msg.Length - 4));
        lock (writeLock)
        {
            while (responses.TryTake(out _)) { }
            int w = Hid.Write(wh, msg);
            if (w <= 0) { LastError = $"escrita: erro {-w}"; return null; }
            var deadline = Environment.TickCount64 + timeoutMs;
            while (true)
            {
                var left = deadline - Environment.TickCount64;
                if (left <= 0) return null;
                if (!responses.TryTake(out var r, (int)left)) return null;
                if (r[2] == featIndex && r[3] == msg[3]) return r;
                if ((r[2] == 0xFF || r[2] == 0x8F) && r[3] == featIndex && r[4] == msg[3]) return null;
            }
        }
    }

    /// <summary>Só envia, sem esperar resposta (usado para trocar de computador: o aparelho vai embora).</summary>
    public bool Send(byte featIndex, byte function, params byte[] args)
    {
        if (!Alive) return false;
        var msg = new byte[Math.Max(Info.OutLen, 20)];
        msg[0] = 0x11; msg[1] = DEV_BLE; msg[2] = featIndex; msg[3] = (byte)((function << 4) | SW_ID);
        Array.Copy(args, 0, msg, 4, Math.Min(args.Length, msg.Length - 4));
        lock (writeLock)
        {
            int w = Hid.Write(wh, msg);
            if (w <= 0) LastError = $"escrita: erro {-w}";
            return w > 0;
        }
    }

    /// <summary>Índice do recurso no aparelho (0 = não tem).</summary>
    public byte FeatureIndex(ushort featureId)
    {
        if (featureId == F_ROOT) return 0;
        if (featureCache.TryGetValue(featureId, out var idx)) return idx;
        var r = Request(0x00, 0, (byte)(featureId >> 8), (byte)featureId);
        idx = r?[4] ?? 0;
        if (r is not null) featureCache[featureId] = idx;
        return idx;
    }

    public string? Name()
    {
        var fi = FeatureIndex(F_DEVICE_NAME);
        if (fi == 0) return null;
        var c = Request(fi, 0);
        if (c is null) return null;
        int len = c[4];
        var chars = new List<byte>();
        while (chars.Count < len)
        {
            var p = Request(fi, 1, (byte)chars.Count);
            if (p is null) break;
            for (int i = 4; i < p.Length && chars.Count < len; i++) chars.Add(p[i]);
        }
        return System.Text.Encoding.ASCII.GetString(chars.ToArray()).TrimEnd('\0');
    }

    /// <summary>(quantos canais, canal atual começando em 0) ou null.</summary>
    public (int count, int current)? HostInfo()
    {
        var fi = FeatureIndex(F_CHANGE_HOST);
        if (fi == 0) return null;
        var r = Request(fi, 0);
        return r is null ? null : (r[4], r[5]);
    }

    /// <summary>Tipo do aparelho: 0 teclado, 3 mouse, 4 trackpad, 5 trackball (null = não respondeu).</summary>
    public int? DeviceType()
    {
        var fi = FeatureIndex(F_DEVICE_NAME);
        if (fi == 0) return null;
        var r = Request(fi, 2);
        return r?[4];
    }

    /// <summary>Liga/desliga o "desvio" de uma tecla: desviada, ela avisa o programa em vez de agir sozinha.
    /// É temporário: o próprio teclado esquece quando reconecta ou é desligado.</summary>
    public bool SetDivert(ushort cid, bool on)
    {
        var fi = FeatureIndex(F_REPROG_V4);
        if (fi == 0) return false;
        byte flags = (byte)(0x02 | (on ? 0x01 : 0x00)); // bit1 = "mudar o desvio", bit0 = desvio
        return Request(fi, 3, (byte)(cid >> 8), (byte)cid, flags, 0, 0) is not null;
    }

    public bool? IsDiverted(ushort cid)
    {
        var fi = FeatureIndex(F_REPROG_V4);
        if (fi == 0) return null;
        var r = Request(fi, 2, (byte)(cid >> 8), (byte)cid);
        return r is null ? null : (r[6] & 0x01) != 0;
    }

    /// <summary>Manda o aparelho para outro canal (0, 1 ou 2).</summary>
    public bool ChangeHost(int channel)
    {
        var fi = FeatureIndex(F_CHANGE_HOST);
        return fi != 0 && Send(fi, 1, (byte)channel);
    }

    public void Dispose()
    {
        cts.Cancel();
        try { Hid.CancelIoEx(rh, IntPtr.Zero); } catch { }
        try { rh.Dispose(); } catch { }
        try { wh.Dispose(); } catch { }
    }
}
