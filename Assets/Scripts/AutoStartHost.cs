using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

// Starts a local host when the Session scene is entered without an active
// network session (single player from the lobby START button, or playing the
// scene directly in the editor). In multiplayer flows the NetworkManager is
// already listening, so this does nothing.
public class AutoStartHost : MonoBehaviour
{
    void Start()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("AutoStartHost: no NetworkManager.Singleton found.");
            return;
        }

        if (nm.IsListening)
            return;

        // Reset the transport to direct localhost in case a previous
        // multiplayer session left relay data on it.
        if (nm.NetworkConfig.NetworkTransport is UnityTransport transport)
            transport.SetConnectionData("127.0.0.1", 7777);

        nm.StartHost();
    }
}
