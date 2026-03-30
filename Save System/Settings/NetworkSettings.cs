using System;
using UnityEngine;

[Serializable]
public class NetworkSettings
{
    [TextArea(4, 25)] public string Description = $"<{nameof(IPAddressDemon)}> IP Address демонстратора" +
        $" | <{nameof(IPAddressNeural)}> IP Address нейронной сети обнаружения и распознавания";

    public string IPAddressDemon;
    public string IPAddressNeural;

    public NetworkSettings(string ipDemon, string ipNeural)
    {
        IPAddressDemon = ipDemon;
        IPAddressNeural = ipNeural;
    }
}