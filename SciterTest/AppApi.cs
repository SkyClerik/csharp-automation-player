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

}
