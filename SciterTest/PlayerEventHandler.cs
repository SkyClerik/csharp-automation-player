using System;
using EmptyFlow.SciterAPI;

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
        if ((int)cmd == 0)
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
