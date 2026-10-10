using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32DragDrop
{
    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(nint hWnd, bool fAccept);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern uint DragQueryFile(nint hDrop, uint iFile, StringBuilder lpszFile, uint cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(nint hDrop);

    [DllImport("user32.dll")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilter(uint msg, uint flags);

    private const int GWL_WNDPROC = -4;
    private const uint WM_DROPFILES = 0x0233;
    private const uint WM_COPYDATA = 0x0049;
    private const uint MSGFLT_ADD = 1;

    private static nint _oldWndProc = nint.Zero;
    private static AppApi _api = null!;
    private static string _selector = "#drop-zone";
    private static WndProcDelegate? _wndProcDelegate;

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    public static void EnableWindowDragDrop(nint hWnd, AppApi api, string cssSelector)
    {
        _api = api;
        _selector = cssSelector;

        // ЗАЩИТА ОТ ПОВТОРНОЙ РЕГИСТРАЦИИ (Исключает StackOverflowException):
        // Если оконный хук уже был успешно внедрен ранее, мы просто выходим из метода,
        // предотвращая зацикливание указателей оконных процедур в памяти!
        if (_oldWndProc != nint.Zero)
        {
            return;
        }

        Console.WriteLine($"\n[C# КРИТИЧЕСКИЙ ЛОГ] Регистрация верхнеуровневого Win32 хука для HWND: {hWnd}");

        // 1. Пробиваем глобальные шлюзы безопасности UIPI
        ChangeWindowMessageFilter(WM_DROPFILES, MSGFLT_ADD);
        ChangeWindowMessageFilter(WM_COPYDATA, MSGFLT_ADD);

        // 2. Разрешаем окну принимать файлы из Проводника
        DragAcceptFiles(hWnd, true);

        // 3. Врезаемся в самую вершину оконной процедуры Sciter окна
        _wndProcDelegate = new WndProcDelegate(PlayerWndProc);
        _oldWndProc = SetWindowLongPtr(hWnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        Console.WriteLine("[C# КРИТИЧЕСКИЙ ЛОГ] Верхнеуровневый Win32 хук успешно внедрен в память окна!");
    }

    private static nint PlayerWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        // Перехватываем сообщение ДО того, как движок Sciter успеет его обработать и удалить
        if (msg == WM_DROPFILES)
        {
            Console.WriteLine("\n[Win32 Message] !!! МЫ ПОЙМАЛИ WM_DROPFILES (0x0233) !!!");

            nint hDrop = wParam;
            var pathBuilder = new StringBuilder(260);

            if (DragQueryFile(hDrop, 0, pathBuilder, 260) > 0)
            {
                string droppedPath = pathBuilder.ToString();
                DragFinish(hDrop);

                Console.WriteLine($"[Win32 Message] Из памяти извлечен путь: {droppedPath}");

                if (Directory.Exists(droppedPath))
                {
                    Console.WriteLine("[Win32 Message] Путь подтвержден как папка. Пишем во фронтенд...");

                    // Обновляем текстовые поля во фронтенде Sciter
                    Program.SetValue("#folder-path", droppedPath);
                    Program.SetValue(_selector, droppedPath);

                    // Вызываем первую попавшуюся зарегистрированную лямбду из .csx скрипта
                    var dndCallback = _api.DragDropActions.Values.FirstOrDefault();
                    if (dndCallback != null)
                    {
                        Console.WriteLine("[Win32 Message] Найдена C# лямбда в скрипте! Запускаем вывод файлов...");
                        dndCallback.Invoke(droppedPath);
                    }
                    else
                    {
                        Console.WriteLine("[Win32 Message] ВНИМАНИЕ: Скрипт .csx не зарегистрировал коллбэк в DragDropActions!");
                    }
                }
                else
                {
                    Console.WriteLine("[Win32 Message] Ошибка: сброшенный объект не является папкой.");
                }

                // Передаем сообщение дальше Sciter, чтобы завершить системный цикл событий
                return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
            }
        }

        // Передаем все остальные системные сообщения (мышь, фокус, размеры) родному обработчику Sciter
        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }
}
