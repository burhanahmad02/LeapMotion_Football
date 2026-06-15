using UnityEngine;

/// <summary>
/// Drives a believable day/night cycle: arcs the sun, recolours/dims it, drives the
/// procedural skybox exposure + ambient, and fades the stadium floodlights in as it gets
/// dark. Works in the editor too (scrub Time Of Day) via OnValidate.
/// </summary>
[ExecuteAlways]
public class DayNightController : MonoBehaviour
{
    [Header("References")]
    public Light sun;
    public Light[] floodlights;
    public Material skybox;            // the procedural sky material

    [Header("Time")]
    [Range(0f, 1f)] public float timeOfDay = 0.78f;   // 0=midnight, .25=dawn, .5=noon, .75=dusk
    public bool autoCycle = true;
    public float dayLengthSeconds = 240f;
    public float sunYaw = 170f;

    [Header("Sun")]
    public float maxSunIntensity = 1.25f;
    public Color dayColor = new Color(1f, 0.96f, 0.86f);
    public Color duskColor = new Color(1f, 0.55f, 0.32f);

    [Header("Floodlights / Sky")]
    public float maxFloodIntensity = 1.5f;
    public float skyDayExposure = 1.3f;
    public float skyNightExposure = 0.55f;

    [Header("Ambient floor (keeps the crowd/stands visible)")]
    public Color dayAmbient = new Color(0.55f, 0.57f, 0.62f);
    public Color nightAmbient = new Color(0.24f, 0.26f, 0.34f);

    private float _giTimer;

    private void Update()
    {
        if (autoCycle && Application.isPlaying)
            timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / Mathf.Max(1f, dayLengthSeconds), 1f);

        Apply(false);

        // refresh ambient from the sky periodically (cheap throttle)
        _giTimer += Application.isPlaying ? Time.deltaTime : 0f;
        if (_giTimer > 0.33f) { _giTimer = 0f; DynamicGI.UpdateEnvironment(); }
    }

    private void OnValidate() => Apply(true);

    private void Apply(bool updateGI)
    {
        float pitch = timeOfDay * 360f - 90f;
        if (sun) sun.transform.rotation = Quaternion.Euler(pitch, sunYaw, 0f);

        float altitude = Mathf.Sin(pitch * Mathf.Deg2Rad);     // -1..1
        float day = Mathf.Clamp01(altitude * 1.1f + 0.15f);    // soft dawn/dusk
        float night = 1f - day;
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(altitude) * 3f); // peaks near the horizon

        if (sun)
        {
            sun.intensity = day * maxSunIntensity;
            sun.color = Color.Lerp(dayColor, duskColor, dusk);
            sun.enabled = sun.intensity > 0.02f;
        }
        if (skybox) skybox.SetFloat("_Exposure", Mathf.Lerp(skyNightExposure, skyDayExposure, day));
        if (floodlights != null)
            foreach (var f in floodlights)
                if (f) { f.intensity = night * maxFloodIntensity; f.enabled = f.intensity > 0.05f; }

        // Flat ambient floor so the stands/crowd never go pitch black at night
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, day);
    }
}
