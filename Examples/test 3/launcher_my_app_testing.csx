// linux - ./SciterPlayerCSharp "/home/user/SciterTest/scripts/launcher_my_app_testing.csx"
// win - .\SciterPlayerCSharp.exe "G:\Windows\Console\SciterTest\win-x64\win-x64\scripts\launcher_my_app_testing.csx"

// ПОДКЛЮЧАЕМ ВНЕШНИЙ СКРИПТ (Важно: путь указывается относительно этого файла)
#load "l_config.csx"

using System;

// 1. ВЕРСТКА ИНТЕРФЕЙСА
// Используем переменные CalcButtonName и CloseButtonName из подключенного config.csx
string htmlUI = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='background: #f0f0f0; text-align: center; font-family: sans-serif; padding-top: 50px;'>
    <h1 id='main-title'>{MainTitleText}</h1>
    
    <!-- Подставляем ID кнопок динамически из внешнего файла -->
    <button id='{CalcButtonName}'>Запустить Калькулятор</button>
    <button id='{CloseButtonName}'>Выход</button>
</body>
</html>";

// Загружаем разметку в окно
Api.LoadHtml(htmlUI);

// 2. НАВЕШИВАЕМ C# ЛОГИКУ ПО ДИНАМИЧЕСКИМ ID
Api.On(CalcButtonName, () => 
{
    Program.SetText("#main-title", LoadingText); 
    Program.RunProcess("calc.exe");                             
});

Api.On(CloseButtonName, () => 
{
    Environment.Exit(0);
});