// 1. ИСПРАВЛЕНИЕ: На Linux файловая система чувствительна к регистру (Case-Sensitive). 
// Убедитесь, что имя файла l_config.csx в точности совпадает на диске (например, не L_Config.csx).
#load "l_config.csx"

using System;
using System.IO;
using System.Runtime.InteropServices; // Добавляем для проверки ОС

// 2. Устанавливаем динамический текст из config.csx в тег h1 при запуске окна
Api.SetHtml("#main-title", MainTitleText);

// 3. Навешиваем логику на кнопки
Api.On("calc-button", () =>
{
     Api.SetHtml("#main-title", LoadingText);

    // 4. ИСПРАВЛЕНИЕ: calc.exe не существует на Linux. Делаем адаптивный запуск системного калькулятора.
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Api.RunProcess("calc.exe");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        // В Linux стандартные калькуляторы могут отличаться в зависимости от оболочки. 
        // gnome-calculator установлен по умолчанию в Ubuntu/Debian. xcalc — универсальный фолбэк.
        try 
        { 
            Api.RunProcess("gnome-calculator"); 
        }
        catch 
        { 
            try { Api.RunProcess("xcalc"); } catch { } 
        }
    }
});

Api.On("close-button", () =>
{
    Environment.Exit(0);
});
