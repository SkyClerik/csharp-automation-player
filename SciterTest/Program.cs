using EmptyFlow.SciterAPI;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CSharp.RuntimeBinder;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class ScriptGlobals
{
    public AppApi Api { get; set; } = null!;
}

public class Program
{
    private static SciterAPIHost? _host;

    [DllImport("libdl.so", EntryPoint = "dlopen")]
    private static extern IntPtr LinuxDlOpen(string filename, int flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x00000000;
    private const uint MB_YESNO = 0x00000004;
    private const uint MB_ICONQUESTION = 0x00000020;
    private const uint MB_ICONINFORMATION = 0x00000040;
    private const int IDYES = 6;
    private const int RTLD_NOW = 2;

    public static readonly ManualResetEventSlim CompletionLock = new(false);

    public static nint MainWindowHandle { get; private set; }

    [STAThread]
    static async Task Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        if (args.Length == 0)
        {
            RunInstaller();
            return;
        }

        // Если аргумент один (как в Win) — берем его. Если больше (Linux разбил пробелы) — склеиваем.
        string scriptPath = args.Length == 1 ? args[0] : string.Join(" ", args);
        scriptPath = scriptPath.Trim('"', '\''); // Чистим от случайных кавычек по краям

        var api = await ExecuteUserScriptAsync(scriptPath);

        if (api != null)
        {
            try
            {
                InitializeUserInterface(scriptPath, api);
            }
            catch (Exception ex)
            {
                // Держим терминал, если упадет инициализация Sciter
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ Ошибка инициализации GUI Sciter:\n{ex.Message}\n{ex.StackTrace}");
                Console.ResetColor();
                Console.WriteLine("\n=== Терминал зафиксирован. Нажмите любую клавишу... ===");
                Console.ReadKey();
            }
        }
    }

    private static void RunInstaller()
    {
        IAssociationManager manager;
        string platformName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            manager = new WindowsAssociationManager();
            platformName = "Windows (Registry)";
        }
        else
        {
            manager = new LinuxAssociationManager();
            platformName = "Linux (XDG MIME)";
        }

        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========================================");
            Console.WriteLine($"     C# Script Player Setup ({platformName})");
            Console.WriteLine("========================================");
            Console.ResetColor();
            Console.WriteLine($"\nТекущий путь: {Environment.ProcessPath}");
            Console.WriteLine("\n1. Установить / Обновить ассоциацию файла .csx");
            Console.WriteLine("2. Удалить ассоциацию из системы");
            Console.WriteLine("3. Выход");
            Console.Write("\nВаш выбор: ");

