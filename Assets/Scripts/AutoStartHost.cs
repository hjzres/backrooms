using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class AutoStartHostDebug : MonoBehaviour
{
    void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No NetworkManager.Singleton found.");
            return;
        }

        if (!NetworkManager.Singleton.IsListening)
        {
            bool started = NetworkManager.Singleton.StartHost();
            Debug.Log($"StartHost result: {started}");
        }

        Debug.Log($"IsHost: {NetworkManager.Singleton.IsHost}");
        Debug.Log($"IsClient: {NetworkManager.Singleton.IsClient}");
        Debug.Log($"IsServer: {NetworkManager.Singleton.IsServer}");
    }
}
