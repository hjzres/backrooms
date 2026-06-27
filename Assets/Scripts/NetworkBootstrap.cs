using UnityEngine;
using Unity.Netcode;

public class NetworkBootstrap : MonoBehaviour
{
    [SerializeField] GameObject networkManagerPrefab;

    void Awake()
    {
        if (NetworkManager.Singleton == null && networkManagerPrefab != null)
        {
            Instantiate(networkManagerPrefab);
        }
    }
}
