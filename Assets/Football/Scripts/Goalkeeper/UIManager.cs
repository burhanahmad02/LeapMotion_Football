using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Owns all UI: the registration screen, the in-game HUD (score + animated countdown +
/// result flash) and the end-of-turn leaderboard. It never touches game logic directly —
/// user actions are forwarded to the GameManager, and GameManager pushes data in.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject registrationPanel;
    [SerializeField] private GameObject gameHUD;
    [SerializeField] private GameObject leaderboardPanel;

    [Header("Registration")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button startButton;

    [Header("HUD")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text resultText;

    [Header("Leaderboard")]
    [Tooltip("Parent (with a VerticalLayoutGroup) that rows are spawned under.")]
    [SerializeField] private Transform leaderboardContent;
    [Tooltip("An inactive TMP_Text used as a row template.")]
    [SerializeField] private TMP_Text leaderboardRowTemplate;
    [SerializeField] private TMP_Text leaderboardSummary;
    [SerializeField] private Button playAgainButton;

    [Header("Countdown / Pop Tween")]
    [SerializeField] private string[] countdownSteps = { "3", "2", "1", "SHOOT!" };
    [SerializeField] private float stepDuration = 0.7f;
    [SerializeField] private float popScale = 1.6f;

    /// <summary>Raised once per countdown step (3, 2, 1, SHOOT!). FeelManager listens to punch the text + play a tick sound.</summary>
    public event System.Action OnCountdownTick;

    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgainClicked);
        if (leaderboardRowTemplate != null) leaderboardRowTemplate.gameObject.SetActive(false);
    }

    // ---------------- Panel switching ----------------

    public void ShowRegistration()
    {
        SetActive(registrationPanel, true);
        SetActive(gameHUD, false);
        SetActive(leaderboardPanel, false);
        if (resultText != null) resultText.text = string.Empty;
        if (countdownText != null) countdownText.text = string.Empty;
    }

    public void ShowGameHUD()
    {
        SetActive(registrationPanel, false);
        SetActive(gameHUD, true);
        SetActive(leaderboardPanel, false);
    }

    // ---------------- HUD ----------------

    public void UpdateScore(int saves, int shotsTaken, int shotsTotal)
    {
        if (scoreText != null)
            scoreText.text = $"Saves: {saves}    Shots: {shotsTaken}/{shotsTotal}";
    }

    public void PlayCountdown(Action onComplete)
    {
        StartCoroutine(CountdownRoutine(onComplete));
    }

    private IEnumerator CountdownRoutine(Action onComplete)
    {
        if (countdownText != null)
        {
            foreach (var step in countdownSteps)
            {
                countdownText.text = step;
                OnCountdownTick?.Invoke();
                yield return PopText(countdownText.rectTransform, stepDuration);
            }
            countdownText.text = string.Empty;
        }
        onComplete?.Invoke();
    }

    public void FlashResult(string message, Color color)
    {
        if (resultText == null) return;
        StopCoroutine(nameof(FlashRoutine));
        StartCoroutine(FlashRoutine(message, color));
    }

    private IEnumerator FlashRoutine(string message, Color color)
    {
        resultText.text = message;
        resultText.color = color;
        yield return PopText(resultText.rectTransform, 0.4f);
        yield return new WaitForSeconds(1.0f);
        resultText.text = string.Empty;
    }

    /// <summary>Scale "pop": starts enlarged and eases back to normal size.</summary>
    private IEnumerator PopText(RectTransform rt, float duration)
    {
        if (rt == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float scale = Mathf.Lerp(popScale, 1f, EaseOutCubic(k));
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    // ---------------- Leaderboard ----------------

    public void ShowLeaderboard(List<LeaderboardManager.ScoreEntry> top, string playerName, int playerSaves)
    {
        SetActive(registrationPanel, false);
        SetActive(gameHUD, false);
        SetActive(leaderboardPanel, true);

        // Clear previous rows.
        foreach (var go in spawnedRows) Destroy(go);
        spawnedRows.Clear();

        if (leaderboardRowTemplate != null && leaderboardContent != null && top != null)
        {
            int rank = 1;
            foreach (var entry in top)
            {
                var row = Instantiate(leaderboardRowTemplate, leaderboardContent);
                row.text = $"{rank}.  {entry.playerName}    {entry.saves}";
                row.gameObject.SetActive(true);
                spawnedRows.Add(row.gameObject);
                rank++;
            }
        }

        if (leaderboardSummary != null)
            leaderboardSummary.text = $"{playerName}  —  {playerSaves} saves this turn";
    }

    // ---------------- Button callbacks ----------------

    private void OnStartClicked()
    {
        string n = nameInput != null ? nameInput.text : "Player";
        if (GameManager.Instance != null) GameManager.Instance.StartTurn(n);
    }

    private void OnPlayAgainClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.PlayAgain();
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}
