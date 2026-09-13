using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Owner-only "look at it and press E" interaction. Casts a ray straight out
// through the crosshair every frame and exposes whichever WorldItem is under
// it, so SessionHUD can draw the pickup prompt without repeating the cast.
public class PlayerInteractor : NetworkBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] float range = 3f;

    // Everything by default: walls have to block the ray, so hits are filtered
    // by looking for a WorldItem rather than by putting items on their own layer.
    [SerializeField] LayerMask mask = ~0;

    public static PlayerInteractor Local { get; private set; }

    // The item under the crosshair this frame, or null.
    public WorldItem Target { get; private set; }

    PlayerInput _playerInput;
    InputAction _interactAction;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        Local = this;

        if (cam == null)
            cam = GetComponentInChildren<Camera>(true);

        _playerInput = GetComponent<PlayerInput>();
        _playerInput.enabled = true;
        _interactAction = _playerInput.actions["Interact"];
        _interactAction.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this)
        {
            Target = null;
            Local = null;
        }
    }

    void OnDisable()
    {
        Target = null;

        if (_interactAction != null)
            _interactAction.Disable();
    }

    void Update()
    {
        UpdateTarget();

        if (Target == null || _interactAction == null || !_interactAction.WasPerformedThisFrame())
            return;

        // Leaves the item in the world when the inventory is full; clearing the
        // target either way keeps the prompt off a destroyed object.
        Target.PickUp(PlayerInventory.Local);
        Target = null;
    }

    void UpdateTarget()
    {
        // Nothing is aimed at while a menu holds the cursor, and the camcorder
        // replaces the crosshair with its viewfinder.
        if (cam == null || LocalUi.AnyOpen || Player.PlayerCam.CamcorderOn)
        {
            Target = null;
            return;
        }

        // Through the viewport centre rather than transform.forward: that is
        // exactly where the crosshair sits, whatever the camera's local offsets.
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Target = Physics.Raycast(ray, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore)
            ? hit.collider.GetComponentInParent<WorldItem>()
            : null;
    }
}
