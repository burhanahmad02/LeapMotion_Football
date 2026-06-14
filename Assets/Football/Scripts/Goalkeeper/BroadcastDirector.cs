using UnityEngine;

/// <summary>
/// Cinematic "TV broadcast" camera that renders to a RenderTexture shown on the stadium
/// scoreboards. It slowly pans across the crowd, cuts to a close, tracking shot of the ball
/// when the AI shoots, and lingers on the crowd after a save/goal — like a real match feed.
/// Decoupled: only listens to GameManager events.
/// </summary>
public class BroadcastDirector : MonoBehaviour
{
    public Camera cam;
    public Transform ball;
    public CrowdManager crowd;

    [Header("Timing")]
    public float crowdShotDuration = 7f;
    public float ballShotDuration = 5f;
    public float wideShotDuration = 6f;
    [Tooltip("Higher = snappier camera moves.")]
    public float moveSmooth = 3f;

    enum Shot { Crowd, Ball, Wide }
    private Shot _shot;
    private Vector3 _from, _to, _look;
    private float _shotLen, _t;
    private Vector3 _camVel;

    private void Start()
    {
        var gm = GameManager.Instance;
        if (gm != null) { gm.OnShoot += CutToBall; gm.OnSave += CutToCrowd; gm.OnGoal += CutToCrowd; }
        NextShot(Shot.Crowd);
        if (cam != null) cam.transform.position = _from;
    }

    private void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm != null) { gm.OnShoot -= CutToBall; gm.OnSave -= CutToCrowd; gm.OnGoal -= CutToCrowd; }
    }

    private void CutToBall() { NextShot(Shot.Ball); }
    private void CutToCrowd() { NextShot(Shot.Crowd); }

    private Bounds CrowdBounds()
    {
        var b = new Bounds(transform.position, Vector3.one);
        bool first = true;
        if (crowd != null)
            foreach (Transform c in crowd.transform)
            {
                var r = c.GetComponentInChildren<Renderer>();
                if (r == null) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
        return b;
    }

    private void NextShot(Shot s)
    {
        _shot = s; _t = 0f;
        if (s == Shot.Crowd)
        {
            var b = CrowdBounds();
            Vector3 toPitch = -new Vector3(b.center.x, 0f, b.center.z);
            toPitch = toPitch.sqrMagnitude > 0.01f ? toPitch.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, toPitch);
            float dist = Mathf.Max(b.extents.x, b.extents.z) * 0.5f + 10f;
            Vector3 viewPos = b.center + toPitch * dist + Vector3.up * (b.extents.y * 0.25f);
            float pan = Mathf.Max(b.extents.x, b.extents.z) * 0.6f + 5f;
            _from = viewPos - side * pan; _to = viewPos + side * pan; _look = b.center; _shotLen = crowdShotDuration;
        }
        else if (s == Shot.Ball && ball != null)
        {
            Vector3 bp = ball.position;
            _from = bp + new Vector3(2.8f, 1.4f, -2.8f);
            _to = bp + new Vector3(-2.8f, 1.1f, -2.2f);
            _look = bp; _shotLen = ballShotDuration;
        }
        else
        {
            _from = new Vector3(-14f, 9f, -16f); _to = new Vector3(14f, 9f, -16f);
            _look = new Vector3(0f, 1f, 3f); _shotLen = wideShotDuration;
        }
    }

    private void Update()
    {
        if (cam == null) return;
        _t += Time.unscaledDeltaTime / Mathf.Max(0.1f, _shotLen);

        Vector3 targetPos = Vector3.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, _t));
        cam.transform.position = Vector3.SmoothDamp(cam.transform.position, targetPos, ref _camVel, 1f / Mathf.Max(0.01f, moveSmooth));

        Vector3 look = (_shot == Shot.Ball && ball != null) ? ball.position : _look;
        Vector3 dir = look - cam.transform.position;
        if (dir.sqrMagnitude > 0.001f)
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(dir), Time.unscaledDeltaTime * moveSmooth);

        if (_t >= 1f)
            NextShot(_shot == Shot.Crowd ? Shot.Wide : Shot.Crowd); // ball shots are event-driven
    }
}
