using UnityEngine;
using MoreMountains.Feedbacks;

/// <summary>
/// Thin bridge between gameplay events and Feel (MMF_Player) feedback sequences.
///
/// Gameplay code (GameManager / UIManager) stays completely Feel-agnostic and just raises
/// plain C# events; this relay is the ONLY place that knows about Feel. Drag your configured
/// MMF_Player objects into the inspector and build the feedback lists there.
///
/// This replaces the hand-rolled GameFeel component — disable/remove GameFeel so effects
/// don't play twice.
/// </summary>
public class FeelManager : MonoBehaviour
{
    [Header("Gameplay Feedbacks (build the feedback lists on these MMF_Players)")]
    [Tooltip("Fires on every countdown number (3,2,1,SHOOT!). Suggested: Scale (punch) on the countdown text + UI Sound with random pitch.")]
    public MMF_Player countdownTickFeedback;
    [Tooltip("Fires when the AI strikes the ball. Suggested: Camera Shake + Motion Blur flash + powerful kick Sound.")]
    public MMF_Player shootFeedback;
    [Tooltip("Fires when the keeper saves. Suggested: Camera Shake + Time (Hit/freeze) + Particles (Instantiate) + smack Sound.")]
    public MMF_Player saveFeedback;
    [Tooltip("Fires when a goal is conceded. Suggested: Camera Shake + Chromatic Aberration flash + heavy Sound.")]
    public MMF_Player goalFeedback;

    [Header("Optional helpers")]
    [Tooltip("If set, the Save feedback plays at the ball's position so impact particles spawn at the collision point.")]
    public Transform ball;
    [Tooltip("If set, the ball trail turns on at the kick and off when the shot resolves.")]
    public TrailRenderer ballTrail;

    private void OnEnable() => Bind();

    // GameManager.Instance / scene singletons may not be ready during OnEnable on the first
    // frame, so we (re)bind in Start too, guarding against double subscription.
    private void Start() => Bind();

    private void Bind()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnShoot -= HandleShoot; gm.OnSave -= HandleSave; gm.OnGoal -= HandleGoal;
            gm.OnShoot += HandleShoot; gm.OnSave += HandleSave; gm.OnGoal += HandleGoal;
        }

        var ui = FindObjectOfType<UIManager>();
        if (ui != null)
        {
            ui.OnCountdownTick -= HandleCountdownTick;
            ui.OnCountdownTick += HandleCountdownTick;
        }
    }

    private void OnDisable()
    {
        var gm = GameManager.Instance;
        if (gm != null) { gm.OnShoot -= HandleShoot; gm.OnSave -= HandleSave; gm.OnGoal -= HandleGoal; }

        var ui = FindObjectOfType<UIManager>();
        if (ui != null) ui.OnCountdownTick -= HandleCountdownTick;
    }

    // ---------------- Event handlers: this is where .PlayFeedbacks() is called ----------------

    private void HandleCountdownTick()
    {
        if (countdownTickFeedback != null) countdownTickFeedback.PlayFeedbacks();
    }

    private void HandleShoot()
    {
        if (ballTrail != null) { ballTrail.Clear(); ballTrail.emitting = true; }
        if (shootFeedback != null) shootFeedback.PlayFeedbacks();
    }

    private void HandleSave()
    {
        if (ballTrail != null) ballTrail.emitting = false;
        if (saveFeedback == null) return;

        // PlayFeedbacks(position) so any "Instantiate Particles" feedback spawns at the
        // collision point (we use the ball's current position as a close proxy).
        if (ball != null) saveFeedback.PlayFeedbacks(ball.position);
        else saveFeedback.PlayFeedbacks();
    }

    private void HandleGoal()
    {
        if (ballTrail != null) ballTrail.emitting = false;
        if (goalFeedback != null) goalFeedback.PlayFeedbacks();
    }
}
