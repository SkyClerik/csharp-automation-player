using System.Diagnostics;

public class LinuxAssociationManager : IAssociationManager
{
    public void ApplyAssociation()
    {
        try
        {
            string? currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[Ошибка] Не удалось определить путь к исполняемому файлу.");
                return;
            }

            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string desktopFileName = "csharp-automation-player.desktop";

            string mimeDir = Path.Combine(homeDir, ".local/share/mime/packages");
            Directory.CreateDirectory(mimeDir);
            string mimeFile = Path.Combine(mimeDir, "csharp-script.xml");

            string mimeContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<mime-info xmlns=""http://freedesktop.org"">
  <mime-type type=""text/x-csharp-script"">
    <comment>C# Script File</comment>
    <glob pattern=""*.csx""/>
  </mime-type>
</mime-info>";
            File.WriteAllText(mimeFile, mimeContent);

            string appsDir = Path.Combine(homeDir, ".local/share/applications");
            Directory.CreateDirectory(appsDir);
            string desktopFile = Path.Combine(appsDir, desktopFileName);

            string desktopContent = $@"[Desktop Entry]
Type=Application
Name=C# Automation Player
Exec=""{currentExe}"" %f
MimeType=text/x-csharp-script;
NoDisplay=false
Terminal=true";

            File.WriteAllText(desktopFile, desktopContent);

            string configDir = Path.Combine(homeDir, ".config");
            Directory.CreateDirectory(configDir);
            string mimeappsFile = Path.Combine(configDir, "mimeapps.list");

            string defaultSection = "[Default Applications]";
            string associationLine = $"text/x-csharp-script={desktopFileName};";

            if (File.Exists(mimeappsFile))
            {
                string content = File.ReadAllText(mimeappsFile);
                if (!content.Contains(associationLine))
                {
                    if (content.Contains(defaultSection))
                    {
                        content = content.Replace(defaultSection, $"{defaultSection}\n{associationLine}");
                    }
                    else
                    {
                        content += $"\n\n{defaultSection}\n{associationLine}";
                    }
                    File.WriteAllText(mimeappsFile, content);
                }
            }
            else
            {
                File.WriteAllText(mimeappsFile, $"{defaultSection}\n{associationLine}\n");
            }

            Console.WriteLine("\nОбновление системных баз данных Linux...");

            string localMimeDir = Path.Combine(homeDir, ".local/share/mime");
            RunToolSilent("update-mime-database", localMimeDir);
            RunToolSilent("update-desktop-database", appsDir);
            RunToolSilent("xdg-mime", $"default {desktopFileName} text/x-csharp-script");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[Успех] Ассоциация .csx в Linux успешно настроена!");
            Console.WriteLine("-> Теперь запускайте .csx файл двойным кликом ЛКМ или через ПКМ -> 'Открыть в приложении'.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nОшибка при установке ассоциаций Linux: {ex.Message}");
        }
    }

    public void RemoveAssociation()
    {
        try
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string mimeFile = Path.Combine(homeDir, ".local/share/mime/packages/csharp-script.xml");
            string desktopFile = Path.Combine(homeDir, ".local/share/applications/csharp-automation-player.desktop");

            if (File.Exists(mimeFile)) File.Delete(mimeFile);
            if (File.Exists(desktopFile)) File.Delete(desktopFile);

            string mimeappsFile = Path.Combine(homeDir, ".config/mimeapps.list");
            if (File.Exists(mimeappsFile))
            {
                string content = File.ReadAllText(mimeappsFile);
                content = content.Replace("text/x-csharp-script=csharp-automation-player.desktop;\n", "");
                content = content.Replace("text/x-csharp-script=csharp-automation-player.desktop;", "");
                File.WriteAllText(mimeappsFile, content);
            }

            RunToolSilent("update-mime-database", Path.Combine(homeDir, ".local/share/mime"));
            RunToolSilent("update-desktop-database", Path.Combine(homeDir, ".local/share/applications"));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[Успех] Все файлы ассоциаций Linux успешно удалены.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nОшибка при удалении ассоциаций Linux: {ex.Message}");
        }
    }

    private static void RunToolSilent(string command, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
        }
        catch { }
    }
}
