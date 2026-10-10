using System;

try
{
    // Тест 1: Проверка базового UI и кликов
Api.LoadHtml(@"
<html>
<head>
    <!-- Явно говорим Sciter рендерить кириллицу в UTF-8 -->
    <meta charset='utf-8'>
</head>
<body style='background:#1e1e1e; color:#fff; font-family:sans-serif; padding:30px; text-align:center;'>
    <h2>Скрипт Автоматизации #1</h2>
    <input id='my-input' value='Привет из первого скрипта!' style='width:80%; padding:5px;' />
    <br/><br/>
    <button id='btn-click' style='padding:10px 20px;'>Нажми меня</button>
</body>
</html>
");


    Api.On("btn-click", () => {
        string currentVal = Api.GetValue("#my-input");
        Api.Alert("Текст из инпута: " + currentVal, "Успех!");
    });
}
catch (Exception ex)
{
    // Если ошибка произойдет на этапе выполнения самого скрипта внутри Roslyn:
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n[Ошибка внутри CSX скрипта]:");
    Console.WriteLine(ex.ToString());
    Console.ResetColor();
    Console.WriteLine("\nНажмите Enter для закрытия окна...");
    Console.ReadLine();
}
