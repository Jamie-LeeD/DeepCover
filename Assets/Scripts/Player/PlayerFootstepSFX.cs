using UnityEngine;

/// <summary>
/// Plays footsteps based on player rigidbody movement. It never stores AudioClips directly;
/// it requests sounds from SFXManager's Dictionary by key.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PlayerFootstepSFX : MonoBehaviour
{
    [SerializeField] private float walkInterval = 0.48f;
    [SerializeField] private float runInterval = 0.34f;
    [SerializeField] private float movingSpeedThreshold = 0.25f;
    [SerializeField] private float runSpeedThreshold = 6.25f;
    [SerializeField] private bool requireGrounded = true;

    private Rigidbody playerRigidbody;
    private FirstPersonController firstPersonController;
    private float nextStepTime;

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        firstPersonController = GetComponent<FirstPersonController>();
    }

    private void Update()
    {
        SFXManager manager = SFXManager.Instance;
        if (manager == null || playerRigidbody == null || Time.timeScale <= 0f)
        {
            return;
        }

        if (requireGrounded && firstPersonController != null && !firstPersonController.IsGrounded)
        {
            return;
        }

        float horizontalSpeed = GetHorizontalSpeed();
        if (horizontalSpeed < movingSpeedThreshold)
        {
            return;
        }

        if (Time.time < nextStepTime)
        {
            return;
        }

        manager.PlayRandomSFX(
            manager.RandomFootstepKeys,
            manager.DefaultFootstepKey,
            manager.FootstepVolume);

        float interval = horizontalSpeed >= runSpeedThreshold ? runInterval : walkInterval;
        nextStepTime = Time.time + Mathf.Max(0.05f, interval);
    }

    private float GetHorizontalSpeed()
    {
        Vector3 velocity = playerRigidbody.linearVelocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }
}
