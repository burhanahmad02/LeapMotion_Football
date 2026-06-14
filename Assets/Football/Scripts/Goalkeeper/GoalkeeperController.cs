using UnityEngine;

/// <summary>
/// Controls the goalkeeper, represented by two cube "hands", from a camera placed
/// behind the goal looking outward toward the shooting area.
///
/// The two hands are moved together as a single rig within a clamped rectangle that
/// matches the goal mouth. Movement is driven by either the mouse (projected onto the
/// goal plane) or the keyboard (WASD / arrows).
///
/// The hands use KINEMATIC Rigidbodies and are moved with Rigidbody.MovePosition so the
/// physics engine still resolves collisions against the (dynamic) ball and deflects it.
/// </summary>
public class GoalkeeperController : MonoBehaviour
{
    public enum ControlMode { Mouse, Keyboard }

    [Header("Control")]
    [SerializeField] private ControlMode controlMode = ControlMode.Mouse;
    [Tooltip("Camera used to project the mouse position onto the goal plane (usually the keeper camera).")]
    [SerializeField] private Camera controlCamera;

    [Header("Hands (Kinematic Rigidbodies)")]
    [SerializeField] private Rigidbody leftHand;
    [SerializeField] private Rigidbody rightHand;
    [Tooltip("Horizontal distance kept between the two hands.")]
    [SerializeField] private float handSeparation = 1.2f;

    [Header("Movement Bounds (world space, matches goal mouth)")]
    [Tooltip("Z position of the plane the hands move on. Should sit just in front of the goal line.")]
    [SerializeField] private float planeZ = 6.6f;
    [SerializeField] private float minX = -3.4f;
    [SerializeField] private float maxX = 3.4f;
    [SerializeField] private float minY = 0.4f;
    [SerializeField] private float maxY = 2.4f;

    [Header("Keyboard Tuning")]
    [SerializeField] private float keyboardSpeed = 6f;

    // Current centre of the two-hand rig on the goal plane (x = horizontal, y = vertical).
    private Vector2 center;

    // When false, input is ignored (e.g. during registration / game over).
    private bool controlEnabled = true;

    /// <summary>Enable or disable player input for the keeper.</summary>
    public void SetControlEnabled(bool enabled) => controlEnabled = enabled;

    private void Start()
    {
        if (controlCamera == null)
            controlCamera = Camera.main;

        // Start centred in the goal mouth.
        center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    }

    private void Update()
    {
        if (!controlEnabled) return;

        // Read input in Update for responsiveness; physics move happens in FixedUpdate.
        switch (controlMode)
        {
            case ControlMode.Mouse:
                UpdateMouseTarget();
                break;
            case ControlMode.Keyboard:
                UpdateKeyboardTarget();
                break;
        }

        // Keep the rig inside the goal mouth at all times.
        center.x = Mathf.Clamp(center.x, minX, maxX);
        center.y = Mathf.Clamp(center.y, minY, maxY);
    }

    private void FixedUpdate()
    {
        float halfGap = handSeparation * 0.5f;

        // MovePosition on kinematic bodies keeps collision/deflection working correctly.
        if (leftHand != null)
            leftHand.MovePosition(new Vector3(center.x - halfGap, center.y, planeZ));
        if (rightHand != null)
            rightHand.MovePosition(new Vector3(center.x + halfGap, center.y, planeZ));
    }

    /// <summary>Projects the mouse cursor onto the goal plane and uses that as the target.</summary>
    private void UpdateMouseTarget()
    {
        if (controlCamera == null) return;

        Ray ray = controlCamera.ScreenPointToRay(Input.mousePosition);

        // Plane facing -Z (toward the camera) positioned at planeZ.
        Plane goalPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));

        if (goalPlane.Raycast(ray, out float distance))
        {
            Vector3 hit = ray.GetPoint(distance);
            center.x = hit.x;
            center.y = hit.y;
        }
    }

    /// <summary>Moves the rig with WASD / arrow keys at a fixed speed.</summary>
    private void UpdateKeyboardTarget()
    {
        float h = Input.GetAxisRaw("Horizontal"); // A/D, Left/Right
        float v = Input.GetAxisRaw("Vertical");   // W/S, Up/Down

        center.x += h * keyboardSpeed * Time.deltaTime;
        center.y += v * keyboardSpeed * Time.deltaTime;
    }

    // Visualise the movement bounds in the Scene view.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 bl = new Vector3(minX, minY, planeZ);
        Vector3 br = new Vector3(maxX, minY, planeZ);
        Vector3 tl = new Vector3(minX, maxY, planeZ);
        Vector3 tr = new Vector3(maxX, maxY, planeZ);
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }
}
