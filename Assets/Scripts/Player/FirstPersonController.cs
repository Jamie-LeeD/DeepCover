using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Rigidbody-based first-person controller for Unity 6 and the Input System.
/// Attach to the player root, assign the Input Actions asset, and place the camera on a child transform.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FirstPersonController : MonoBehaviour
{
    private const string PlayerActionMapName = "Player";

    [Header("References")]
    [Tooltip("Project Input Actions asset (for example Assets/InputSystem_Actions).")]
    [SerializeField] private InputActionAsset inputActions;

    [Tooltip("Child transform that holds the player camera. Pitch is applied here.")]
    [SerializeField] private Transform cameraHolder;

    [Tooltip("Optional point at the feet used for ground checks. Defaults to this transform when empty.")]
    [SerializeField] private Transform groundCheck;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float movementSmoothTime = 0.1f;
    [SerializeField] private float airControlMultiplier = 0.5f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 7f;

    [Header("Grounding")]
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private float groundCheckOffset = 0.1f;
    [SerializeField] private LayerMask groundLayers = ~0;

    private Rigidbody playerRigidbody;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputActionMap playerActionMap;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpRequested;
    private bool sprintHeld;
    private bool isGrounded;
    private float pitch;
    private Vector3 movementVelocity;
    private bool inputActionsReady;

    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        GameplayPauseController.EnsureExists();
        playerRigidbody = GetComponent<Rigidbody>();
        ConfigureRigidbody();
        ResolveGroundCheck();
        TryCacheInputActions();
    }

    private void OnEnable()
    {
        TryCacheInputActions();
        EnableInputActions();
        LockCursor();
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

    private void OnDisable()
    {
        DisableInputActions();
        UnlockCursor();
    }

    private void Update()
    {
        if (!inputActionsReady)
        {
            return;
        }

        ReadInput();
        ApplyLook();
    }

    private void FixedUpdate()
    {
        if (!inputActionsReady)
        {
            return;
        }

        UpdateGrounding();
        ApplyMovement();
        ApplyJump();
    }

    /// <summary>
    /// Reads movement, look, sprint, and jump values from the Player action map.
    /// </summary>
    private void ReadInput()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        lookInput = lookAction.ReadValue<Vector2>();
        sprintHeld = sprintAction.IsPressed();

        if (jumpAction.WasPressedThisFrame())
        {
            jumpRequested = true;
        }
    }

    /// <summary>
    /// Rotates the body horizontally and the camera holder vertically from mouse input.
    /// </summary>
    private void ApplyLook()
    {
        if (cameraHolder == null)
        {
            return;
        }

        float yaw = lookInput.x * lookSensitivity;
        float pitchDelta = lookInput.y * lookSensitivity;

        transform.Rotate(Vector3.up, yaw, Space.Self);

        pitch = Mathf.Clamp(pitch - pitchDelta, minPitch, maxPitch);
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// <summary>
    /// Smoothly accelerates the rigidbody toward the desired horizontal velocity.
    /// </summary>
    private void ApplyMovement()
    {
        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }

        Vector3 worldDirection = transform.TransformDirection(inputDirection);
        float targetSpeed = sprintHeld ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = worldDirection * targetSpeed;

        float controlMultiplier = isGrounded ? 1f : airControlMultiplier;
        float smoothTime = Mathf.Max(movementSmoothTime, 0.0001f);

        Vector3 currentHorizontalVelocity = new Vector3(
            playerRigidbody.linearVelocity.x,
            0f,
            playerRigidbody.linearVelocity.z);

        Vector3 smoothedHorizontalVelocity = Vector3.SmoothDamp(
            currentHorizontalVelocity,
            targetVelocity,
            ref movementVelocity,
            smoothTime / controlMultiplier);

        playerRigidbody.linearVelocity = new Vector3(
            smoothedHorizontalVelocity.x,
            playerRigidbody.linearVelocity.y,
            smoothedHorizontalVelocity.z);
    }

    /// <summary>
    /// Applies an upward impulse when jump is requested and the player is grounded.
    /// </summary>
    private void ApplyJump()
    {
        if (!jumpRequested)
        {
            return;
        }

        jumpRequested = false;

        if (!isGrounded)
        {
            return;
        }

        Vector3 velocity = playerRigidbody.linearVelocity;
        velocity.y = 0f;
        playerRigidbody.linearVelocity = velocity;
        playerRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    /// <summary>
    /// Tests whether the player is standing on walkable geometry.
    /// </summary>
    private void UpdateGrounding()
    {
        Vector3 origin = groundCheck.position + Vector3.up * groundCheckOffset;
        isGrounded = Physics.CheckSphere(
            origin,
            groundCheckRadius,
            groundLayers,
            QueryTriggerInteraction.Ignore);
    }

    private void ConfigureRigidbody()
    {
        playerRigidbody.freezeRotation = true;
        playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        playerRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
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
                $"{nameof(FirstPersonController)} on {name} requires an Input Action Asset. " +
                "Assign Assets/InputSystem_Actions in the inspector.",
                this);
            return;
        }

        playerActionMap = inputActions.FindActionMap(PlayerActionMapName, false);
        if (playerActionMap == null)
        {
            Debug.LogError(
                $"{nameof(FirstPersonController)} on {name} could not find the '{PlayerActionMapName}' action map.",
                this);
            return;
        }

        moveAction = playerActionMap.FindAction("Move", false);
        lookAction = playerActionMap.FindAction("Look", false);
        jumpAction = playerActionMap.FindAction("Jump", false);
        sprintAction = playerActionMap.FindAction("Sprint", false);

        if (moveAction == null || lookAction == null || jumpAction == null || sprintAction == null)
        {
            Debug.LogError(
                $"{nameof(FirstPersonController)} on {name} is missing one or more Player actions " +
                "(Move, Look, Jump, Sprint).",
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

        playerActionMap?.Enable();
    }

    private void DisableInputActions()
    {
        playerActionMap?.Disable();
    }

    private void ResolveGroundCheck()
    {
        if (groundCheck == null)
        {
            groundCheck = transform;
        }
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDrawGizmosSelected()
    {
        Transform checkTransform = groundCheck != null ? groundCheck : transform;
        Vector3 origin = checkTransform.position + Vector3.up * groundCheckOffset;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
    }
}
