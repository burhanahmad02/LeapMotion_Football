using UnityEngine;
using TMPro;

/// <summary>
/// Drives the stadium scoreboards: shows the current player's name, live save count, and a
/// flashed result ("SUPER SAVE!" / "GOAL!") after each shot. Creates a 3D TextMeshPro on
/// each assigned scoreboard plane and (optionally) billboards it toward the keeper camera.
/// Decoupled: reads GameManager state + listens to OnSave/OnGoal.
/// </summary>
public class ScoreboardDisplay : MonoBehaviour
{
    [Tooltip("The scoreboard plane transforms to write onto.")]
    public Transform[] boards;
    [Tooltip("Camera the text turns to face (defaults to main).")]
    public Camera faceCamera;
    [Tooltip("Lift the text this far off the plane surface toward the camera.")]
    public float surfaceOffset = 0.2f;
    public float fontSize = 6f;
    public bool billboard = true;

    private TextMeshPro[] _texts;
    private float _flashTimer;
    private string _flashLine = "";

    private void Start()
    {
        if (faceCamera == null) faceCamera = Camera.main;
        if (boards == null) return;

        _texts = new TextMeshPro[boards.Length];
        var font = TMP_Settings.defaultFontAsset;
        for (int i = 0; i < boards.Length; i++)
        {
            if (boards[i] == null) continue;
            var go = new GameObject("ScoreboardText");
            go.transform.SetParent(boards[i], false);
            go.transform.localPosition = Vector3.zero;
            go.transform.position = boards[i].position + Vector3.up * surfaceOffset;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.enableWordWrapping = false;
            tmp.rectTransform.sizeDelta = new Vector2(16f, 9f);
            tmp.color = Color.white;
            _texts[i] = tmp;
        }

        var gm = GameManager.Instance;
        if (gm != null) { gm.OnSave += OnSave; gm.OnGoal += OnGoal; }
        Refresh();
    }

    private void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm != null) { gm.OnSave -= OnSave; gm.OnGoal -= OnGoal; }
    }

    private void OnSave() { _flashLine = "<color=#33FF88>SUPER SAVE!</color>"; _flashTimer = 2.5f; Refresh(); }
    private void OnGoal() { _flashLine = "<color=#FF5533>GOAL!</color>";       _flashTimer = 2.5f; Refresh(); }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        string name = gm != null && !string.IsNullOrEmpty(gm.PlayerName) ? gm.PlayerName : "PLAYER";
        int saves = gm != null ? gm.Saves : 0;
        string body = $"<size=120%><b>{name}</b></size>\nSAVES  {saves}";
        string text = string.IsNullOrEmpty(_flashLine) ? body : _flashLine + "\n" + body;

        if (_texts == null) return;
        foreach (var t in _texts) if (t != null) t.text = text;
    }

    private void Update()
    {
        Refresh(); // keep name / save count live (cheap: 2 texts)

        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f) { _flashLine = ""; }
        }

        if (billboard && faceCamera != null && _texts != null)
            foreach (var t in _texts)
                if (t != null) t.transform.rotation = Quaternion.LookRotation(t.transform.position - faceCamera.transform.position);
    }
}
