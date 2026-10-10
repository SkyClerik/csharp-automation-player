#load "l_config.csx"

using System;
using System.IO;
using System.Linq;

Api.HideConsole();

Action updateUI = () => {
    if (!File.Exists(CacheFile) || new FileInfo(CacheFile).Length == 0) {
        Api.SetHtml("#project-list", "<div style='padding:10px; color:#888;'>Кэш пуст. Выберите проект ниже.</div>");
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
    Api.SetHtml("#project-list", html);
};

// Загружаем интерфейс
Api.LoadHtml("ui.html");
updateUI();

// 1. КНОПКА ВЫБОРА ПРОЕКТА
Api.On("btn-select", () => {
    string targetFile = Api.SelectFile("ProjectVersion|ProjectVersion.txt", "Выберите файл ProjectVersion.txt внутри ProjectSettings");
    
    if (!string.IsNullOrEmpty(targetFile) && File.Exists(targetFile)) {
        string projectSettingsDir = Path.GetDirectoryName(targetFile);
        string projectPath = Path.GetDirectoryName(projectSettingsDir);
        
        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) {
            Api.Alert("Не удалось определить корень проекта Unity", "Ошибка пути");
            return;
        }
        
        var vLine = File.ReadLines(targetFile).FirstOrDefault(l => l.StartsWith("m_EditorVersion:"));
        string ver = vLine != null ? vLine.Replace("m_EditorVersion:", "").Trim() : "Unknown";
        string name = Path.GetFileName(projectPath);
        
        // Перезаписываем кэш
        File.WriteAllText(CacheFile, name + "|" + projectPath + "|" + ver); 
        
        try {
            Api.SetValue("#project-path-input", projectPath);
        } catch {}
        
        updateUI();
    }
});

// 2. КНОПКА ЗАПУСКА ПРОЕКТА (С автоматическим поиском альтернативной версии Unity)
Api.On("btn-launch", () => {
    Console.WriteLine("[Launcher Log] Нажата кнопка запуска...");

    if (!File.Exists(CacheFile) || new FileInfo(CacheFile).Length == 0) {
        Api.Alert("Сначала выберите проект через кнопку 📂!", "Внимание");
        return;
    }

    var firstLine = File.ReadAllLines(CacheFile).FirstOrDefault();
    if (firstLine != null) {
        var parts = firstLine.Split('|');
        if (parts.Length == 3) {
            string pPath = parts[1]; // Путь к проекту
            string pVer = parts[2];  // Родная версия проекта (например, 2020.3.1f1)
            
            string editorsRootDir = @"C:\Program Files\Unity\Hub\Editor";
            string editorPath = Path.Combine(editorsRootDir, pVer, "Editor", "Unity.exe");
            
            Console.WriteLine($"[Launcher Log] Родная версия проекта: {pVer}");

            // Если родная версия не установлена, включаем умный подбор
            if (!File.Exists(editorPath)) {
                Console.WriteLine($"[Launcher Warning] Родная версия {pVer} не найдена. Ищем альтернативу...");
                
                if (Directory.Exists(editorsRootDir)) {
                    // Ищем все папки установленных редакторов и сортируем по убыванию (свежие — первые)
                    var availableEditors = Directory.GetDirectories(editorsRootDir)
                        .Select(Path.GetFileName)
                        .Where(name => File.Exists(Path.Combine(editorsRootDir, name, "Editor", "Unity.exe")))
                        .OrderByDescending(name => name)
                        .ToList();

                    if (availableEditors.Any()) {
                        // Берем самую свежую из доступных версий
                        string fallbackVer = availableEditors.First();
                        editorPath = Path.Combine(editorsRootDir, fallbackVer, "Editor", "Unity.exe");
                        
                        Console.WriteLine($"[Launcher Log] Выбрана альтернативная версия: {fallbackVer}");
                        Api.Alert($"Родная версия Unity ({pVer}) не установлена.\nПроект будет запущен на доступной версии: {fallbackVer}", "Смена версии");
                    }
                }
            }

            // Финальный запуск (если нашли хоть какой-то рабочий Unity.exe)
            if (File.Exists(editorPath)) {
                //string cmdArguments = $@" /k "" ""{editorPath}"" -projectPath ""{pPath}"" """; // держать CMD открытой
                string cmdArguments = $@" /c "" ""{editorPath}"" -projectPath ""{pPath}"" """; // закрыть CMD после работы
                Console.WriteLine($"[Launcher Log] Вызов Api.RunProcess: {cmdArguments}");
                Api.RunProcess("cmd.exe", cmdArguments);
            } else {
                Console.WriteLine("[Launcher Error] На компьютере не найдено вообще ни одной версии Unity!");
                Api.Alert("На вашем компьютере не обнаружено установленных версий Unity в папке:\n" + editorsRootDir, "Ошибка запуска");
            }
        }
    }
});

// 3. КНОПКА ОЧИСТКИ КЭША
Api.On("btn-clear", () => {
    if (File.Exists(CacheFile)) File.WriteAllText(CacheFile, string.Empty);
    try { Api.SetValue("#project-path-input", ""); } catch {}
    updateUI();
});

// 4. ЗАГЛУШКА ДЛЯ СЕРВИСНОЙ КНОПКИ ПЛЕЕРА (Убирает ошибку .csx в консоли)
Api.On("hidden-init-btn", () => {});
