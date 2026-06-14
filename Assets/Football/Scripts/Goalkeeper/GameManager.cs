using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Drives the save/goal game loop:
///   - Resets the ball and tells the AI striker to shoot.
///   - Listens for "save" (ball touched the keeper hands) and "goal" (ball crossed the line).
///   - Tracks the save score and updates the UI.
///   - Resolves each shot exactly once, then queues the next one.
///
/// The small forwarder components (<see cref="KeeperHand"/>, <see cref="GoalTrigger"/>)
/// report events to this singleton.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private AIStriker striker;
    [SerializeField] private Rigidbody ball;
    [SerializeField] private Transform ballSpawn;

    [Header("UI (TextMeshPro)")]
    [SerializeField] private TMP_Text saveText;
    [SerializeField] private TMP_Text goalText;
    [SerializeField] private TMP_Text resultText; // optional "SAVE!" / "GOAL!" flash

    [Header("Timing")]
    [Tooltip("Delay after a shot resolves before the next shot starts.")]
    [SerializeField] private float delayBetweenShots = 2f;
    [Tooltip("If a shot neither scores nor is saved within this time, it counts as a miss and resets.")]
    [SerializeField] private float shotTimeout = 4f;
    [SerializeField] private float startupDelay = 1.5f;

    [Header("Difficulty Ramp")]
    [Tooltip("Number of saves needed to reach maximum difficulty.")]
    [SerializeField] private int savesForMaxDifficulty = 10;

    private int saveScore;
    private int goalScore;
    private bool shotResolved = true; // true = no active shot
    private Coroutine timeoutRoutine;

    private void Awake()
    {
        // Simple singleton so forwarders can reach the manager.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        UpdateUI();
        if (resultText != null) resultText.text = string.Empty;
        Invoke(nameof(BeginShot), startupDelay);
    }

    /// <summary>Resets the ball and asks the striker to take a shot.</summary>
    public void BeginShot()
    {
        ResetBall();

        shotResolved = false;
        if (striker != null)
        {
            // Ramp difficulty with the number of saves made so far.
            float difficulty = savesForMaxDifficulty > 0
                ? Mathf.Clamp01((float)saveScore / savesForMaxDifficulty)
                : 0f;
            striker.SetDifficulty(difficulty);
            striker.TriggerShot();
        }

        // Safety net: if the ball goes wide/over and nothing fires, recover.
        if (timeoutRoutine != null) StopCoroutine(timeoutRoutine);
        timeoutRoutine = StartCoroutine(ShotTimeout());
    }

    /// <summary>Called by KeeperHand when the ball contacts a keeper cube.</summary>
    public void OnBallSaved()
    {
        if (shotResolved) return;
        shotResolved = true;

        saveScore++;
        UpdateUI();
        Flash("SAVE!", Color.green);

        QueueNextShot();
    }

    /// <summary>Called by GoalTrigger when the ball crosses the goal line.</summary>
    public void OnBallEnteredGoal()
    {
        if (shotResolved) return;
        shotResolved = true;

        goalScore++;
        UpdateUI();
        Flash("GOAL!", Color.red);

        QueueNextShot();
    }

    /// <summary>Ball went wide/over without resolving — just reset, no score change.</summary>
    private void OnShotMissed()
    {
        if (shotResolved) return;
        shotResolved = true;
        QueueNextShot();
    }

    private void QueueNextShot()
    {
        if (timeoutRoutine != null) StopCoroutine(timeoutRoutine);
        CancelInvoke(nameof(BeginShot));
        Invoke(nameof(BeginShot), delayBetweenShots);
    }

    private IEnumerator ShotTimeout()
    {
        yield return new WaitForSeconds(shotTimeout);
        OnShotMissed();
    }

    private void ResetBall()
    {
        if (ball == null) return;
        ball.Sleep();
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        if (ballSpawn != null)
            ball.position = ballSpawn.position;
    }

    private void UpdateUI()
    {
        if (saveText != null) saveText.text = $"Saves: {saveScore}";
        if (goalText != null) goalText.text = $"Goals: {goalScore}";
    }

    private void Flash(string message, Color color)
    {
        if (resultText == null) return;
        resultText.text = message;
        resultText.color = color;
        CancelInvoke(nameof(ClearFlash));
        Invoke(nameof(ClearFlash), 1.2f);
    }

    private void ClearFlash()
    {
        if (resultText != null) resultText.text = string.Empty;
    }
}
