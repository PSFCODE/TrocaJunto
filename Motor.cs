using System.Collections.Concurrent;
using static TrocaJunto.L;

namespace TrocaJunto;

/// <summary>
/// O coração do TrocaJunto. Fica de olho no teclado e no mouse Logitech ligados neste computador.
/// Quando o teclado está aqui, "captura" as teclas Easy-Switch 1-2-3. Ao apertar uma delas,
/// manda primeiro o mouse e depois o teclado para o mesmo canal.
/// Se o mouse estiver dormindo, ele é mandado assim que acordar aqui (por até 30 minutos).
/// </summary>
public sealed class Motor : IDisposable
{
    static readonly ushort[] HostKeys = { 0x00D1, 0x00D2, 0x00D3 };
    static readonly TimeSpan PendingMax = TimeSpan.FromMinutes(30);

    public event Action<string>? StatusChanged;
    public string Status { get; private set; } = T("Procurando teclado e mouse…", "Looking for keyboard and mouse…");

    readonly BlockingCollection<Action> work = new();
    readonly Thread thread;
    readonly HashSet<string> ignored = new(StringComparer.OrdinalIgnoreCase);
    readonly bool[] pressed = new bool[3];
    volatile bool stopping;

    HidppDevice? kb, mouse;
    int kbChannel = -1, mouseChannel = -1;
    int? pending; DateTime pendingAt;
    DateTime nextDivertCheck, kbArrivedAt, leavingUntil;