            string? choice = Console.ReadLine();
            if (choice == "1")
            {
                manager.ApplyAssociation();
            }
            else if (choice == "2")
            {
                manager.RemoveAssociation();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n[Успех] Все упоминания и ветки реестра успешно удалены.");
                }
            }
            else if (choice == "3")
            {
                break;
            }

            Console.ResetColor();
            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }
    }

    private static async Task<AppApi?> ExecuteUserScriptAsync(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            Alert($"Файл скрипта не найден: {scriptPath}", "Ошибка запуска");
            return null;
        }

        var api = new AppApi();
        var fullPath = Path.GetFullPath(scriptPath);
        string scriptDir = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory;

        string scriptCode = File.ReadAllText(scriptPath);

        var requiredAssemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Process).Assembly,
            typeof(AppApi).Assembly,
            typeof(Program).Assembly,
            typeof(RuntimeBinderException).Assembly
        };

        var options = ScriptOptions.Default;
        options = options.WithReferences(requiredAssemblies);
        options = options.WithSourceResolver(ScriptSourceResolver.Default.WithBaseDirectory(scriptDir));
        options = options.WithImports("System", "System.IO", "System.Diagnostics");

        try
        {
            await CSharpScript.EvaluateAsync(scriptCode, options, new ScriptGlobals { Api = api });
            return api;
        }
        catch (AggregateException aggEx)
        {
            string errors = "❌ ОШИБКА КОМПИЛЯЦИИ В СКРИПТЕ:\n";
            foreach (var inner in aggEx.InnerExceptions) errors += inner.Message + "\n";

            Console.WriteLine(errors);
            Alert(errors, "Ошибка компиляции Roslyn");
            return null;
        }
        catch (Exception ex)
        {
            string error = $"❌ ОШИБКА ВЫПОЛНЕНИЯ СКРИПТА:\n{ex.Message}";
            Console.WriteLine(error);
            Alert(error, "Ошибка выполнения Roslyn");
            return null;
        }
    }

    private static void InitializeUserInterface(string scriptPath, AppApi api)
    {
        _host = new SciterAPIHost(AppContext.BaseDirectory);
        _host.CreateWindow(asMain: true);
        MainWindowHandle = _host.MainWindow;

        api.Host = _host;

        var eventHandler = new PlayerEventHandler(_host.MainWindow, _host, api);
        _host.AddWindowEventHandler(eventHandler);

        LoadHtmlLayout(scriptPath, api);

        _host.Process();

        Thread.Sleep(Timeout.Infinite);
    }

    private static void LoadHtmlLayout(string scriptPath, AppApi api)
    {
        string fullPath = Path.GetFullPath(scriptPath);
        string scriptDir = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory;
        string finalHtmlPath = "";

        if (!string.IsNullOrEmpty(api.Html))
        {
            string inputHtml = api.Html.Trim();

            bool isFilePath = !inputHtml.Contains("<") && !inputHtml.Contains(">") &&
                (inputHtml.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || inputHtml.Contains(Path.DirectorySeparatorChar));

            if (!isFilePath)
            {
                if (!inputHtml.Contains("charset=", StringComparison.OrdinalIgnoreCase))
                    inputHtml = "<meta charset=\"utf-8\">" + inputHtml;

                _host!.LoadHtml(inputHtml);
                return;
            }

            finalHtmlPath = Path.IsPathRooted(inputHtml) ? inputHtml : Path.Combine(scriptDir, inputHtml);
        }

        if (string.IsNullOrEmpty(finalHtmlPath) || !File.Exists(finalHtmlPath))
            finalHtmlPath = Path.Combine(scriptDir, "ui.html");

        if (!File.Exists(finalHtmlPath))
            finalHtmlPath = Path.Combine(AppContext.BaseDirectory, "ui.html");

        if (File.Exists(finalHtmlPath))
            _host!.LoadFile(finalHtmlPath);
        else
            _host!.LoadHtml("<meta charset=\"utf-8\"><html><body style='background:#222;color:#fff;text-align:center;padding:50px;'><h1>Ошибка: ui.html не найден!</h1></body></html>");
    }

    public static void Alert(string message, string title)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MessageBox(MainWindowHandle, message, title, MB_OK | MB_ICONINFORMATION);
        }
        else
        {
            try
            {
                using var proc = Process.Start("zenity", $"--info --title=\"{Escape(title)}\" --text=\"{Escape(message)}\"");
                proc?.WaitForExit();
            }
            catch
            {
                Console.WriteLine($"[{title}] {message}");
            }
        }
    }

    public static bool Confirm(string message, string title)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            int result = MessageBox(MainWindowHandle, message, title, MB_YESNO | MB_ICONQUESTION);
            return result == IDYES;
        }
        else
        {
            try
            {
                using var proc = Process.Start("zenity", $"--question --title=\"{Escape(title)}\" --text=\"{Escape(message)}\"");
                proc?.WaitForExit();
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    private static string Escape(string text) => text?.Replace("\"", "\\\"") ?? string.Empty;

    public static void SetHtml(string cssSelector, string html)
    {
        if (_host == null) return;
        try
        {
            using var selector = _host.MakeCssSelector(cssSelector).GetEnumerator();
            if (selector.MoveNext()) _host.SetElementHtml(selector.Current, html, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error SetHtml: {ex.Message}");
        }
    }

    public static string GetValue(string cssSelector)
    {
        if (_host == null) return string.Empty;
        try
        {
            using var selector = _host.MakeCssSelector(cssSelector).GetEnumerator();
            if (selector.MoveNext())
            {
                var he = selector.Current;
                string val = _host.GetElementAttribute(he, "value");
                return !string.IsNullOrEmpty(val) ? val : _host.GetElementText(he);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error GetValue: {ex.Message}");
        }
        return string.Empty;
    }

    public static void SetValue(string cssSelector, string value)
    {
        if (_host == null) return;
        try
        {
            using var selector = _host.MakeCssSelector(cssSelector).GetEnumerator();
            if (selector.MoveNext())
            {
                var he = selector.Current;
                _host.SetElementAttribute(he, "value", value);
                _host.SetElementText(he, value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error SetValue: {ex.Message}");
        }
    }

    //public static void RunProcess(string filename)
    //{
    //    try
    //    {
    //        using var proc = Process.Start(new ProcessStartInfo { FileName = filename, UseShellExecute = true });
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"[Player Error] Сбой запуска процесса {filename}: {ex.Message}");
    //    }
    //}
}