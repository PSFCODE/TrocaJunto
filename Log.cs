namespace TrocaJunto;

/// <summary>Registro simples (TrocaJunto.log), com no máximo ~200 KB. Fica ao lado do programa;
/// se a pasta não deixar gravar (ex.: Arquivos de Programas), vai para %LOCALAPPDATA%\TrocaJunto.</summary>
public static class Log
{
    public static readonly string FilePath = PickPath();

    static string PickPath()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "TrocaJunto.log");
        try { using (File.Open(beside, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { } return beside; }
        catch
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrocaJunto");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "TrocaJunto.log");
        }
    }
    static readonly object gate = new();
    public static bool AlsoConsole;

    public static void Write(string msg)
    {
        var line = $"{DateTime.Now:dd/MM HH:mm:ss}  {msg}";
        if (AlsoConsole) Console.WriteLine(line);
        lock (gate)
        {
            try
            {
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > 200_000)
                {
                    var keep = File.ReadAllLines(FilePath).TakeLast(500);
                    File.WriteAllLines(FilePath, keep);
                }
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
            catch { }
        }
    }
}
