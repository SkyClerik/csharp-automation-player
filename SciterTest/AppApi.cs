using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EmptyFlow.SciterAPI;

public class AppApi
{
    // Сюда скрипт кладет путь к HTML-файлу или текст верстки
    public string Html { get; set; } = "";

    // Хранилище C# функций, привязанных к id элементов
    public Dictionary<string, Action> Actions { get; } = new();

    // Полный доступ к библиотеке для внешних скриптов
    public SciterAPIHost Host { get; set; } = null!;

    public void LoadHtml(string html)
    {
        Html = html;
    }

    // Регистрация обработчика события по ID элемента
    public void On(string eventName, Action callback)
    {
        Actions[eventName] = callback;
    }

    // Замена HTML-содержимого элемента по CSS-селектору
    public void SetHtml(string cssSelector, string html)
    {
        Program.SetHtml(cssSelector, html);
    }

    // Чтение значения поля по CSS-селектору (input/textarea)
    public string GetValue(string cssSelector)
    {
        return Program.GetValue(cssSelector);
    }

    // Установка значения поля по CSS-селектору
    public void SetValue(string cssSelector, string value)
    {
        Program.SetValue(cssSelector, value);
    }

    // КРОССПЛАТФОРМЕННЫЙ ВЫБОР ФАЙЛА (Без Win32 и без Eval)
    public string SelectFile(string filter = "All Files|*.*", string title = "Выберите файл")
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Win32Dialogs.GetOpenFileNameDialog(Program.MainWindowHandle, filter, title);
        }
        else
        {
            return LinuxDialogs.ZenitySelectFile(title, false);
        }
    }

    // КРОССПЛАТФОРМЕННЫЙ ВЫБОР ПАПКИ (Без Win32 и без Eval)
    public string SelectFolder(string title = "Выберите папку")
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Win32Dialogs.GetFolderDialog(Program.MainWindowHandle, title);
        }
        else
        {
            return LinuxDialogs.ZenitySelectFile(title, true);
        }
    }

    // Системное модальное окно уведомления (Alert)
    public void Alert(string message, string title = "Уведомление")
    {
        Program.Alert(message, title);
    }

    // Системное окно подтверждения (Confirm)
    public bool Confirm(string message, string title = "Подтверждение")
    {
        return Program.Confirm(message, title);
    }

    // Метод запуска процессов
    public void RunProcess(string filename)
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

    public void RunProcess(string filename, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Player Error] Сбой запуска процесса {filename} с аргументами {arguments}: {ex.Message}");
        }
    }

    public Dictionary<string, Action<string>> DragDropActions { get; } = new();

    public void EnableDragDrop(string cssSelector, Action<string> onDropCallback)
    {
        DragDropActions[cssSelector] = onDropCallback;

        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            // Вызываем наш монолитный отладчик с реальным хэндлом окна плеера!
            Win32DragDrop.EnableWindowDragDrop(Program.MainWindowHandle, this, cssSelector);
        }
    }

    /// <summary>
    /// Универсальный кроссплатформенный Drag-and-Drop папок для Windows и Linux.
    /// Безопасно распределяет потоки интерфейса операционных систем.
    /// </summary>
    public void MultiEnableDragDrop(string cssSelector, Action<string> onDropCallback)
    {
        Console.WriteLine($"[C# Multi-API] Регистрация кроссплатформенного Drag-and-Drop для: {cssSelector}");

        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            // НА WINDOWS: Мы просто подписываемся на событие hidden-init-btn через твой рабочий метод On.
            // Когда PlayerEventHandler поймает готовность документа (0x8015) в главном UI-потоке,
            // он сам вызовет этот коллбэк строго в контексте окна, и хук встанет как влитой!
            this.On("hidden-init-btn", () =>
            {
                Console.WriteLine("[C# Multi-API] Сигнал готовности получен. Безопасно запускаем Win32-хук в UI потоке...");

                // Вызываем твой оригинальный рабочий метод, который у тебя 100% выдавал результат
                this.EnableDragDrop(cssSelector, onDropCallback);
            });
        }
        else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
        {
            // НА LINUX: Среда выполнения Sciter 6 нативно пишет путь в значение элемента.
            // Здесь фоновый поток полностью безопасен, так как он не трогает Win32 структуры.
            System.Threading.Tasks.Task.Run(() =>
            {
                string lastPath = "";
                Console.WriteLine($"[C# Multi-API] Linux сессия: запущен поток мониторинга для {cssSelector}");

                while (true)
                {
                    string currentPath = this.GetValue(cssSelector);

                    if (!string.IsNullOrEmpty(currentPath) && currentPath != "Перетащите папку сюда" && currentPath != lastPath)
                    {
                        lastPath = currentPath;
                        string cleanPath = currentPath.Trim('"', '\'', '[', ']', ' ');

                        if (System.IO.Directory.Exists(cleanPath))
                        {
                            Console.WriteLine($"[C# Multi-API Linux] Обнаружен сброс папки! Путь: {cleanPath}");
                            this.SetValue("#folder-path", cleanPath);
                            onDropCallback?.Invoke(cleanPath);
                        }
                    }
                    System.Threading.Thread.Sleep(200);
                }
            });
        }
    }


    [DllImport("kernel32.dll")]
    private static extern nint GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private const int SW_HIDE = 0; // Скрыть окно
    private const int SW_SHOW = 5; // Показать окно

    /// <summary>
    /// Мгновенно и бесшумно скрывает черное окно консоли CMD от пользователя в фоновый режим.
    /// </summary>
    public void HideConsole()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            try
            {
                nint consoleWindow = GetConsoleWindow();
                if (consoleWindow != nint.Zero)
                {
                    ShowWindow(consoleWindow, SW_HIDE);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C# API Error] Сбой скрытия консоли: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Мгновенно возвращает скрытое окно консоли CMD обратно на экран для вывода дебаг-логов.
    /// </summary>
    public void ShowConsole()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            try
            {
                nint consoleWindow = GetConsoleWindow();
                if (consoleWindow != nint.Zero)
                {
                    ShowWindow(consoleWindow, SW_SHOW);
                    Console.WriteLine("[C# API] Окно консоли успешно возвращено на экран.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C# API Error] Сбой отображения консоли: {ex.Message}");
            }
        }
    }

}
