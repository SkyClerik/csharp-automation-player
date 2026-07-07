using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Win32;
using EmptyFlow.SciterAPI;

public class ScriptGlobals
{
    public AppApi Api { get; set; } = null!;
}

public class Program
{
    private static SciterAPIHost? _host;
    public static nint MainWindowHandle { get; private set; }

    [STAThread]
    static void Main(string[] args)
    {
        // 1. Без аргументов запускаем интерактивный установщик
        if (args.Length == 0)
        {
            RunInstaller();
            return;
        }

        // 2. С аргументами компилируем скрипт и запускаем UI
        string scriptPath = args[0];
        var api = ExecuteUserScript(scriptPath);

        if (api != null)
        {
            InitializeUserInterface(scriptPath, api);
        }
    }

    private static void RunInstaller()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            RunWindowsInstallerMenu();
        }
        else
        {
            RunLinuxInstallerMenu();
        }
    }

    private static AppApi? ExecuteUserScript(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            Alert($"Файл скрипта не найден: {scriptPath}", "Ошибка запуска");
            return null;
        }

        var api = new AppApi();
        var fullPath = Path.GetFullPath(scriptPath);
        string scriptDir = Path.GetDirectoryName(fullPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        string scriptCode = File.ReadAllText(scriptPath, System.Text.Encoding.UTF8);


        var options = ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,
                typeof(Process).Assembly,
                typeof(AppApi).Assembly,
                typeof(Program).Assembly,
                typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException).Assembly
            )
            .WithSourceResolver(ScriptSourceResolver.Default.WithBaseDirectory(scriptDir))
            .WithImports("System", "System.IO", "System.Diagnostics");

        try
        {
            CSharpScript.EvaluateAsync(scriptCode, options, new ScriptGlobals { Api = api }).Wait();
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
    }

    private static void InitializeUserInterface(string scriptPath, AppApi api)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            static extern bool SetDllDirectory(string lpPathName);
            SetDllDirectory(AppDomain.CurrentDomain.BaseDirectory);
        }

        _host = new SciterAPIHost(AppDomain.CurrentDomain.BaseDirectory);
        _host.CreateWindow(asMain: true);
        MainWindowHandle = _host.MainWindow;

        api.Host = _host;
        _host.AddWindowEventHandler(new PlayerEventHandler(_host.MainWindow, _host, api));

        LoadHtmlLayout(scriptPath, api);

        // НАДЕЖНЫЙ МОНИТОР ЗАКРЫТИЯ ОКНА (РАБОТАЕТ НА ВСЕХ ОС)
        var windowMonitor = new System.Threading.Thread(() =>
        {
            while (true)
            {
                System.Threading.Thread.Sleep(250);

                // Кроссплатформенная проверка: если дескриптор окна потерял валидность в ОС
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    [DllImport("user32.dll")] static extern bool IsWindow(nint hWnd);
                    if (!IsWindow(MainWindowHandle)) Environment.Exit(0);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // На Linux проверяем, жива ли графическая подсистема окна через проверку хоста
                    // Если хост уничтожен или сигнализирует о закрытии — тушим cmd
                    if (_host == null) Environment.Exit(0);
                }
            }
        })
        { IsBackground = true };
        windowMonitor.Start();

        // Запускаем цикл UI. Когда окно закроется, этот метод ДОЛЖЕН вернуть управление
        _host.Process();

        // Если мы дошли сюда — гарантированно уничтожаем cmd процесс
        Environment.Exit(0);
    }

    private static void LoadHtmlLayout(string scriptPath, AppApi api)
    {
        string scriptDir = Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? AppDomain.CurrentDomain.BaseDirectory;
        string finalHtmlPath = "";

        if (!string.IsNullOrEmpty(api.Html))
        {
            string inputHtml = api.Html.Trim();
            bool isFilePath = !inputHtml.Contains("<") && !inputHtml.Contains(">") &&
                             (inputHtml.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || inputHtml.Contains(Path.DirectorySeparatorChar));

            if (!isFilePath)
            {
                // АВТО-ФИКС КОДИРОВКИ ДЛЯ СЫРОГО HTML:
                // Если скрипт передал текст верстки напрямую, и там нет тега charset, внедряем его в самое начало
                if (!inputHtml.Contains("charset=", StringComparison.OrdinalIgnoreCase))
                {
                    inputHtml = "<meta charset=\"utf-8\">" + inputHtml;
                }

                _host!.LoadHtml(inputHtml);
                return;
            }
            finalHtmlPath = Path.IsPathRooted(inputHtml) ? inputHtml : Path.Combine(scriptDir, inputHtml);
        }

        if (string.IsNullOrEmpty(finalHtmlPath) || !File.Exists(finalHtmlPath))
            finalHtmlPath = Path.Combine(scriptDir, "ui.html");

        if (!File.Exists(finalHtmlPath))
            finalHtmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui.html");

        if (!File.Exists(finalHtmlPath))
        {
            _host!.LoadHtml("<meta charset=\"utf-8\"><html><body style='background:#222;color:#fff;text-align:center;padding:50px;'><h1>Ошибка: ui.html не найден!</h1></body></html>");
        }
        else
        {
            _host!.LoadFile(finalHtmlPath);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RunWindowsInstallerMenu()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("========================================");
        Console.WriteLine("     C# Script Player Integration       ");
        Console.WriteLine("========================================");
        Console.ResetColor();
        Console.WriteLine($"\nТекущий путь: {Environment.ProcessPath}");
        Console.WriteLine("\n1. Установить / Обновить ассоциацию .csx");
        Console.WriteLine("2. Удалить ассоциацию (Очистить реестр)");
        Console.WriteLine("3. Выход");
        Console.Write("\nВаш выбор: ");

        string? choice = Console.ReadLine();
        if (choice == "1") ApplyWindowsAssociation();
        else if (choice == "2") RemoveWindowsAssociationWithFeedback();

        Console.ResetColor();
        Console.WriteLine("\nНажмите любую клавишу для выхода...");
        Console.ReadKey();
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindowsAssociation()
    {
        try
        {
            string currentExe = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory;
            RemoveWindowsAssociation();

            using (var key = Registry.ClassesRoot.CreateSubKey(".csx")) key.SetValue("", "CSharpScriptAutomation");
            using (var key = Registry.ClassesRoot.CreateSubKey(@"CSharpScriptAutomation\shell\Run"))
            {
                key.SetValue("", "Запустить в C# Плеере");
                using (var commandKey = key.CreateSubKey("command")) commandKey.SetValue("", $"\"{currentExe}\" \"%1\"");
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[Успех] Ассоциация успешно обновлена под текущий путь!");
        }
        catch (UnauthorizedAccessException) { ShowAdminError(); }
        catch (Exception ex) { Console.WriteLine($"\nОшибка: {ex.Message}"); }
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveWindowsAssociationWithFeedback()
    {
        try
        {
            RemoveWindowsAssociation();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[Успех] Все упоминания и ветки реестра успешно удалены.");
        }
        catch (UnauthorizedAccessException) { ShowAdminError(); }
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveWindowsAssociation()
    {
        if (Registry.ClassesRoot.OpenSubKey(".csx") != null) Registry.ClassesRoot.DeleteSubKeyTree(".csx", false);
        if (Registry.ClassesRoot.OpenSubKey("CSharpScriptAutomation") != null) Registry.ClassesRoot.DeleteSubKeyTree("CSharpScriptAutomation", false);
    }

    private static void ShowAdminError()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n[Ошибка] Недостаточно прав! Запустите от имени АДМИНИСТРАТОРА.");
    }

    private static void RunLinuxInstallerMenu()
    {
        Console.Clear();
        Console.WriteLine("=== Linux Integration ===");
        Console.WriteLine("Интеграция выполняется копированием бинарника в /usr/local/bin/");
        Console.ReadKey();
    }

    public static void Alert(string message, string title)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int MessageBox(nint hWnd, string text, string caption, uint type);
            MessageBox(MainWindowHandle, message, title, 0x00000000 | 0x00000040);
        }
        else
        {
            try { Process.Start("zenity", $"--info --title=\"{title}\" --text=\"{message}\""); }
            catch { Console.WriteLine($"[{title}] {message}"); }
        }
    }

    public static bool Confirm(string message, string title)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int MessageBox(nint hWnd, string text, string caption, uint type);
            int result = MessageBox(MainWindowHandle, message, title, 0x00000004 | 0x00000020);
            return result == 6;
        }
        else
        {
            try
            {
                var proc = Process.Start("zenity", $"--question --title=\"{title}\" --text=\"{message}\"");
                proc?.WaitForExit();
                return proc?.ExitCode == 0;
            }
            catch { return false; }
        }
    }

    public static void SetHtml(string cssSelector, string html)
    {
        if (_host == null) return;
        try
        {
            var selector = _host.MakeCssSelector(cssSelector).GetEnumerator();
            if (selector.MoveNext()) _host.SetElementHtml(selector.Current, html, 0);
        }
        catch (Exception ex) { Console.WriteLine($"Error SetHtml: {ex.Message}"); }
    }

    public static string GetValue(string cssSelector)
    {
        if (_host == null) return string.Empty;
        try
        {
            var selector = _host.MakeCssSelector(cssSelector).GetEnumerator();
            if (selector.MoveNext())
            {
                var he = selector.Current;
                string val = _host.GetElementAttribute(he, "value");
                return !string.IsNullOrEmpty(val) ? val : _host.GetElementText(he);
            }
        }
        catch (Exception ex) { Console.WriteLine($"Error GetValue: {ex.Message}"); }
        return string.Empty;
    }

    public static void SetValue(string cssSelector, string value)
    {
        if (_host == null)
            return;

        try
        {
            // ИСПРАВЛЕНО: Заменено на корректный MakeCssSelector
            var selector = _host.MakeCssSelector(cssSelector).GetEnumerator();
            if (selector.MoveNext())
            {
                var he = selector.Current;
                _host.SetElementAttribute(he, "value", value);
                _host.SetElementText(he, value);
            }
        }
        catch (Exception ex) { Console.WriteLine($"Error SetValue: {ex.Message}"); }
    }

    // Возвращаем метод запуска процессов в класс Program
    public static void RunProcess(string filename)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = filename, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Player Error] Сбой запуска процесса {filename}: {ex.Message}");
        }
    }

}