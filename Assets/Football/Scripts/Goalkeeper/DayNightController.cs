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
    [Tooltip("Brightness floor so night is moonlit, never pitch black (0..1).")]
    [Range(0f,1f)] public float minLight = 0.5f;
    [Tooltip("Sun never drops below this elevation, so it always lights from above.")]
    public float minSunElevation = 14f;
    public Color dayColor = new Color(1f, 0.96f, 0.86f);
    public Color duskColor = new Color(1f, 0.55f, 0.32f);
    public Color nightSunColor = new Color(0.55f, 0.64f, 0.9f);   // cool moonlight

    [Header("Floodlights / Sky")]
    public float maxFloodIntensity = 1.6f;
    public float skyDayExposure = 1.3f;
    public float skyNightExposure = 0.75f;

    [Header("Ambient floor (keeps everything visible)")]
    public Color dayAmbient = new Color(0.58f, 0.60f, 0.65f);
    public Color nightAmbient = new Color(0.34f, 0.37f, 0.46f);

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
        float noon = Mathf.Cos((timeOfDay - 0.5f) * 2f * Mathf.PI); // 1 at noon, -1 at midnight
        float day = Mathf.InverseLerp(-1f, 1f, noon);               // 0 night .. 1 noon
        float elevation = Mathf.Lerp(minSunElevation, 82f, day);    // sun always above the horizon
        if (sun) sun.transform.rotation = Quaternion.Euler(elevation, sunYaw, 0f);

        float lit = Mathf.Lerp(minLight, 1f, day);                  // brightness floor: never pitch black
        float night = 1f - day;
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(noon) * 2.2f);    // warm glow near dawn/dusk

        if (sun)
        {
            sun.intensity = lit * maxSunIntensity;
            Color baseCol = Color.Lerp(nightSunColor, dayColor, day);
            sun.color = Color.Lerp(baseCol, duskColor, dusk);
            sun.enabled = true;
        }
        if (skybox) skybox.SetFloat("_Exposure", Mathf.Lerp(skyNightExposure, skyDayExposure, day));
        if (floodlights != null)
            foreach (var f in floodlights)
                if (f) { f.intensity = night * maxFloodIntensity; f.enabled = f.intensity > 0.05f; }

        // Flat ambient floor so the stands/crowd never go pitch black at night
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, lit);
    }
}
