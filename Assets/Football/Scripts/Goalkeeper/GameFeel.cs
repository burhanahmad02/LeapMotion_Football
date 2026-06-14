using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// All the "juice": camera shake, hit-stop + slow-motion, screen flashes, ball trail and
/// particle bursts driven by the GameManager's gameplay events. Fully decoupled — it only
/// subscribes to events, so removing this component leaves the game fully playable.
/// </summary>
public class GameFeel : MonoBehaviour
{
    [Header("Camera Shake")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float shootShake = 0.05f;
    [SerializeField] private float saveShake = 0.35f;
    [SerializeField] private float goalShake = 0.22f;
    [SerializeField] private float shakeDuration = 0.35f;

    [Header("Hit-Stop / Slow-Mo (unscaled time)")]
    [Tooltip("Full freeze on a save, in real seconds.")]
    [SerializeField] private float saveHitStop = 0.06f;
    [Tooltip("Time scale during the save slow-mo (0..1).")]
    [SerializeField] private float saveSlowMoScale = 0.25f;
    [SerializeField] private float saveSlowMoDuration = 0.45f;
    [SerializeField] private float goalHitStop = 0.04f;

    [Header("Screen Flash")]
    [SerializeField] private Image flashImage;
    [SerializeField] private Color saveFlash = new Color(0.25f, 1f, 0.4f, 0.45f);
    [SerializeField] private Color goalFlash = new Color(1f, 0.25f, 0.25f, 0.45f);
    [SerializeField] private float flashFade = 0.4f;

    [Header("Ball Trail")]
    [SerializeField] private TrailRenderer ballTrail;

    [Header("Particle Bursts (spawn at the ball)")]
    [SerializeField] private ParticleSystem saveBurst;
    [SerializeField] private ParticleSystem goalBurst;
    [SerializeField] private Transform ballTransform;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip saveClip;

    private Vector3 camBasePos;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (cameraTransform != null) camBasePos = cameraTransform.localPosition;
    }

    private void OnEnable() => Bind();

    // GameManager.Instance may not be set during OnEnable on the very first frame, so we
    // (re)bind in Start as well, guarding against double subscription.
    private void Start() => Bind();

    private void Bind()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        gm.OnShoot -= HandleShoot; gm.OnSave -= HandleSave; gm.OnGoal -= HandleGoal;
        gm.OnShoot += HandleShoot; gm.OnSave += HandleSave; gm.OnGoal += HandleGoal;
    }

    private void OnDisable()
    {
        var gm = GameManager.Instance;
        if (gm != null) { gm.OnShoot -= HandleShoot; gm.OnSave -= HandleSave; gm.OnGoal -= HandleGoal; }
        Time.timeScale = 1f; // never leave the game frozen
    }

    // ---------------- Event handlers ----------------

    private void HandleShoot()
    {
        if (sfxSource != null && shootClip != null) sfxSource.PlayOneShot(shootClip);
        Shake(shootShake);
        if (ballTrail != null) { ballTrail.Clear(); ballTrail.emitting = true; }
    }

    private void HandleSave()
    {
        if (sfxSource != null && saveClip != null) sfxSource.PlayOneShot(saveClip);
        Shake(saveShake);
        Flash(saveFlash);
        StopBallTrail();
        BurstAtBall(saveBurst);
        StartCoroutine(HitStopThenSlowMo(saveHitStop, saveSlowMoScale, saveSlowMoDuration));
    }

    private void HandleGoal()
    {
        Shake(goalShake);
        Flash(goalFlash);
        StopBallTrail();
        BurstAtBall(goalBurst);
        StartCoroutine(HitStopThenSlowMo(goalHitStop, 1f, 0f));
    }

    private void StopBallTrail()
    {
        if (ballTrail != null) ballTrail.emitting = false;
    }

    private void BurstAtBall(ParticleSystem ps)
    {
        if (ps == null) return;
        if (ballTransform != null) ps.transform.position = ballTransform.position;
        ps.Play();
    }

    // ---------------- Camera shake ----------------

    private void Shake(float magnitude)
    {
        if (cameraTransform == null || magnitude <= 0f) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(magnitude));
    }

    private IEnumerator ShakeRoutine(float magnitude)
    {
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float damper = 1f - (t / shakeDuration);
            Vector3 off = Random.insideUnitSphere * magnitude * damper;
            cameraTransform.localPosition = camBasePos + off;
            yield return null;
        }
        cameraTransform.localPosition = camBasePos;
        shakeRoutine = null;
    }

    // ---------------- Screen flash ----------------

    private void Flash(Color c)
    {
        if (flashImage == null) return;
        StopCoroutine(nameof(FlashRoutine));
        StartCoroutine(FlashRoutine(c));
    }

    private IEnumerator FlashRoutine(Color c)
    {
        float a0 = c.a;
        float t = 0f;
        while (t < flashFade)
        {
            t += Time.unscaledDeltaTime;
            Color cc = c; cc.a = Mathf.Lerp(a0, 0f, t / flashFade);
            flashImage.color = cc;
            yield return null;
        }
        flashImage.color = new Color(c.r, c.g, c.b, 0f);
    }

    // ---------------- Hit-stop + slow-mo ----------------

    private IEnumerator HitStopThenSlowMo(float freeze, float slowScale, float slowDur)
    {
        Time.timeScale = 0f;
        float t = 0f;
        while (t < freeze) { t += Time.unscaledDeltaTime; yield return null; }

        if (slowDur > 0f)
        {
            float s = 0f;
            while (s < slowDur)
            {
                s += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(slowScale, 1f, s / slowDur);
                yield return null;
            }
        }
        Time.timeScale = 1f;
    }
}
