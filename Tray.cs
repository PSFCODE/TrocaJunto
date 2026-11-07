using System.Diagnostics;
using Microsoft.Win32;
using static TrocaJunto.L;

namespace TrocaJunto;

/// <summary>Ícone perto do relógio, com o estado e o menu (iniciar com o Windows, registro, site, sair).</summary>
public sealed class Tray : ApplicationContext
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppKey = @"Software\TrocaJunto";
    public const string Site = "https://github.com/PSFCODE/TrocaJunto";

    readonly NotifyIcon icon;
    readonly ToolStripMenuItem statusItem, autoItem;
    readonly Motor motor;
    readonly SynchronizationContext ui;

    public Tray()
    {
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        FirstRunEnableAutostart();

        statusItem = new ToolStripMenuItem(T("Procurando teclado e mouse…", "Looking for keyboard and mouse…")) { Enabled = false };
        autoItem = new ToolStripMenuItem(T("Iniciar com o Windows", "Start with Windows"), null, (_, _) => ToggleAutostart()) { Checked = IsAutostart() };
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(T($"TrocaJunto {Program.Version}: o mouse acompanha o teclado",
                                               $"TrocaJunto {Program.Version}: the mouse follows the keyboard")) { Enabled = false });
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(autoItem);
        menu.Items.Add(T("Ver o registro", "Open the log"), null, (_, _) => Open(Log.FilePath));
        menu.Items.Add(T("Site e novas versões", "Website and updates"), null, (_, _) => Open(Site));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(T("Sair (teclas 1-2-3 voltam ao normal)", "Exit (keys 1-2-3 back to normal)"), null, (_, _) => ExitThread());

        icon = new NotifyIcon { Icon = LoadIcon(), Text = "TrocaJunto", ContextMenuStrip = menu, Visible = true };

        motor = new Motor();
        motor.StatusChanged += s => ui.Post(_ => ShowStatus(s), null);
    }

    void ShowStatus(string s)
    {
        statusItem.Text = s;
        var tip = "TrocaJunto — " + s;
        icon.Text = tip.Length > 127 ? tip[..127] : tip;
    }

    static bool IsAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue("TrocaJunto") is string;
    }

    static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue("TrocaJunto", $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue("TrocaJunto", false);
    }

    void ToggleAutostart()
    {
        SetAutostart(!IsAutostart());
        autoItem.Checked = IsAutostart();
    }

    static void FirstRunEnableAutostart()
    {
        using var k = Registry.CurrentUser.CreateSubKey(AppKey);
        if (k.GetValue("Configurado") is null)
        {
            SetAutostart(true);
            k.SetValue("Configurado", 1);
        }
        else if (IsAutostart())
        {
            SetAutostart(true); // atualiza o caminho, se o programa mudou de pasta
        }
    }

    static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
    }

    /// <summary>Ícone embutido no programa (TrocaJunto.ico), no tamanho pequeno do relógio.</summary>
    static Icon LoadIcon()
    {
        using var s = typeof(Tray).Assembly.GetManifestResourceStream("TrocaJunto.TrocaJunto.ico");
        return s is null ? SystemIcons.Application : new Icon(s, SystemInformation.SmallIconSize);
    }

    protected override void ExitThreadCore()
    {
        icon.Visible = false;
        motor.Dispose();
        icon.Dispose();
        base.ExitThreadCore();
    }
}
