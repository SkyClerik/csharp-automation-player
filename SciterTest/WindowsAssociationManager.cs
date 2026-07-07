using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

[SupportedOSPlatform("windows")]
public class WindowsAssociationManager : IAssociationManager
{
    private const string ProgId = "CSharpScriptAutomation";
    private const string Extension = ".csx";

    public void ApplyAssociation()
    {
        try
        {
            string? currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
            {
                currentExe = Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".exe");
            }

            // Предварительно очищаем старые записи
            RemoveAssociation();

            // Регистрируем расширение и связываем его с ProgID приложения
            using (var key = Registry.ClassesRoot.CreateSubKey(Extension))
            {
                key.SetValue("", ProgId);
            }

            // Настраиваем команду запуска
            using (var key = Registry.ClassesRoot.CreateSubKey($@"{ProgId}\shell\Run"))
            {
                key.SetValue("", "Запустить в C# Плеере");
                using (var commandKey = key.CreateSubKey("command"))
                {
                    commandKey.SetValue("", $"\"{currentExe}\" \"%1\"");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[Успех] Ассоциация в Windows успешно обновлена под текущий путь!");
        }
        catch (UnauthorizedAccessException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n[Ошибка] Недостаточно прав! Запустите программу от имени АДМИНИСТРАТОРА.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nОшибка при работе с реестром Windows: {ex.Message}");
        }
    }

    public void RemoveAssociation()
    {
        try
        {
            if (Registry.ClassesRoot.OpenSubKey(Extension) != null)
                Registry.ClassesRoot.DeleteSubKeyTree(Extension, false);

            if (Registry.ClassesRoot.OpenSubKey(ProgId) != null)
                Registry.ClassesRoot.DeleteSubKeyTree(ProgId, false);
        }
        catch (UnauthorizedAccessException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n[Ошибка] Недостаточно прав для удаления записей реестра.");
        }
    }
}