    public Motor()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "motor" };
        thread.Start();
    }

    void Run()
    {
        Log.Write(T($"TrocaJunto {Program.Version} ligado.", $"TrocaJunto {Program.Version} started."));
        while (!stopping)
        {
            try
            {
                if (work.TryTake(out var job, 1000)) { job(); continue; }
                Tick();
            }
            catch (Exception ex) { Log.Write(T("Erro: ", "Error: ") + ex); }
        }
    }

    void Tick()
    {
        var cols = Hid.Enumerate(0x046D).Where(c => c.UsagePage == 0xFF43 && c.OutLen >= 20).ToList();
        var paths = new HashSet<string>(cols.Select(c => c.Path), StringComparer.OrdinalIgnoreCase);

        if (kb is not null && (!kb.Alive || !paths.Contains(kb.Info.Path)))
        {
            Log.Write(T("Teclado saiu deste computador.", "Keyboard left this computer."));
            kb.Dispose(); kb = null; kbChannel = -1;
            Array.Clear(pressed);
            UpdateStatus();
        }
        if (mouse is not null && (!mouse.Alive || !paths.Contains(mouse.Info.Path)))
        {
            Log.Write(T("Mouse saiu deste computador.", "Mouse left this computer."));
            mouse.Dispose(); mouse = null; mouseChannel = -1;
            UpdateStatus();
        }
        ignored.RemoveWhere(p => !paths.Contains(p));
        CheckReceiver();

        foreach (var c in cols)
        {
            if (c.Path.Equals(kb?.Info.Path, StringComparison.OrdinalIgnoreCase) ||
                c.Path.Equals(mouse?.Info.Path, StringComparison.OrdinalIgnoreCase) ||
                ignored.Contains(c.Path)) continue;

            HidppDevice d;
            try { d = new HidppDevice(c); } catch { continue; }
            var type = d.DeviceType();
            if (type is null) { d.Dispose(); continue; } // ainda acordando: tenta no próximo giro
            bool canSwitch = d.FeatureIndex(HidppDevice.F_CHANGE_HOST) != 0;
            if (type == 0 && kb is null && canSwitch && d.FeatureIndex(HidppDevice.F_REPROG_V4) != 0)
            {
                kb = d;
                KeyboardArrived();
            }
            else if (type is 3 or 4 or 5 && mouse is null && canSwitch)
            {
                mouse = d;
                MouseArrived();
            }
            else { d.Dispose(); ignored.Add(c.Path); }
        }

        // Confere se as teclas continuam capturadas (o Logi Options+ pode soltar ao configurar o teclado):
        // logo depois de chegar, aos 10 s e 30 s; depois, a cada minuto.
        // Logo depois de mandar o teclado embora, não recaptura (ele pode levar alguns segundos para sair).
        if (kb is not null && DateTime.Now >= nextDivertCheck && DateTime.Now >= leavingUntil)
        {
            var since = DateTime.Now - kbArrivedAt;
            nextDivertCheck = DateTime.Now.AddSeconds(since < TimeSpan.FromSeconds(10) ? 10 : since < TimeSpan.FromSeconds(30) ? 20 : 60);
            var loose = HostKeys.Where(cid => kb.IsDiverted(cid) == false).ToList();
            if (loose.Count > 0)
            {
                Log.Write(T($"{loose.Count} tecla(s) Easy-Switch estavam soltas. Capturando de novo.",
                            $"{loose.Count} Easy-Switch key(s) were released. Capturing again."));
                foreach (var cid in loose) kb.SetDivert(cid, true);
            }
        }
    }

    void KeyboardArrived()
    {
        var k = kb!;
        // Captura primeiro (cada pedido leva ~0,2 s pelo Bluetooth); nome e canal depois.
        k.Notification += r => work.Add(() => OnKeyboardEvent(r));
        leavingUntil = DateTime.MinValue;
        DivertAll(true);
        kbChannel = k.HostInfo()?.current ?? -1;
        Log.Write(T($"Teclado chegou: {k.Name()} (canal {kbChannel + 1}).", $"Keyboard connected: {k.Name()} (channel {kbChannel + 1})."));
        if (pending is not null)
        {
            Log.Write(T("Teclado voltou: troca pendente do mouse cancelada.", "Keyboard is back: pending mouse switch cancelled."));
            pending = null;
        }
        kbArrivedAt = DateTime.Now;
        nextDivertCheck = DateTime.Now.AddSeconds(3);
        UpdateStatus();
    }

    void MouseArrived()
    {
        var m = mouse!;
        mouseChannel = m.HostInfo()?.current ?? -1;
        Log.Write(T($"Mouse chegou: {m.Name()} (canal {mouseChannel + 1}).", $"Mouse connected: {m.Name()} (channel {mouseChannel + 1})."));
        if (pending is int target)
        {
            bool fresh = DateTime.Now - pendingAt < PendingMax;
            pending = null;
            if (kb is null && fresh && target != mouseChannel)
            {
                Log.Write(T($"O teclado já foi para o canal {target + 1}: mandando o mouse atrás.",
                            $"The keyboard already went to channel {target + 1}: sending the mouse after it."));
                SendMouse(target);
            }
        }
        UpdateStatus();
    }

    void DivertAll(bool on)
    {
        var k = kb;
        if (k is null) return;
        int ok = HostKeys.Count(cid => k.SetDivert(cid, on));
        Log.Write(on ? T($"Teclas 1-2-3 capturadas ({ok}/3).", $"Keys 1-2-3 captured ({ok}/3).")
                     : T($"Teclas 1-2-3 soltas ({ok}/3).", $"Keys 1-2-3 released ({ok}/3)."));
    }

    void OnKeyboardEvent(byte[] r)
    {
        var k = kb;
        if (k is null) return;
        byte reprog = k.FeatureIndex(HidppDevice.F_REPROG_V4);
        if (r[2] != reprog || r[3] != 0x00) return; // só "tecla desviada apertada/solta"

        var down = new HashSet<int>();
        for (int i = 4; i + 1 < r.Length; i += 2) { int cid = (r[i] << 8) | r[i + 1]; if (cid != 0) down.Add(cid); }

        for (int i = 0; i < 3; i++)
        {
            bool isDown = down.Contains(HostKeys[i]);
            if (isDown && !pressed[i]) SwitchTo(i);
            pressed[i] = isDown;
        }
    }

    void SwitchTo(int target)
    {
        var k = kb;
        if (k is null) return;
        if (target == kbChannel)
        {
            Log.Write(T($"Tecla {target + 1}: já estamos neste computador.", $"Key {target + 1}: already on this computer."));
            return;
        }
        Log.Write(T($"Tecla {target + 1} apertada: trocando mouse e teclado para o canal {target + 1}.",
                    $"Key {target + 1} pressed: switching mouse and keyboard to channel {target + 1}."));

        if (mouse is { Alive: true } m)
        {
            if (m.ChangeHost(target)) Log.Write(T("  Mouse mandado.", "  Mouse sent."));
            else
            {
                Log.Write(T("  Mouse não recebeu: vai assim que acordar.", "  Mouse did not receive it: it will go as soon as it wakes up."));
                SetPending(target);
            }
            Thread.Sleep(120);
        }
        else
        {
            Log.Write(T("  Mouse dormindo/fora: vai assim que acordar aqui.", "  Mouse asleep/away: it will go as soon as it wakes up here."));
            SetPending(target);
        }

        // Solta as teclas antes de mandar o teclado (sem esperar resposta, para não atrasar):
        // no outro computador elas funcionam normalmente, mesmo sem o TrocaJunto lá.
        leavingUntil = DateTime.Now.AddSeconds(15);
        byte reprog = k.FeatureIndex(HidppDevice.F_REPROG_V4);
        foreach (var cid in HostKeys) k.Send(reprog, 3, (byte)(cid >> 8), (byte)cid, 0x02, 0, 0);
        if (k.ChangeHost(target)) Log.Write(T("  Teclado mandado.", "  Keyboard sent."));
        else Log.Write(T("  Teclado não recebeu a ordem.", "  Keyboard did not receive the command."));

        // Confere: se o mouse continuar aqui depois de 2 s, manda de novo.
        _ = Task.Delay(2000).ContinueWith(_ => work.Add(() =>
        {
            if (mouse is { Alive: true } still && still.HostInfo()?.current is int cur && cur != target)
            {
                Log.Write(T("  Mouse ainda aqui: mandando de novo.", "  Mouse still here: sending again."));
                still.ChangeHost(target);
            }
        }));
    }

    void SendMouse(int target)
    {
        if (mouse?.ChangeHost(target) == true) Log.Write(T("  Mouse mandado.", "  Mouse sent."));
    }

    void SetPending(int target) { pending = target; pendingAt = DateTime.Now; }

    void UpdateStatus()
    {
        string t = kb is null ? T("teclado fora", "keyboard away") : T($"teclado no canal {kbChannel + 1}", $"keyboard on channel {kbChannel + 1}");
        string m = mouse is null ? T("mouse fora/dormindo", "mouse away/asleep") : T($"mouse no canal {mouseChannel + 1}", $"mouse on channel {mouseChannel + 1}");
        Status = $"{t} · {m}";
        if (kb is null && mouse is null && receiverSeen)
            Status = T("Receptor USB Logitech: ainda não suportado (só Bluetooth)", "Logitech USB receiver: not supported yet (Bluetooth only)");
        else if (kb is not null && mouse is not null && kbChannel >= 0 && mouseChannel >= 0 && kbChannel != mouseChannel)
        {
            Status = T($"ATENÇÃO: teclado no canal {kbChannel + 1}, mouse no {mouseChannel + 1}",
                       $"WARNING: keyboard on channel {kbChannel + 1}, mouse on {mouseChannel + 1}");
            if (!mismatchLogged)
            {
                Log.Write(T($"Atenção: neste computador o teclado está no canal {kbChannel + 1} e o mouse no {mouseChannel + 1}. " +
                            "Pareie os dois no mesmo número para o mouse ir ao computador certo.",
                            $"Warning: on this computer the keyboard is on channel {kbChannel + 1} and the mouse on {mouseChannel + 1}. " +
                            "Pair both on the same number so the mouse goes to the right computer."));
                mismatchLogged = true;
            }
        }
        StatusChanged?.Invoke(Status);
    }

    bool mismatchLogged, receiverSeen;
    DateTime nextReceiverCheck;

    /// <summary>Teclado/mouse pelo receptor USB (Unifying/Bolt) usam outro caminho, que o TrocaJunto ainda não fala.</summary>
    void CheckReceiver()
    {
        if (DateTime.Now < nextReceiverCheck) return;
        nextReceiverCheck = DateTime.Now.AddSeconds(30);
        bool seen = Hid.Enumerate(0x046D).Any(c => c.UsagePage == 0xFF00 && c.OutLen >= 20);
        if (seen && !receiverSeen)
            Log.Write(T("Encontrei um receptor USB Logitech (Unifying/Bolt). O TrocaJunto só controla teclado e mouse ligados por Bluetooth.",
                        "Found a Logitech USB receiver (Unifying/Bolt). TrocaJunto only controls keyboards and mice connected over Bluetooth."));
        if (seen != receiverSeen) { receiverSeen = seen; UpdateStatus(); }
    }

    public void Dispose()
    {
        // Ao sair, devolve as teclas 1-2-3 ao teclado (voltam a trocar só o teclado).
        var done = new ManualResetEventSlim();
        work.Add(() => { try { DivertAll(false); } finally { done.Set(); } });
        done.Wait(3000);
        stopping = true;
        thread.Join(2000);
        kb?.Dispose(); mouse?.Dispose();
        Log.Write(T("TrocaJunto desligado.", "TrocaJunto stopped."));
    }
}
