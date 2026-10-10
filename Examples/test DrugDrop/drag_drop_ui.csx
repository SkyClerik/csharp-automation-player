using System;
using System.IO;
using System.Linq;

// ТО ЧТО НАДО: Скрипт сам при старте гасит консоль!
Api.HideConsole();

Console.WriteLine("[C# Скрипт] Старт универсального лаунчера...");

// 1. Загружаем HTML разметку интерфейса
Api.LoadHtml("drag_drop.html");

// 2. Включаем Drag-and-Drop ОДНИМ методом
Api.MultiEnableDragDrop("#drop-zone", (path) => {
    Console.WriteLine($"[C# Скрипт] Папка успешно получена: {path}");
    Api.SetValue("#folder-path", path);

    if (Directory.Exists(path)) {
        var files = Directory.GetFiles(path).Select(Path.GetFileName).Take(30).ToList();
        string html = string.Join("", files.Select(f => "<div class='file-item'>📄 " + f + "</div>"));
        Api.SetHtml("#files-list", html);
    }
});

Console.WriteLine("[C# Скрипт] Конфигурация завершена. Ожидаем перетаскивание папки...");
