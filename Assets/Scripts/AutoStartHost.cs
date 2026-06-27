using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class AutoStartHostDebug : MonoBehaviour
{
    void Start()
    {
        Debug.Log("AutoStartHostDebug started.");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("FAILED: No NetworkManager.Singleton found in scene 5.");
            return;
        }

        Debug.Log("NetworkManager found.");

        if (NetworkManager.Singleton.NetworkConfig.PlayerPrefab == null)
        {
            Debug.LogError("FAILED: No Default Player Prefab assigned in NetworkManager.");
            return;
        }

        Debug.Log($"Default Player Prefab: {NetworkManager.Singleton.NetworkConfig.PlayerPrefab.name}");

        if (!NetworkManager.Singleton.NetworkConfig.PlayerPrefab.TryGetComponent<NetworkObject>(out _))
        {
            Debug.LogError("FAILED: Player prefab does not have a NetworkObject component.");
            return;
        }

        Debug.Log("Player prefab has NetworkObject.");

        if (!NetworkManager.Singleton.IsListening)
        {
            bool started = NetworkManager.Singleton.StartHost();
            Debug.Log($"StartHost result: {started}");
        }

        Debug.Log($"IsHost: {NetworkManager.Singleton.IsHost}");
        Debug.Log($"IsClient: {NetworkManager.Singleton.IsClient}");
        Debug.Log($"IsServer: {NetworkManager.Singleton.IsServer}");
        Debug.Log($"LocalClientId: {NetworkManager.Singleton.LocalClientId}");

        Invoke(nameof(CheckPlayerSpawned), 1f);
    }

    void CheckPlayerSpawned()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager disappeared after StartHost.");
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjectsList.Any(obj => obj.IsPlayerObject))
        {
            Debug.LogError("FAILED: No player object was spawned.");
            return;
        }

        foreach (NetworkObject obj in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (obj.IsPlayerObject)
            {
                Debug.Log($"Player spawned: {obj.name}");

                Camera cam = obj.GetComponentInChildren<Camera>(true);

                if (cam == null)
                {
                    Debug.LogError("FAILED: Player spawned, but no Camera found inside player prefab.");
                    return;
                }

                cam.gameObject.SetActive(true);
                cam.enabled = true;

                AudioListener listener = cam.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = true;

                Debug.Log($"Camera enabled on spawned player: {cam.name}");
            }
        }
    }
}