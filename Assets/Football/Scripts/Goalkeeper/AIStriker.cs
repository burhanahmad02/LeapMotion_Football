using UnityEngine;
using System.Collections;

/// <summary>
/// AI opponent that repeatedly shoots the ball at random targets inside the goal frame.
///
/// Flow per shot:
///   1. GameManager (or the internal auto-loop) calls <see cref="TriggerShot"/>.
///   2. A random target point on the goal face is chosen.
///   3. The shooting animation is started (animator bool/trigger).
///   4. At the "hit" moment the ball is launched with a ballistic velocity that lands on
///      the chosen target. The hit moment is driven either by an Animation Event
///      (recommended) or by a simple wind-up timer.
/// </summary>
public class AIStriker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody ball;
    [Tooltip("Where the ball sits before each shot (also the launch origin).")]
    [SerializeField] private Transform ballSpawn;

    [Header("Goal Target Area (world space)")]
    [Tooltip("Z position of the goal line the shots aim for.")]
    [SerializeField] private float goalLineZ = 7.12f;
    [SerializeField] private float goalMinX = -3.3f;
    [SerializeField] private float goalMaxX = 3.3f;
    [SerializeField] private float goalMinY = 0.3f;
    [SerializeField] private float goalMaxY = 2.3f;

    [Header("Shot Tuning (Easy — difficulty 0)")]
    [Tooltip("Flight time to the target. Lower = faster, flatter, harder shots.")]
    [SerializeField] private float minFlightTime = 0.70f;
    [SerializeField] private float maxFlightTime = 0.95f;

    [Header("Shot Tuning (Hard — difficulty 1)")]
    [SerializeField] private float hardMinFlightTime = 0.40f;
    [SerializeField] private float hardMaxFlightTime = 0.60f;
    [Tooltip("How strongly shots are pushed toward the corners at max difficulty (0 = no bias, 1 = always corners).")]
    [Range(0f, 1f)]
    [SerializeField] private float maxCornerBias = 0.8f;

    // Current difficulty in 0..1, set by the GameManager as saves accumulate.
    [SerializeField, Range(0f, 1f)] private float difficulty = 0f;

    [Header("Animation")]
    [SerializeField] private string shootAnimatorParam = "shooting";
    [Tooltip("If true, the ball is launched by an Animation Event calling LaunchBall(). " +
             "If false, it is launched after 'windupTime' seconds.")]
    [SerializeField] private bool useAnimationEvent = false;
    [Tooltip("Delay from animation start to ball launch when NOT using an Animation Event.")]
    [SerializeField] private float windupTime = 0.35f;
    [Tooltip("How long the animator bool stays true after a shot.")]
    [SerializeField] private float animResetTime = 0.6f;

    // Target chosen for the current shot, resolved at launch time.
    private Vector3 currentTarget;
    private bool shotPending;

    /// <summary>Sets shot difficulty (0 = easy, 1 = hardest). Called by the GameManager.</summary>
    public void SetDifficulty(float value01)
    {
        difficulty = Mathf.Clamp01(value01);
    }

    /// <summary>Called by the GameManager to start a new shot.</summary>
    public void TriggerShot()
    {
        // Pick a random spot inside the goal frame, biased toward the corners at higher difficulty.
        currentTarget = new Vector3(
            BiasedAxis(goalMinX, goalMaxX),
            BiasedAxis(goalMinY, goalMaxY),
            goalLineZ);

        shotPending = true;

        // Kick off the animation. Works whether the controller uses a bool or a trigger.
        if (animator != null && !string.IsNullOrEmpty(shootAnimatorParam))
        {
            animator.SetBool(shootAnimatorParam, true);
            CancelInvoke(nameof(ResetAnimator));
            Invoke(nameof(ResetAnimator), animResetTime);
        }

        if (!useAnimationEvent)
        {
            // Launch on a timer that approximates the animation's contact frame.
            CancelInvoke(nameof(LaunchBall));
            Invoke(nameof(LaunchBall), windupTime);
        }
        // else: wait for the Animation Event to call LaunchBall().
    }

    /// <summary>
    /// Launches the ball toward the chosen target. Public so it can be called from an
    /// Animation Event placed on the "hit" frame of the kick animation.
    /// </summary>
    public void LaunchBall()
    {
        if (!shotPending || ball == null) return;
        shotPending = false;

        Vector3 start = ballSpawn != null ? ballSpawn.position : ball.position;

        // Make sure we start from a clean physics state.
        ball.position = start;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;

        // Faster (shorter) flight times as difficulty rises.
        float lo = Mathf.Lerp(minFlightTime, hardMinFlightTime, difficulty);
        float hi = Mathf.Lerp(maxFlightTime, hardMaxFlightTime, difficulty);
        float flightTime = Random.Range(lo, hi);
        Vector3 launchVelocity = CalculateLaunchVelocity(start, currentTarget, flightTime);

        // Unity 6 API. Setting velocity directly gives precise, repeatable aiming.
        ball.linearVelocity = launchVelocity;
    }

    /// <summary>
    /// Returns a random value in [min, max], pushed toward whichever end is nearer as
    /// difficulty/corner-bias rises, so harder shots favour the edges of the goal.
    /// </summary>
    private float BiasedAxis(float min, float max)
    {
        float t = Random.value; // 0..1 uniform
        float bias = maxCornerBias * difficulty;

        // Pull t toward the nearest extreme (0 or 1) by the bias amount.
        float nearest = t < 0.5f ? 0f : 1f;
        t = Mathf.Lerp(t, nearest, bias);

        return Mathf.Lerp(min, max, t);
    }

    /// <summary>
    /// Ballistic solution: the velocity needed to travel from <paramref name="start"/> to
    /// <paramref name="target"/> in exactly <paramref name="time"/> seconds under gravity.
    /// </summary>
    private static Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 target, float time)
    {
        Vector3 gravity = Physics.gravity;
        // displacement = v*t + 0.5*g*t^2  =>  v = (displacement - 0.5*g*t^2) / t
        Vector3 displacement = target - start;
        return (displacement - 0.5f * gravity * time * time) / time;
    }

    private void ResetAnimator()
    {
        if (animator != null && !string.IsNullOrEmpty(shootAnimatorParam))
            animator.SetBool(shootAnimatorParam, false);
    }

    // Optional: a self-contained loop for testing without a GameManager.
    // Enable in the inspector to see shots fire automatically.
    [Header("Standalone Test Loop")]
    [SerializeField] private bool autoLoop = false;
    [SerializeField] private float autoLoopInterval = 3f;

    private void OnEnable()
    {
        if (autoLoop) StartCoroutine(AutoLoop());
    }

    private IEnumerator AutoLoop()
    {
        var wait = new WaitForSeconds(autoLoopInterval);
        while (autoLoop)
        {
            TriggerShot();
            yield return wait;
        }
    }

    // Visualise the goal target area.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 bl = new Vector3(goalMinX, goalMinY, goalLineZ);
        Vector3 br = new Vector3(goalMaxX, goalMinY, goalLineZ);
        Vector3 tl = new Vector3(goalMinX, goalMaxY, goalLineZ);
        Vector3 tr = new Vector3(goalMaxX, goalMaxY, goalLineZ);
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }
}
