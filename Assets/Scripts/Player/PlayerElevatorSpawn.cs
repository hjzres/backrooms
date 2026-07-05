using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

// Places the owning player inside the elevator when the Session scene starts.
// The host's player object is spawned in the Create scene before the networked
// scene load, so without this only late-joining clients (whose objects spawn
// after the scene is loaded, at the prefab's default position) end up in the
// elevator. NetworkTransform is owner-authoritative, so each owner teleports
// its own player.
public class PlayerElevatorSpawn : NetworkBehaviour
{
    const string SessionSceneName = "Session";
    const string ElevatorObjectName = "Elevator";
    const float SpawnRingRadius = 0.5f;
    const float SpawnHeight = 1f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (SceneManager.GetActiveScene().name == SessionSceneName)
            MoveToElevator();
    }

    public override void OnNetworkDespawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SessionSceneName)
            MoveToElevator();
    }

    void MoveToElevator()
    {
        GameObject elevator = GameObject.Find(ElevatorObjectName);
        if (elevator == null)
        {
            Debug.LogWarning($"'{ElevatorObjectName}' not found in scene; player not repositioned.");
            return;
        }

        // Offset each player on a small ring so they don't spawn inside each other.
        float angle = OwnerClientId * Mathf.PI * 2f / 6f;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SpawnRingRadius;
        Vector3 position = elevator.transform.position + offset + Vector3.up * SpawnHeight;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        NetworkTransform netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null)
            netTransform.Teleport(position, transform.rotation, transform.localScale);
        else
            transform.position = position;
    }
}
