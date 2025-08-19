using System.Collections.Generic;
using UnityEngine;

/// Attacca TrailRenderer ai 'tip' del rig Ultraleap (niente API Leap).
/// Cerca Transform con nomi tipo: index_end, middle_end, thumb_end, ... e anche *Distal*.
[ExecuteAlways]
[AddComponentMenu("Ultraleap Utils/Attach Trails To Leap Tips")]
public class AttachTrailsToLeapTips : MonoBehaviour
{
    [Header("Trail settings")]
    public float lifetime = 0.6f;
    public float startWidth = 0.025f;
    public float endWidth = 0.0f;
    public float minVertexDistance = 0.003f;
    public Material trailMaterial; // URP/Particles/Unlit consigliato (Transparent + Additive)

    [Header("Auto-scan")]
    public bool autoScan = true;
    [Tooltip("Riesegue la scansione se non trova tip o se cambiano i figli")]
    public float rescanEvery = 0.5f;

    [Header("Manual override (se vuoi specificare a mano)")]
    public Transform[] manualTips;

    // match: 'tip', '_end', 'end', 'distal', 'fingertip'
    static readonly string[] tipKeywords = { "tip", "_end", " end", "distal", "finger_tip", "fingertip" };
    // match: 'thumb','index','middle','ring','pinky','finger'
    static readonly string[] fingerKeywords = { "thumb", "index", "middle", "ring", "pinky", "finger" };

    readonly Dictionary<Transform, TrailRenderer> trails = new();
    float _lastScanTime = -999f;

    void OnEnable()
    {
        EnsureMaterial();
        RefreshTrails(force:true);
    }

    void OnTransformChildrenChanged()
    {
        if (autoScan) RefreshTrails(force:true);
    }

    void Update()
    {
        // segui i tip e emetti solo se attivi
        foreach (var kv in trails)
        {
            var tip = kv.Key; var tr = kv.Value;
            if (!tip || !tr) continue;
            tr.transform.position = tip.position;
            tr.emitting = tip.gameObject.activeInHierarchy && tip.gameObject.activeSelf;
        }

        if (autoScan && Application.isPlaying)
        {
            // se ancora non abbiamo trovato niente, riprova periodicamente (alcuni rig spaw nano tardi)
            if (trails.Count < 4 && Time.time - _lastScanTime > rescanEvery)
                RefreshTrails();
        }
    }

    void EnsureMaterial()
    {
        if (trailMaterial != null) return;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (!sh) sh = Shader.Find("Sprites/Default");
        trailMaterial = new Material(sh);
        if (trailMaterial.HasProperty("_BaseColor")) trailMaterial.SetColor("_BaseColor", Color.white);
        // suggerito: rendilo additive in Inspector (Blend: Additive)
    }

    public void RefreshTrails(bool force = false)
    {
        _lastScanTime = Time.time;

        // pulizia
        var toRemove = new List<Transform>();
        foreach (var kv in trails) if (!kv.Key || !kv.Value) toRemove.Add(kv.Key);
        foreach (var t in toRemove) trails.Remove(t);

        var tips = new List<Transform>();
        if (manualTips != null && manualTips.Length > 0)
        {
            foreach (var t in manualTips) if (t) tips.Add(t);
        }
        else
        {
            // scandisci tutto sotto questo root
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                var n = t.name.ToLowerInvariant();

                bool hasTipWord = false;
                for (int i=0;i<tipKeywords.Length;i++) if (n.Contains(tipKeywords[i])) { hasTipWord = true; break; }
                if (!hasTipWord) continue;

                bool hasFingerWord = false;
                for (int i=0;i<fingerKeywords.Length;i++) if (n.Contains(fingerKeywords[i])) { hasFingerWord = true; break; }
                if (!hasFingerWord) continue;

                // Ancestor contiene 'hand' (evita match casuali)
                bool underHand = false;
                var p = t.parent; int hops = 0;
                while (p && hops++ < 5) { if (p.name.ToLowerInvariant().Contains("hand")) { underHand = true; break; } p = p.parent; }
                if (!underHand) continue;

                tips.Add(t);
            }
        }

        // crea TrailRenderer per ogni tip trovato
        foreach (var tip in tips)
        {
            if (tip == null || trails.ContainsKey(tip)) continue;

            var go = new GameObject($"Trail_{tip.name}");
            // lo tengo come figlio del tip per semplificare (ma lo muovo in world in Update)
            go.transform.SetParent(tip, worldPositionStays:false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var tr = go.AddComponent<TrailRenderer>();
            tr.time = lifetime;
            tr.minVertexDistance = minVertexDistance;
            tr.startWidth = startWidth;
            tr.endWidth = endWidth;
            tr.alignment = LineAlignment.View;
            tr.numCornerVertices = 4;
            tr.numCapVertices   = 4;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.material = trailMaterial;
            tr.sortingOrder = 10;                            // sopra ad altri transparent
            if (tr.material) tr.material.renderQueue = 3050; // dopo altri transparent

            trails[tip] = tr;
        }

#if UNITY_EDITOR
        if (tips.Count == 0 && force)
            Debug.LogWarning("[AttachTrailsToLeapTips] Nessun tip trovato. Seleziona il componente e verifica i nomi (es. index_end, thumb_end, *Distal*). Puoi anche trascinare i Transform nei campi 'Manual Tips'.");
#endif
    }
}
