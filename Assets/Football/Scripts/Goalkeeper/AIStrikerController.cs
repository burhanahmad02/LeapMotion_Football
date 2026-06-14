using UnityEngine;

/// <summary>
/// AI opponent that shoots the ball at random points in the goal and reacts emotionally
/// to the outcome. Also grounds its own model on the pitch at startup.
///
/// Animator contract:
///   - Bool   "shooting"   -> kick animation (Soccer Pass)
///   - Trigger "celebrate" -> happy animation when the AI scores
///   - Trigger "frustrated"-> sad animation when the keeper saves
/// </summary>
[DisallowMultipleComponent]
public class AIStrikerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [Tooltip("The visual model root to place on the ground (e.g. the 'Baller' mesh).")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Rigidbody ball;
    [SerializeField] private Transform ballSpawn;

    [Header("Grounding")]
    [SerializeField] private bool groundOnStart = true;
    [Tooltip("Layers considered 'pitch' when snapping to the ground.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundProbeHeight = 5f;

    [Header("Goal Target Area (world space)")]
    [SerializeField] private float goalLineZ = 7.12f;
    [SerializeField] private float goalMinX = -3.3f;
    [SerializeField] private float goalMaxX = 3.3f;
    [SerializeField] private float goalMinY = 0.3f;
    [SerializeField] private float goalMaxY = 2.3f;

    [Header("Shot Tuning (Easy = difficulty 0)")]
    [SerializeField] private float minFlightTime = 0.70f;
    [SerializeField] private float maxFlightTime = 0.95f;

    [Header("Shot Tuning (Hard = difficulty 1)")]
    [SerializeField] private float hardMinFlightTime = 0.40f;
    [SerializeField] private float hardMaxFlightTime = 0.60f;
    [Range(0f, 1f)]
    [SerializeField] private float maxCornerBias = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float difficulty = 0f;

    [Header("Animator Parameters")]
    [SerializeField] private string shootBool = "shooting";
    [SerializeField] private string celebrateTrigger = "celebrate";
    [SerializeField] private string frustratedTrigger = "frustrated";
    [Tooltip("Delay from animation start to the ball launch (the contact frame).")]
    [SerializeField] private float windupTime = 0.4f;
    [Tooltip("How long the shoot bool stays true after a shot.")]
    [SerializeField] private float animResetTime = 0.6f;

    private Vector3 currentTarget;
    private bool shotPending;

    private void Start()
    {
        if (groundOnStart) GroundModel();
    }

    /// <summary>Set shot difficulty (0 = easy, 1 = hardest).</summary>
    public void SetDifficulty(float value01) => difficulty = Mathf.Clamp01(value01);

    /// <summary>Snaps the model so its feet rest on the pitch directly below it.</summary>
    public void GroundModel()
    {
        if (modelRoot == null) return;

        var renderers = modelRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        Vector3 probe = new Vector3(modelRoot.position.x, b.center.y + groundProbeHeight, modelRoot.position.z);

        // RaycastAll so we can skip our own colliders (capsule / character controller).
        var hits = Physics.RaycastAll(probe, Vector3.down, groundProbeHeight * 4f, groundMask, QueryTriggerInteraction.Ignore);
        bool found = false;
        float groundY = 0f;
        float bestDist = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue; // ignore self
            float d = probe.y - h.point.y;
            if (d < bestDist) { bestDist = d; groundY = h.point.y; found = true; }
        }
        if (!found) groundY = 0f;

        float offset = groundY - b.min.y;
        modelRoot.position += new Vector3(0f, offset, 0f);
    }

    /// <summary>Begins a shot: chooses a target, plays the kick, launches at the contact frame.</summary>
    public void TriggerShot()
    {
        currentTarget = new Vector3(
            BiasedAxis(goalMinX, goalMaxX),
            BiasedAxis(goalMinY, goalMaxY),
            goalLineZ);

        shotPending = true;

        if (animator != null && !string.IsNullOrEmpty(shootBool))
        {
            animator.SetBool(shootBool, true);
            CancelInvoke(nameof(ResetShootBool));
            Invoke(nameof(ResetShootBool), animResetTime);
        }

        CancelInvoke(nameof(LaunchBall));
        Invoke(nameof(LaunchBall), windupTime);
    }

    /// <summary>Launches the ball toward the chosen target. Also callable from an Animation Event.</summary>
    public void LaunchBall()
    {
        if (!shotPending || ball == null) return;
        shotPending = false;

        Vector3 start = ballSpawn != null ? ballSpawn.position : ball.position;
        ball.position = start;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;

        float lo = Mathf.Lerp(minFlightTime, hardMinFlightTime, difficulty);
        float hi = Mathf.Lerp(maxFlightTime, hardMaxFlightTime, difficulty);
        float flightTime = Random.Range(lo, hi);

        ball.linearVelocity = CalculateLaunchVelocity(start, currentTarget, flightTime);
    }

    /// <summary>Play the happy animation (AI scored).</summary>
    public void ReactToGoal()
    {
        if (animator != null && !string.IsNullOrEmpty(celebrateTrigger))
            animator.SetTrigger(celebrateTrigger);
    }

    /// <summary>Play the sad animation (keeper saved).</summary>
    public void ReactToSave()
    {
        if (animator != null && !string.IsNullOrEmpty(frustratedTrigger))
            animator.SetTrigger(frustratedTrigger);
    }

    private void ResetShootBool()
    {
        if (animator != null && !string.IsNullOrEmpty(shootBool))
            animator.SetBool(shootBool, false);
    }

    private float BiasedAxis(float min, float max)
    {
        float t = Random.value;
        float bias = maxCornerBias * difficulty;
        float nearest = t < 0.5f ? 0f : 1f;
        t = Mathf.Lerp(t, nearest, bias);
        return Mathf.Lerp(min, max, t);
    }

    private static Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 target, float time)
    {
        Vector3 gravity = Physics.gravity;
        return ((target - start) - 0.5f * gravity * time * time) / time;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 bl = new Vector3(goalMinX, goalMinY, goalLineZ);
        Vector3 br = new Vector3(goalMaxX, goalMinY, goalLineZ);
        Vector3 tl = new Vector3(goalMinX, goalMaxY, goalLineZ);
        Vector3 tr = new Vector3(goalMaxX, goalMaxY, goalLineZ);
        Gizmos.DrawLine(bl, br); Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl); Gizmos.DrawLine(tl, bl);
    }
}
