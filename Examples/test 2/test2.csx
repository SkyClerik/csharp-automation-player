// Тест 2: Проверка диалогов файлов и параллельного запуска
Api.LoadHtml(@"
<html>
<body style='background:#2d3748; color:#fff; font-family:sans-serif; padding:30px; text-align:center;'>
    <h2>Скрипт #2 (Параллельный)</h2>
    <p>Если это окно открылось рядом с первым — мультипроцессность работает!</p>
    <button id='btn-file' style='padding:10px; background:#4a5568; color:#fff;'>Выбрать файл</button>
</body>
</html>
");

Api.On("btn-file", () => {
    string filePath = Api.SelectFile("Текстовые файлы|*.txt|Все файлы|*.*", "Тест выбора файла");
    if (!string.IsNullOrEmpty(filePath)) {
        Api.Alert("Вы выбрали: " + filePath, "Файл получен");
    }
});
