using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Raycasts from the player camera, focuses interactables, and handles the interact input.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    private const string PlayerActionMapName = "Player";

    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InteractionPromptUI promptUI;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactableLayers = ~0;

    private InputAction interactAction;
    private IInteractable focusedInteractable;
    private RaycastHit raycastHit;
    private bool inputActionsReady;

    private void Awake()
    {
        ResolveCameraTransform();
        TryCacheInputActions();
    }

    private void OnEnable()
    {
        TryCacheInputActions();
        EnableInputActions();
    }

    private void OnDisable()
    {
        DisableInputActions();
        ClearFocus();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (inputActions != null)
        {
            return;
        }

        string[] assetGuids = AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset");
        if (assetGuids.Length == 0)
        {
            return;
        }

        inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            AssetDatabase.GUIDToAssetPath(assetGuids[0]));
    }
#endif

    private void Update()
    {
        if (!inputActionsReady)
        {
            return;
        }

        UpdateFocusedInteractable();

        if (interactAction.WasPressedThisFrame())
        {
            TryInteract();
        }
    }

    private void UpdateFocusedInteractable()
    {
        IInteractable hitInteractable = FindInteractableFromRaycast();

        if (hitInteractable == focusedInteractable)
        {
            if (focusedInteractable != null && !focusedInteractable.CanInteract(gameObject))
            {
                ClearFocus();
            }

            return;
        }

        ClearFocus();

        if (hitInteractable == null || !hitInteractable.CanInteract(gameObject))
        {
            return;
        }

        focusedInteractable = hitInteractable;
        promptUI?.Show(focusedInteractable.GetInteractionPrompt());
    }

    private IInteractable FindInteractableFromRaycast()
    {
        if (cameraTransform == null)
        {
            return null;
        }

        if (!Physics.Raycast(
                cameraTransform.position,
                cameraTransform.forward,
                out raycastHit,
                interactionDistance,
                interactableLayers,
                QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        return raycastHit.collider.GetComponentInParent<IInteractable>();
    }

    private void TryInteract()
    {
        if (focusedInteractable == null || !focusedInteractable.CanInteract(gameObject))
        {
            return;
        }

        focusedInteractable.Interact(gameObject);
        UpdateFocusedInteractable();
    }

    private void ClearFocus()
    {
        focusedInteractable = null;
        promptUI?.Hide();
    }

    private void ResolveCameraTransform()
    {
        if (cameraTransform != null)
        {
            return;
        }

        Camera playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            cameraTransform = playerCamera.transform;
        }
    }

    private void TryCacheInputActions()
    {
        if (inputActionsReady)
        {
            return;
        }

        if (inputActions == null)
        {
            inputActions = LoadDefaultInputActions();
        }

        if (inputActions == null)
        {
            Debug.LogError(
                $"{nameof(PlayerInteractor)} on {name} requires an Input Action Asset. " +
                "Assign Assets/InputSystem_Actions in the inspector.",
                this);
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap(PlayerActionMapName, false);
        if (playerMap == null)
        {
            Debug.LogError(
                $"{nameof(PlayerInteractor)} on {name} could not find the '{PlayerActionMapName}' action map.",
                this);
            return;
        }

        interactAction = playerMap.FindAction("Interact", false);
        if (interactAction == null)
        {
            Debug.LogError(
                $"{nameof(PlayerInteractor)} on {name} could not find the 'Interact' action.",
                this);
            return;
        }

        inputActionsReady = true;
    }

    private static InputActionAsset LoadDefaultInputActions()
    {
        InputActionAsset[] assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
        for (int i = 0; i < assets.Length; i++)
        {
            InputActionAsset asset = assets[i];
            if (asset != null && asset.name == "InputSystem_Actions")
            {
                return asset;
            }
        }

        return null;
    }

    private void EnableInputActions()
    {
        if (!inputActionsReady)
        {
            return;
        }

        interactAction.Enable();
    }

    private void DisableInputActions()
    {
        interactAction?.Disable();
    }

    private void OnDrawGizmosSelected()
    {
        if (cameraTransform == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            cameraTransform.position,
            cameraTransform.position + cameraTransform.forward * interactionDistance);
    }
}
