#load "l_config.csx"

using System;
using System.IO;
using System.Linq;

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

// Передаем имя файла ui.html. Метод LoadHtmlLayout сам найдет его в папке скрипта!
Api.LoadHtml("ui.html");
updateUI();

// 1. КНОПКА ВЫБОРА ПРОЕКТА (Переписана на рабочий SelectFile)
Api.On("btn-select", () => {
    // Вызываем проверенный Api.SelectFile, который у автора плеера работает без ошибок маршалинга
    string targetFile = Api.SelectFile("ProjectVersion|ProjectVersion.txt", "Выберите файл ProjectVersion.txt внутри ProjectSettings");
    
    if (!string.IsNullOrEmpty(targetFile) && File.Exists(targetFile)) {
        // Если пользователь выбрал файл, отматываем назад: 
        // Из "C:/Folder/Project/ProjectSettings/ProjectVersion.txt" получаем "C:/Folder/Project"
        string projectSettingsDir = Path.GetDirectoryName(targetFile);
        string projectPath = Path.GetDirectoryName(projectSettingsDir);
        
        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) {
            Api.Alert("Не удалось определить корень проекта Unity", "Ошибка пути");
            return;
        }
        
        var vLine = File.ReadLines(targetFile).FirstOrDefault(l => l.StartsWith("m_EditorVersion:"));
        string ver = vLine != null ? vLine.Replace("m_EditorVersion:", "").Trim() : "Unknown";
        string name = Path.GetFileName(projectPath);
        
        // Записываем данные в кэш
        File.WriteAllText(CacheFile, name + "|" + projectPath + "|" + ver); 
        
        try {
            Api.SetValue("#project-path-input", projectPath);
        } catch {}
        
        updateUI();
    }
});

Api.On("btn-launch", () => {
    if (!File.Exists(CacheFile) || new FileInfo(CacheFile).Length == 0) {
        Api.Alert("Сначала выберите проект через кнопку 📂!", "Внимание");
        return;
    }

    var firstLine = File.ReadAllLines(CacheFile).FirstOrDefault();
    if (firstLine != null) {
        var parts = firstLine.Split('|');
        if (parts.Length == 3) {
            string pPath = parts[1];
            string pVer = parts[2];
            string exePath = Path.Combine(UnityEditorsRoot, pVer, "Editor", "Unity.exe");
            
            if (File.Exists(exePath)) {
                Api.RunProcess("\"" + exePath + "\" -projectPath \"" + pPath + "\"");
            } else {
                Api.Alert("Редактор Unity версии " + pVer + " не найден по пути:\n" + exePath, "Ошибка");
            }
        }
    }
});

Api.On("btn-clear", () => {
    if (File.Exists(CacheFile)) File.WriteAllText(CacheFile, string.Empty);
    try { Api.SetValue("#project-path-input", ""); } catch {}
    updateUI();
});
