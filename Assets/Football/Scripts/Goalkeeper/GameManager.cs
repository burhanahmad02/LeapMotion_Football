using System.Collections;
using UnityEngine;

/// <summary>
/// Central game state machine:
///   Registration -> Countdown -> Shooting -> Result -> (next shot | GameOver)
///
/// Owns the score for the current turn, drives the AI striker, resolves each shot exactly
/// once, and hands the final result to the leaderboard. UI is fully delegated to UIManager.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Registration, Countdown, Shooting, Result, GameOver }
    public GameState State { get; private set; }

    private enum Outcome { Save, Goal, Wide }

    [Header("References")]
    [SerializeField] private AIStrikerController striker;
    [SerializeField] private GoalkeeperController goalkeeper;
    [SerializeField] private UIManager ui;
    [SerializeField] private LeaderboardManager leaderboard;
    [SerializeField] private Rigidbody ball;
    [SerializeField] private Transform ballSpawn;

    [Header("Turn Rules")]
    [Tooltip("Total shots the AI takes in one turn.")]
    [SerializeField] private int shotsPerTurn = 5;
    [Tooltip("Also end the turn early after this many goals conceded.")]
    [SerializeField] private bool endOnMisses = true;
    [SerializeField] private int maxMisses = 3;

    [Header("Timing")]
    [SerializeField] private float resultDisplayTime = 1.8f;
    [Tooltip("If a shot neither scores nor is saved (wide/over) within this time, move on.")]
    [SerializeField] private float shotTimeout = 4f;

    [Header("Difficulty / Leaderboard")]
    [SerializeField] private float savesForMaxDifficulty = 10f;
    [SerializeField] private int leaderboardTopN = 5;

    // Juice hooks. Decoupled listeners (e.g. GameFeel) subscribe to these; the
    // GameManager never needs to know who is listening.
    public event System.Action OnShoot;
    public event System.Action OnSave;
    public event System.Action OnGoal;

    private string playerName = "Player";
    private int saves;
    private int misses;
    private int shotsTaken;
    private bool shotResolved = true;
    private Coroutine timeoutRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() => EnterRegistration();

    // ---------------- Registration ----------------

    public void EnterRegistration()
    {
        State = GameState.Registration;
        ResetBall();
        if (goalkeeper != null) goalkeeper.SetControlEnabled(false);
        if (ui != null) ui.ShowRegistration();
    }

    /// <summary>Called by UIManager when the player submits a name.</summary>
    public void StartTurn(string name)
    {
        playerName = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
        saves = 0;
        misses = 0;
        shotsTaken = 0;

        if (goalkeeper != null) goalkeeper.SetControlEnabled(true);
        if (ui != null) { ui.ShowGameHUD(); ui.UpdateScore(saves, shotsTaken, shotsPerTurn); }

        BeginNextShot();
    }

    // ---------------- Shot cycle ----------------

    private void BeginNextShot()
    {
        if (IsTurnOver()) { EndTurn(); return; }
        StartCoroutine(CountdownThenShoot());
    }

    private bool IsTurnOver()
    {
        if (shotsTaken >= shotsPerTurn) return true;
        if (endOnMisses && misses >= maxMisses) return true;
        return false;
    }

    private IEnumerator CountdownThenShoot()
    {
        State = GameState.Countdown;
        ResetBall();

        bool done = false;
        if (ui != null) ui.PlayCountdown(() => done = true);
        else done = true;

        while (!done) yield return null;
        DoShot();
    }

    private void DoShot()
    {
        State = GameState.Shooting;
        shotResolved = false;
        shotsTaken++;

        if (striker != null)
        {
            float d = savesForMaxDifficulty > 0 ? Mathf.Clamp01(saves / savesForMaxDifficulty) : 0f;
            striker.SetDifficulty(d);
            striker.TriggerShot();
        }

        OnShoot?.Invoke();

        if (timeoutRoutine != null) StopCoroutine(timeoutRoutine);
        timeoutRoutine = StartCoroutine(ShotTimeout());
    }

    // Called by KeeperHand / GoalTrigger.
    public void OnBallSaved() => Resolve(Outcome.Save);
    public void OnBallEnteredGoal() => Resolve(Outcome.Goal);

    private void Resolve(Outcome outcome)
    {
        if (shotResolved || State != GameState.Shooting) return;
        shotResolved = true;
        State = GameState.Result;
        if (timeoutRoutine != null) StopCoroutine(timeoutRoutine);

        switch (outcome)
        {
            case Outcome.Save:
                saves++;
                if (striker != null) striker.ReactToSave();
                if (ui != null) ui.FlashResult("SAVE!", Color.green);
                OnSave?.Invoke();
                break;
            case Outcome.Goal:
                misses++;
                if (striker != null) striker.ReactToGoal();
                if (ui != null) ui.FlashResult("GOAL!", Color.red);
                OnGoal?.Invoke();
                break;
            case Outcome.Wide:
                if (ui != null) ui.FlashResult("MISSED!", Color.yellow);
                break;
        }

        if (ui != null) ui.UpdateScore(saves, shotsTaken, shotsPerTurn);

        CancelInvoke(nameof(BeginNextShot));
        Invoke(nameof(BeginNextShot), resultDisplayTime);
    }

    private IEnumerator ShotTimeout()
    {
        yield return new WaitForSeconds(shotTimeout);
        Resolve(Outcome.Wide); // wide/over: neither a save nor a goal
    }

    // ---------------- End of turn ----------------

    private void EndTurn()
    {
        State = GameState.GameOver;
        if (goalkeeper != null) goalkeeper.SetControlEnabled(false);
        if (leaderboard != null) leaderboard.AddScore(playerName, saves);
        if (ui != null && leaderboard != null)
            ui.ShowLeaderboard(leaderboard.GetTop(leaderboardTopN), playerName, saves);
    }

    /// <summary>Called by UIManager's Play Again button.</summary>
    public void PlayAgain() => EnterRegistration();

    private void ResetBall()
    {
        if (ball == null) return;
        ball.Sleep();
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        if (ballSpawn != null) ball.position = ballSpawn.position;
    }
}
