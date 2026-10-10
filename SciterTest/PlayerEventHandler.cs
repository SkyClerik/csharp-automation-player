using EmptyFlow.SciterAPI;
using System;
using System.IO;
using System.Threading.Tasks;

public class PlayerEventHandler : SciterEventHandler
{
    private readonly SciterAPIHost _host;
    private readonly AppApi _api;

    public PlayerEventHandler(nint window, SciterAPIHost host, AppApi api)
        : base(window, host, SciterEventHandlerMode.Window)
    {
        _host = host;
        _api = api;
    }

    public override void BehaviourEvent(BehaviourEvents cmd, nint heTarget, nint he, nint reason, SciterValue data, string name)
    {
        int cmdCode = (int)cmd;

        // Если код команды не равен клику (0), закрытию (193) и служебным ивентам, выводим его для отладки
        if (cmdCode != 0 && cmdCode != 6 && cmdCode != 193 && cmdCode != 33008 && cmdCode != 240)
        {
            Console.WriteLine($"[C# Core D&D Log] Перехвачено событие! Код команды (cmd): {cmdCode} (0x{cmdCode:X4}), Имя: {name}");
        }

        // =====================================================================
        // ЖЕЛЕЗОБЕТОННЫЙ ПЕРЕХВАТ ГОТОВНОСТИ ДОКУМЕНТА (Код 32789 / 0x8015)
        // =====================================================================
        if (cmdCode == 32789 || cmdCode == 0x8015)
        {
            Console.WriteLine("[C# Core] Документ HTML успешно загружен в память Sciter.");

            // Запускаем асинхронный таймаут прямо в C#, чтобы дать Windows 
            // завершить CreateWindow и выдать реальный HWND!
            Task.Run(async () => {
                await Task.Delay(100);

                Console.WriteLine("[C# Core] 100мс прошло. Инициируем нативный запуск хука...");

                // Напрямую вызываем зарегистрированный экшен hidden-init-btn из словаря Actions
                if (_api.Actions.ContainsKey("hidden-init-btn"))
                {
                    Console.WriteLine("[C# Core] Кнопка hidden-init-btn найдена в Actions. Безопасно запускаем хук Windows!");
                    _api.Actions["hidden-init-btn"]?.Invoke();
                }
                else
                {
                    Console.WriteLine("[C# Core Error] Ошибка: .csx скрипт не зарегистрировал Api.On(\"hidden-init-btn\")!");
                }
            });
        }

        // Код 0x10 (16) = EXCHANGE_ENTER (Файл занесен над областью окна)
        if (cmdCode == 16 || cmdCode == 0x10)
        {
            Console.WriteLine("[C# Core D&D Log] Файл пересёк границу элемента плеера!");
        }

        // Код 0x12 (18) = EXCHANGE_DROP (Пользователь отпустил мышь и сбросил папку!)
        if (cmdCode == 18 || cmdCode == 0x12)
        {
            Console.WriteLine("[C# Core D&D Log] !!! МЫ ПОЙМАЛИ ВСТРОЕННЫЙ DROP В BEHAVIOUR_EVENT !!!");
            try
            {
                string droppedPath = data.ToString();
                Console.WriteLine($"[C# Core D&D Log] Сырые данные из data: '{droppedPath}'");

                if (!string.IsNullOrEmpty(droppedPath))
                {
                    droppedPath = droppedPath.Trim('"', '\'', '[', ']', ' ');

                    if (Directory.Exists(droppedPath))
                    {
                        Console.WriteLine($"[C# Core D&D Log] Путь успешно верифицирован как папка: {droppedPath}");

                        Program.SetValue("#folder-path", droppedPath);
                        Program.SetValue("#drop-zone", droppedPath);

                        if (_api.DragDropActions.ContainsKey("#drop-zone"))
                        {
                            Console.WriteLine("[C# Core D&D Log] Вызываем лямбду OnFolderDropped из .csx скрипта...");
                            _api.DragDropActions["#drop-zone"]?.Invoke(droppedPath);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[C# Core D&D Log] Ошибка: Путь '{droppedPath}' не является папкой.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C# Core D&D Error] Сбой парсинга SciterValue: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------------
        // Стандартная логика вашего плеера (Клик и Закрытие)
        // ---------------------------------------------------------------------
        if (cmdCode == 193) // DOCUMENT_CLOSE_REQUEST
        {
            Environment.Exit(0);
        }

        if (cmdCode == 0) // BUTTON_CLICK
        {
            try
            {
                string elementId = _host.GetElementAttribute(heTarget, "id");
                if (!string.IsNullOrEmpty(elementId) && _api.Actions.TryGetValue(elementId, out var callback))
                {
                    callback.Invoke();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Player Error] Сбой обработки клика: {ex.Message}");
            }
        }

        base.BehaviourEvent(cmd, heTarget, he, reason, data, name);
    }
}
