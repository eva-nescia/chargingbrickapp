using UnityEngine;
using Vuforia;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CubeSpawner : MonoBehaviour
{
    public PlaneFinderBehaviour planeFinder;
    public GameObject cubePrefab;
    public Camera arCamera;
    public int totalCubes = 10;
    public float minDistance = 0.2f;
    public float startDelay = 1f;
    public float minScanSeconds = 5f;
    public int pointsPerTap = 10;
    public Vector3 spawnRotation = Vector3.zero;
    public bool showDebug = false;

    enum State { Menu, Scanning, Playing, Won }
    State state = State.Menu;

    List<Vector3> scannedSpots = new List<Vector3>();
    List<GameObject> spawned = new List<GameObject>();
    float scanStartTime, playStartTime, finalTime;
    int attempts, hits, score;

    GUIStyle titleStyle, textStyle, buttonStyle;
    Texture2D panelTex;
    Rect resetRect;

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        planeFinder.OnInteractiveHitTest.AddListener(OnHit);
    }

    // ---------- game flow ----------

    void StartScan()
    {
        ClearAll();
        state = State.Scanning;
        scanStartTime = Time.time;
        InvokeRepeating(nameof(ScanStep), startDelay, 0.3f);
    }

    void ResetGame()
    {
        ClearAll();
        state = State.Menu;
    }

    void ClearAll()
    {
        CancelInvoke();
        foreach (var obj in spawned)
            if (obj != null) Destroy(obj);
        spawned.Clear();
        scannedSpots.Clear();
        attempts = 0;
        hits = 0;
        score = 0;
    }

    void ScanStep()
    {
        if (state != State.Scanning) return;

        if (scannedSpots.Count >= totalCubes && Time.time - scanStartTime >= minScanSeconds)
        {
            SpawnAll();
            return;
        }

        attempts++;
        Vector2 p = new Vector2(
            Random.Range(0.1f, 0.9f) * Screen.width,
            Random.Range(0.1f, 0.9f) * Screen.height);
        planeFinder.PerformHitTest(p);
    }

    void OnHit(HitTestResult result)
    {
        if (state != State.Scanning) return;
        hits++;
        if (scannedSpots.Count >= totalCubes) return;

        foreach (var pos in scannedSpots)
            if (Vector3.Distance(pos, result.Position) < minDistance) return;

        scannedSpots.Add(result.Position);
    }

    void SpawnAll()
    {
        CancelInvoke();
        foreach (var pos in scannedSpots)
        {
            GameObject obj = Instantiate(cubePrefab, pos + Vector3.up * 0.05f, Quaternion.Euler(spawnRotation));

            if (obj.GetComponentInChildren<Collider>() == null)
            {
                foreach (var rend in obj.GetComponentsInChildren<MeshRenderer>())
                    rend.gameObject.AddComponent<BoxCollider>();
            }
            spawned.Add(obj);
        }
        playStartTime = Time.time;
        state = State.Playing;
    }

    // ---------- tapping ----------

    void Update()
    {
        if (state != State.Playing) return;

        Vector2 tapPos = Vector2.zero;
        bool pressed = false;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            tapPos = Touchscreen.current.primaryTouch.position.ReadValue();
            pressed = true;
        }
        else if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            tapPos = Pointer.current.position.ReadValue();
            pressed = true;
        }
        if (!pressed) return;

        // ignore taps on the Reset button
        Vector2 guiPos = new Vector2(tapPos.x, Screen.height - tapPos.y);
        if (resetRect.Contains(guiPos)) return;

        Camera cam = arCamera != null ? arCamera : Camera.main;
        if (cam == null) cam = FindObjectOfType<Camera>();
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(tapPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            GameObject target = GetSpawnedObject(hit.collider.transform);
            if (target != null)
            {
                spawned.Remove(target);
                Destroy(target);
                score += pointsPerTap;

                if (spawned.Count == 0)
                {
                    finalTime = Time.time - playStartTime;
                    state = State.Won;
                }
            }
        }
    }

    GameObject GetSpawnedObject(Transform t)
    {
        while (t != null)
        {
            if (spawned.Contains(t.gameObject))
                return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    // ---------- UI ----------

    void EnsureStyles()
    {
        if (panelTex == null)
        {
            panelTex = new Texture2D(1, 1);
            panelTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            panelTex.Apply();
        }
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
            titleStyle.wordWrap = true;

            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.normal.textColor = Color.white;
            textStyle.wordWrap = true;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontStyle = FontStyle.Bold;
        }
        float h = Screen.height;
        titleStyle.fontSize = Mathf.RoundToInt(h * 0.05f);
        textStyle.fontSize = Mathf.RoundToInt(h * 0.03f);
        buttonStyle.fontSize = Mathf.RoundToInt(h * 0.035f);
    }

    string FormatTime(float seconds)
    {
        int t = Mathf.FloorToInt(seconds);
        return string.Format("{0:00}:{1:00}", t / 60, t % 60);
    }

    void OnGUI()
    {
        EnsureStyles();
        float w = Screen.width, h = Screen.height;
        float m = h * 0.02f;
        resetRect = new Rect(w - w * 0.25f - m, m, w * 0.25f, h * 0.07f);

        switch (state)
        {
            case State.Menu:
                {
                    Rect panel = new Rect(w * 0.1f, h * 0.25f, w * 0.8f, h * 0.45f);
                    GUI.DrawTexture(panel, panelTex);
                    GUI.Label(new Rect(panel.x, panel.y + h * 0.02f, panel.width, h * 0.12f),
                        "Power Brick\nHide & Seek", titleStyle);
                    GUI.Label(new Rect(panel.x + w * 0.04f, panel.y + h * 0.15f, panel.width - w * 0.08f, h * 0.14f),
                        "Move your phone around to scan surfaces. Then find and tap all "
                        + totalCubes + " hidden power bricks!", textStyle);
                    if (GUI.Button(new Rect(panel.x + panel.width * 0.2f, panel.y + h * 0.31f, panel.width * 0.6f, h * 0.09f),
                        "START", buttonStyle))
                        StartScan();
                    break;
                }

            case State.Scanning:
                {
                    Rect bar = new Rect(m, m, w - w * 0.25f - m * 3f, h * 0.12f);
                    GUI.DrawTexture(bar, panelTex);
                    GUI.Label(bar, "Scanning... move slowly\nSpots found: " + scannedSpots.Count + "/" + totalCubes, textStyle);
                    if (GUI.Button(resetRect, "RESET", buttonStyle))
                        ResetGame();
                    break;
                }

            case State.Playing:
                {
                    Rect bar = new Rect(m, m, w - w * 0.25f - m * 3f, h * 0.12f);
                    GUI.DrawTexture(bar, panelTex);
                    GUI.Label(bar, "Found: " + (totalCubes - spawned.Count) + "/" + totalCubes
                        + "   Score: " + score + "\nTime: " + FormatTime(Time.time - playStartTime), textStyle);
                    if (GUI.Button(resetRect, "RESET", buttonStyle))
                        ResetGame();
                    break;
                }

            case State.Won:
                {
                    Rect panel = new Rect(w * 0.1f, h * 0.25f, w * 0.8f, h * 0.45f);
                    GUI.DrawTexture(panel, panelTex);
                    GUI.Label(new Rect(panel.x, panel.y + h * 0.02f, panel.width, h * 0.1f),
                        "All found!", titleStyle);
                    GUI.Label(new Rect(panel.x, panel.y + h * 0.14f, panel.width, h * 0.14f),
                        "Score: " + score + "\nTime: " + FormatTime(finalTime), textStyle);
                    if (GUI.Button(new Rect(panel.x + panel.width * 0.2f, panel.y + h * 0.31f, panel.width * 0.6f, h * 0.09f),
                        "PLAY AGAIN", buttonStyle))
                        StartScan();
                    break;
                }
        }

        if (showDebug)
            GUI.Label(new Rect(m, h - h * 0.08f, w, h * 0.06f),
                "tests: " + attempts + "  hits: " + hits, textStyle);
    }
}