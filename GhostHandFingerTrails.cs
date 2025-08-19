using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[AddComponentMenu("Ultraleap Utils/Ghost Hand Finger Trails")]
public class GhostHandFingerTrails : MonoBehaviour
{
    // ── Trail ───────────────────────────────────────────────────────────────────
    [Header("Trail")]
    [Tooltip("Durata della scia (secondi).")]
    public float lifetime = 0.45f;
    [Tooltip("Larghezza iniziale della scia (metri).")]
    public float startWidth = 0.006f;   // molto più sottile
    public float endWidth   = 0.0f;
    [Tooltip("Distanza minima tra vertici della scia (m).")]
    public float minVertexDistance = 0.0025f;
    [Tooltip("Fattore globale di scala per la larghezza (moltiplica start/end).")]
    public float globalWidthScale = 1.0f;
    [Tooltip("Mantieni lo spessore simile a schermo (approx in pixel).")]
    public bool  widthInPixels = false;
    [Tooltip("Spessore desiderato a schermo (pixel) se widthInPixels è ON.")]
    public float pixelWidth = 3f;

    [Header("Compensazione scala rig")]
    [Tooltip("Neutralizza la scala dei tip (utile se le mani sono scalate).")]
    public bool compensateParentScale = true;

    [Tooltip("Materiale URP/Particles/Unlit (Transparent + Additive consigliato). Se nullo lo creo.")]
    public Material trailMaterial;
    public Gradient trailGradient = DefaultGradient();
    public int sortingOrder = 10;

    // ── Pallino ─────────────────────────────────────────────────────────────────
    [Header("Dot (pallino sul tip)")]
    public bool  enableDot = true;
    public float dotSize   = 0.010f;    // ~10 mm
    public Color dotColor  = Color.white;
    public Material dotMaterial;        // Shader URP/Particles/Unlit (Transparent + Additive)

    // ── Scansione ──────────────────────────────────────────────────────────────
    [Header("Scan")]
    public Transform searchRoot;        // se nullo usa questo GO
    public float    rescanEvery = 0.5f; // riprova a trovare i tip
    public bool     logFoundTips = false;
    public Transform[] manualTips;      // override manuale

    // ── Ciclo di vita ─────────────────────────────────────────────────────────
    [Header("Lifecycle")]
    [Tooltip("Se OFF, Trail_*/Dot_* non vengono salvati e vengono rimossi allo Stop/Disable.")]
    public bool persistChildren = false;
    [Tooltip("Crea/scansiona anche in Edit Mode. Se OFF, opera solo in Play.")]
    public bool spawnInEditMode = false;

    // ── Runtime ────────────────────────────────────────────────────────────────
    readonly Dictionary<Transform, TrailRenderer> trails = new();
    readonly Dictionary<Transform, Transform>    dots   = new();
    float lastScanTime;
    Camera _cam;

    // thumb/index/middle/ring/pinky + (tip | _end | end | distal)
    static readonly Regex tipRegex = new Regex(@"(thumb|index|middle|ring|pinky).*(tip|_end|(^|[^a-z])end([^a-z]|$)|distal)",
                                               RegexOptions.IgnoreCase | RegexOptions.Compiled);

    void OnEnable()
    {
        if (!searchRoot) searchRoot = transform;
        EnsureMaterials();
        _cam = Camera.main;

        if (ShouldRunNow())
        {
            RefreshTrails(true);
            lastScanTime = Time.realtimeSinceStartup;
        }
    }

    void OnDisable() { StopEmitting(); if (!persistChildren) CleanupSpawned(); }
    void OnDestroy() { if (!persistChildren) CleanupSpawned(); }

    void Update()
    {
        if (!ShouldRunNow()) return;

        foreach (var kv in trails)
        {
            var tip = kv.Key; var tr = kv.Value;
            if (!tip || !tr) continue;

            // segui il tip
            var go = tr.transform;
            go.position = tip.position;

            // compensazione scala del parent (per mantenere lo spessore coerente)
            if (compensateParentScale)
            {
                float inv = 1f / Mathf.Max(0.0001f, tip.lossyScale.x);
                go.localScale = Vector3.one * inv;
            }

            // spessore a pixel costanti (approssimazione world->screen)
            if (widthInPixels && _cam)
            {
                float dist = Vector3.Distance(_cam.transform.position, tip.position);
                float worldPerPixel = 2f * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist / Mathf.Max(1, _cam.pixelHeight);
                float w = pixelWidth * worldPerPixel;
                tr.startWidth = w * globalWidthScale;
                tr.endWidth   = endWidth  * globalWidthScale;
            }

            bool active = tip.gameObject.activeInHierarchy && tip.gameObject.activeSelf;
            tr.emitting = active;

            if (enableDot && dots.TryGetValue(tip, out var dot) && dot)
            {
                dot.position = tip.position;
                dot.gameObject.SetActive(active);
                if (compensateParentScale)
                {
                    float inv = 1f / Mathf.Max(0.0001f, tip.lossyScale.x);
                    dot.localScale = Vector3.one * (dotSize * inv);
                }
            }
        }

        // riscan finché non agganciamo abbastanza tip
        if (trails.Count < 8 && Time.realtimeSinceStartup - lastScanTime > rescanEvery)
        {
            RefreshTrails(logFoundTips);
            lastScanTime = Time.realtimeSinceStartup;
        }
    }

    void OnValidate()
    {
        // Propaga subito i cambi in Inspector
        ApplySettingsToAll();
    }

