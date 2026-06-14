using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns animated humanoid spectators (the imported fan models) onto baked seat positions
/// and reacts to the game via an Animator:
///   - Save -> crowd plays the "cheer" trigger (Cheering)
///   - Idle -> Clapping (looping)
/// Seat positions are baked from the real Seats mesh (see seatPositions). Because skinned
/// humanoids are expensive, only the <see cref="maxFans"/> seats nearest the camera are
/// populated, and off-screen animators are culled.
/// </summary>
public class CrowdManager : MonoBehaviour
{
    [Header("Baked seat positions (world space)")]
    public List<Vector3> seatPositions = new List<Vector3>();

    [Header("Fan models + animation")]
    public GameObject[] fanPrefabs;                 // zlatan / nimer / indian fan
    public RuntimeAnimatorController crowdController;
    public string cheerTrigger = "cheer";

    [Header("Spawn")]
    [Tooltip("Max animated humanoids (perf cap). Nearest-to-camera seats are filled first.")]
    public int maxFans = 90;
    public float fanHeight = 1.8f;                   // normalize each model to this height
    public Camera sortCamera;                        // nearest seats to this camera are used
    public Transform faceTarget;                     // fans turn to face this (the pitch); defaults to origin
    public float yawOffset = 0f;                     // flip 180 if models face away
    public float yawJitter = 18f;

    private Animator[] _anims;

    private void Start()
    {
        Build();
        var gm = GameManager.Instance;
        if (gm != null) { gm.OnSave += Cheer; gm.OnGoal += Frown; }
    }

    private void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm != null) { gm.OnSave -= Cheer; gm.OnGoal -= Frown; }
    }

    public void Cheer()
    {
        if (_anims == null) return;
        foreach (var a in _anims)
            if (a != null) { a.ResetTrigger(cheerTrigger); a.SetTrigger(cheerTrigger); }
    }

    public void Frown() { /* no sad crowd clip provided; fans settle back to the clapping idle */ }

    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        if (seatPositions == null || seatPositions.Count == 0 || fanPrefabs == null || fanPrefabs.Length == 0)
        { _anims = null; return; }

        var cam = sortCamera != null ? sortCamera : Camera.main;
        var ordered = new List<Vector3>(seatPositions);
        if (cam != null)
        {
            Vector3 cp = cam.transform.position;
            Vector3 fwd = cam.transform.forward;
            // prefer seats IN FRONT of the camera (visible), nearest first; push behind-camera seats to the back
            float Key(Vector3 p)
            {
                Vector3 d = p - cp;
                float front = Vector3.Dot(d.normalized, fwd);
                return d.magnitude + (front < 0.15f ? 100000f : 0f);
            }
            ordered.Sort((a, b) => Key(a).CompareTo(Key(b)));
        }
        int count = Mathf.Min(maxFans, ordered.Count);
        Vector3 target = faceTarget != null ? faceTarget.position : new Vector3(0f, 0f, 0f);

        _anims = new Animator[count];
        for (int i = 0; i < count; i++)
        {
            var prefab = fanPrefabs[Random.Range(0, fanPrefabs.Length)];
            if (prefab == null) continue;
            var fan = Instantiate(prefab, transform);

            // Imported model FBXs often contain embedded cameras / lights / audio listeners.
            // Strip them, or dozens of crowd cameras hijack the screen.
            foreach (var extraCam in fan.GetComponentsInChildren<Camera>(true)) DestroyImmediate(extraCam);
            foreach (var al in fan.GetComponentsInChildren<AudioListener>(true)) DestroyImmediate(al);
            foreach (var lt in fan.GetComponentsInChildren<Light>(true)) DestroyImmediate(lt);

            fan.transform.position = ordered[i];

            Vector3 look = target - ordered[i]; look.y = 0f;
            float yaw = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
            fan.transform.rotation = Quaternion.Euler(0f, yaw + yawOffset + Random.Range(-yawJitter, yawJitter), 0f);

            NormalizeHeightAndSeat(fan, ordered[i]);

            var anim = fan.GetComponentInChildren<Animator>();
            if (anim == null) anim = fan.AddComponent<Animator>();
            if (crowdController != null) anim.runtimeAnimatorController = crowdController;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullCompletely;
            _anims[i] = anim;
        }
    }

    private void NormalizeHeightAndSeat(GameObject go, Vector3 seatPos)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        float h = b.size.y;
        if (h > 0.001f) go.transform.localScale *= (fanHeight / h);

        // re-measure and drop feet onto the seat
        rends = go.GetComponentsInChildren<Renderer>();
        b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        float footOffset = seatPos.y - b.min.y;
        go.transform.position += new Vector3(0f, footOffset, 0f);
    }
}
