namespace TrocaJunto;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0] : "";

        if (mode == "sondar") { Probe(); return; }

        // "TrocaJunto.exe sair" fecha o que está rodando do jeito certo (soltando as teclas 1-2-3).
        using var quit = new EventWaitHandle(false, EventResetMode.ManualReset, QuitEventName);
        if (mode is "sair" or "exit") { quit.Set(); return; }

        using var single = new Mutex(true, @"Local\TrocaJunto", out bool first);
        if (!first) return; // já está rodando
        quit.Reset();

        if (mode == "console")
        {
            // Modo de teste: roda sem ícone e escreve tudo na tela. Para sair, crie o arquivo "parar" ao lado do programa.
            Log.AlsoConsole = true;
            var stopFile = Path.Combine(AppContext.BaseDirectory, "parar");
            File.Delete(stopFile);
            using (var motor = new Motor())
                while (!File.Exists(stopFile) && !quit.WaitOne(300)) { }
            File.Delete(stopFile);
            return;
        }

        ApplicationConfiguration.Initialize();
        var tray = new Tray();
        var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        new Thread(() => { quit.WaitOne(); ui.Post(_ => tray.ExitThread(), null); }) { IsBackground = true }.Start();
        Application.Run(tray);
    }

    public const string QuitEventName = @"Local\TrocaJunto-sair";

    public static readonly string Version =
        typeof(Program).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "?";

    /// <summary>Mostra o que o teclado e o mouse aceitam (só lê, não muda nada).</summary>
    static void Probe()
    {
        foreach (var c in Hid.Enumerate(0x046D).Where(c => c.UsagePage == 0xFF43 && c.OutLen >= 20))
        {
            using var d = new HidppDevice(c);
            var ping = d.Request(0x00, 1, 0, 0, 0x5A);
            if (ping is null) { Console.WriteLine($"PID {c.Pid:X4}: sem resposta"); continue; }
            var hi = d.HostInfo();
            Console.WriteLine($"PID {c.Pid:X4}: {d.Name()} | tipo {d.DeviceType()} | HID++ {ping[4]}.{ping[5]} | " +
                (hi is null ? "sem troca de canal" : $"{hi.Value.count} canais, atual {hi.Value.current + 1}"));
            foreach (ushort cid in new ushort[] { 0xD1, 0xD2, 0xD3 })
            {
                var dv = d.IsDiverted(cid);
                if (dv is not null) Console.WriteLine($"   tecla 0x{cid:X2}: capturada={dv}");
            }
        }
    }
}