    // ── API di servizio ────────────────────────────────────────────────────────
    [ContextMenu("Force Rescan Now")] public void ForceRescan() => RefreshTrails(true);
    [ContextMenu("Clear Spawned Now")] public void ClearNow() { CleanupSpawned(); }

    // ── Core ───────────────────────────────────────────────────────────────────
    void RefreshTrails(bool log = false)
    {
        // rimuovi orfani
        var toRemove = new List<Transform>();
        foreach (var kv in trails) if (!kv.Key || !kv.Value) toRemove.Add(kv.Key);
        foreach (var t in toRemove) { trails.Remove(t); dots.Remove(t); }

        var tips = new List<Transform>();
        if (manualTips != null && manualTips.Length > 0)
        {
            foreach (var t in manualTips) if (t) tips.Add(t);
        }
        else
        {
            var all = (searchRoot ? searchRoot : transform).GetComponentsInChildren<Transform>(true);
            foreach (var t in all) { if (t && t != searchRoot && tipRegex.IsMatch(t.name)) tips.Add(t); }
        }

        if (log) Debug.Log($"[GhostHandFingerTrails] Tips trovati: {tips.Count}");

        foreach (var tip in tips)
        {
            if (!trails.ContainsKey(tip))
            {
                var trailGO = new GameObject($"Trail_{tip.name}");
                trailGO.transform.SetParent(tip, worldPositionStays:false);
                trailGO.transform.localPosition = Vector3.zero;
                trailGO.transform.localRotation = Quaternion.identity;
                trailGO.transform.localScale    = Vector3.one;

                if (!persistChildren) trailGO.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

                var tr = trailGO.AddComponent<TrailRenderer>();
                tr.time = lifetime;
                tr.minVertexDistance = minVertexDistance;
                tr.startWidth = startWidth * globalWidthScale;
                tr.endWidth   = endWidth   * globalWidthScale;
                tr.numCornerVertices = 4;
                tr.numCapVertices    = 4;
                tr.alignment = LineAlignment.View;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.receiveShadows = false;
                tr.sortingOrder = sortingOrder;
                tr.material = trailMaterial;
                if (tr.material) tr.material.renderQueue = 3050;
                if (trailGradient != null) tr.colorGradient = trailGradient;
                trails[tip] = tr;
            }

            if (enableDot && !dots.ContainsKey(tip))
            {
                var dotGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dotGO.name = $"Dot_{tip.name}";
                dotGO.transform.SetParent(tip, worldPositionStays:false);
                dotGO.transform.localPosition = Vector3.zero;
                dotGO.transform.localRotation = Quaternion.identity;
                dotGO.transform.localScale    = Vector3.one * dotSize;

                if (!persistChildren) dotGO.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

                var col = dotGO.GetComponent<Collider>();
#if UNITY_EDITOR
                if (col) DestroyImmediate(col);
#else
                if (col) Destroy(col);
#endif
                var mr = dotGO.GetComponent<MeshRenderer>();
                if (mr)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.sharedMaterial = dotMaterial;
                    if (mr.sharedMaterial) mr.sharedMaterial.renderQueue = 3050;
                }

                dots[tip] = dotGO.transform;
            }
        }

        ApplySettingsToAll();

        if (tips.Count == 0)
            Debug.LogWarning("[GhostHandFingerTrails] Nessun tip trovato. Trascina i 10 Transform in 'Manual Tips'.");
    }

    void ApplySettingsToAll()
    {
        foreach (var tr in trails.Values)
        {
            if (!tr) continue;
            tr.time = lifetime;
            tr.minVertexDistance = minVertexDistance;
            tr.startWidth = (widthInPixels && _cam) ? tr.startWidth : startWidth * globalWidthScale;
            tr.endWidth   = endWidth * globalWidthScale;
            tr.sortingOrder = sortingOrder;
            if (trailMaterial) tr.material = trailMaterial;
            if (tr.material) tr.material.renderQueue = 3050;
            if (trailGradient != null) tr.colorGradient = trailGradient;
        }
        foreach (var d in dots.Values)
        {
            if (!d) continue;
            d.localScale = Vector3.one * dotSize;
            var mr = d.GetComponent<MeshRenderer>();
            if (mr && dotMaterial) mr.sharedMaterial = dotMaterial;
            if (mr && mr.sharedMaterial) mr.sharedMaterial.renderQueue = 3050;
        }
    }

    void StopEmitting()
    {
        foreach (var tr in trails.Values) if (tr) tr.emitting = false;
    }

    void EnsureMaterials()
    {
        if (!trailMaterial)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!sh) sh = Shader.Find("Sprites/Default");
            trailMaterial = new Material(sh);
            if (trailMaterial.HasProperty("_BaseColor"))
                trailMaterial.SetColor("_BaseColor", Color.white);
        }
        if (!dotMaterial)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!sh) sh = Shader.Find("Sprites/Default");
            dotMaterial = new Material(sh);
            if (dotMaterial.HasProperty("_BaseColor"))
                dotMaterial.SetColor("_BaseColor", dotColor);
        }
    }

    void CleanupSpawned()
    {
        foreach (var tr in trails.Values) if (tr) DestroyNow(tr.gameObject);
        trails.Clear();
        foreach (var d in dots.Values) if (d) DestroyNow(d.gameObject);
        dots.Clear();
    }

    static void DestroyNow(GameObject go)
    {
#if UNITY_EDITOR
        if (Application.isPlaying) Object.Destroy(go);
        else Object.DestroyImmediate(go);
#else
        Object.Destroy(go);
#endif
    }

    static Gradient DefaultGradient()
    {
        return new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        };
    }

    bool ShouldRunNow() => Application.isPlaying || spawnInEditMode;
}
