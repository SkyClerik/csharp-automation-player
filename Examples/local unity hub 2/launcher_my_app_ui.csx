#load "l_config.csx"

using System;
using System.IO;
using System.Linq;

// Отключаем консоль на старте, как в вашем рабочем шаблоне
Api.HideConsole();

Console.WriteLine("[C# Скрипт] Старт универсального лаунчера...");

Action updateUI = () => {
    if (!File.Exists(CacheFile) || new FileInfo(CacheFile).Length == 0) {
        Api.SetHtml("#files-list", "<div style='padding:10px; color:#888;'>Кэш пуст. Перетащите папку проекта выше.</div>");
        return;
    }
    
    var lines = File.ReadAllLines(CacheFile);
    string html = "";
    
    foreach (var line in lines) {
        var parts = line.Split('|');
        if (parts.Length == 3) {
            html += "<div class='project-item'>" +
                    "<div class='name'>" + parts[0] + "</div>" +
                    "<div class='version'>Версия: " + parts[2] + "</div>" +
                    "<div class='path'>" + parts[1] + "</div>" +
                    "</div>";
        }
    }
    Api.SetHtml("#files-list", html);
};

// 1. Загружаем HTML разметку (Ваш родной файл ui.html)
Api.LoadHtml("ui.html");
updateUI();

// 2. Включаем Drag-and-Drop (КОПИЯ 1 в 1 ИЗ ВАШЕГО РАБОЧЕГО ПРИМЕРА)
Api.MultiEnableDragDrop("#drop-zone", (path) => {
    Console.WriteLine($"[C# Скрипт] Папка успешно получена: {path}");
    Api.SetValue("#folder-path", path);

    if (string.IsNullOrEmpty(path)) return;

    // Нормализуем путь Windows из Sciter URL
    string cleanFolder = path.Replace("file://", "").Replace("file:", "").Replace('/', Path.DirectorySeparatorChar).Trim();
    cleanFolder = cleanFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    string projectPath = cleanFolder;
    string targetFile = Path.Combine(cleanFolder, "ProjectSettings", "ProjectVersion.txt");

    if (Path.GetFileName(cleanFolder).Equals("ProjectSettings", StringComparison.OrdinalIgnoreCase)) {
        projectPath = Path.GetDirectoryName(cleanFolder);
        targetFile = Path.Combine(cleanFolder, "ProjectVersion.txt");
    }

    if (File.Exists(targetFile)) {
        var vLine = File.ReadLines(targetFile).FirstOrDefault(l => l.StartsWith("m_EditorVersion:"));
        string ver = vLine != null ? vLine.Replace("m_EditorVersion:", "").Trim() : "Unknown";
        string name = Path.GetFileName(projectPath);
        
        File.WriteAllText(CacheFile, name + "|" + projectPath + "|" + ver); 
        updateUI();
    } else {
        Api.Alert("В указанной директории не найден файл ProjectSettings/ProjectVersion.txt\nУбедитесь, что это корень проекта Unity.", "Не Unity проект");
    }
});

// 3. КНОПКА ВЫБОРА ПРОЕКТА ЧЕРЕЗ ПРОВОДНИК
Api.On("btn-select", () => {
    string targetFile = Api.SelectFile("ProjectVersion|ProjectVersion.txt", "Выберите файл ProjectVersion.txt внутри ProjectSettings");
    if (!string.IsNullOrEmpty(targetFile) && File.Exists(targetFile)) {
        string projectSettingsDir = Path.GetDirectoryName(targetFile);
        string projectPath = Path.GetDirectoryName(projectSettingsDir);
        
        string name = Path.GetFileName(projectPath);
        var vLine = File.ReadLines(targetFile).FirstOrDefault(l => l.StartsWith("m_EditorVersion:"));
        string ver = vLine != null ? vLine.Replace("m_EditorVersion:", "").Trim() : "Unknown";
        
        File.WriteAllText(CacheFile, name + "|" + projectPath + "|" + ver); 
        Api.SetValue("#folder-path", projectPath);
        updateUI();
    }
});

// 4. КНОПКА АВТОНОМНОГО ЗАПУСКА (Офлайн-режим через чистый cmd.exe /c)
Api.On("btn-launch", () => {
    if (!File.Exists(CacheFile) || new FileInfo(CacheFile).Length == 0) {
        Api.Alert("Сначала перетащите проект в зону сброса!", "Внимание");
        return;
    }

    var firstLine = File.ReadAllLines(CacheFile).FirstOrDefault();
    if (firstLine != null) {
        var parts = firstLine.Split('|');
        if (parts.Length == 3) {
            string pPath = parts[1]; 
            string pVer = parts[2];  
            
            string editorsRootDir = @"C:\Program Files\Unity\Hub\Editor";
            string editorPath = Path.Combine(editorsRootDir, pVer, "Editor", "Unity.exe");
            
            if (!File.Exists(editorPath) && Directory.Exists(editorsRootDir)) {
                var availableEditors = Directory.GetDirectories(editorsRootDir)
                    .Select(Path.GetFileName)
                    .Where(name => File.Exists(Path.Combine(editorsRootDir, name, "Editor", "Unity.exe")))
                    .OrderByDescending(name => name)
                    .ToList();

                if (availableEditors.Any()) {
                    string fallbackVer = availableEditors.First();
                    editorPath = Path.Combine(editorsRootDir, fallbackVer, "Editor", "Unity.exe");
                    Api.Alert($"Родная версия Unity ({pVer}) не установлена.\nПроект будет запущен на доступной версии: {fallbackVer}", "Смена версии");
                }
            }

            if (File.Exists(editorPath)) {
                string cmdArguments = $@" /c "" ""{editorPath}"" -projectPath ""{pPath}"" """;
                Api.RunProcess("cmd.exe", cmdArguments);
            } else {
                Api.Alert("На вашем компьютере не обнаружено установленных редакторов Unity.", "Ошибка запуска");
            }
        }
    }
});

// 5. КНОПКА ОЧИСТКИ КЭША
Api.On("btn-clear", () => {
    if (File.Exists(CacheFile)) File.WriteAllText(CacheFile, string.Empty);
    try { Api.SetValue("#folder-path", "Ожидание папки..."); } catch {}
    updateUI();
});
