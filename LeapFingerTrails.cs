using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap; // solo Leap

/// Scie dei polpastrelli con dissolvenza automatica (URP ok, compatibile con vari SDK Ultraleap)
public class LeapFingerTrails : MonoBehaviour
{
    [Header("Leap")]
    public LeapProvider provider;           // assegna LeapServiceProvider o LeapXRServiceProvider
    [Tooltip("mm → m (Leap lavora in millimetri)")]
    public float leapToUnity = 0.001f;
    public bool leftHand = true, rightHand = true;

    [Header("Trail")]
    public float lifetime = 0.45f;
    public float minVertexDistance = 0.005f;
    public float startWidth = 0.02f, endWidth = 0.0f;
    public Material trailMaterial;          // URP/Particles/Unlit consigliato
    public Gradient colorGradient;

    [Header("Reattività")]
    public bool widthBySpeed = true;
    public float widthSpeedMin = 0.0f;   // m/s
    public float widthSpeedMax = 2.0f;   // m/s
    public float widthScale = 1.4f;

    class Trail { public GameObject go; public TrailRenderer tr; public float lastSeen; }
    readonly Dictionary<int, Trail> trails = new();
    readonly Dictionary<int, Vector3> lastPos = new();
    readonly Dictionary<int, float>   lastTime = new();

    void Reset()
    {
        // Gradiente base: bianco → trasparente
        colorGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        };
    }

    void Update()
    {
        if (provider == null) return;
        Frame frame = provider.CurrentFrame;
        if (frame == null) return;

        var now = Time.time;
        var seen = new HashSet<int>();

        foreach (var hand in frame.Hands)
        {
            if (hand.IsLeft && !leftHand)  continue;
            if (hand.IsRight && !rightHand) continue;

            foreach (var tip in EnumerateFingerTips(hand))
            {
                int key = tip.key;
                Vector3 tipWorld = provider.transform.TransformPoint(tip.localMeters);

                var t = GetOrCreateTrail(key);
                t.go.transform.position = tipWorld;
                t.tr.emitting = true;
                t.lastSeen = now;

                // velocità stimata (se l’SDK non espone TipVelocity)
                float speed = 0f;
                if (lastPos.TryGetValue(key, out var prev) && lastTime.TryGetValue(key, out var pt))
                {
                    float dt = Mathf.Max(1e-4f, now - pt);
                    speed = Vector3.Distance(prev, tipWorld) / dt; // m/s
                }
                lastPos[key] = tipWorld;
                lastTime[key] = now;

                if (widthBySpeed)
                {
                    float k = Mathf.InverseLerp(widthSpeedMin, widthSpeedMax, speed);
                    float w = Mathf.Lerp(startWidth * 0.6f, startWidth * widthScale, k);
                    t.tr.startWidth = w; t.tr.endWidth = endWidth;
                }

                seen.Add(key);
            }
        }

        // dita non viste: smetti di emettere (svanisce con time)
        foreach (var kv in trails)
            if (!seen.Contains(kv.Key) && Time.time - kv.Value.lastSeen > 0.05f)
                kv.Value.tr.emitting = false;
    }

    // --------- compat layer: raccoglie i polpastrelli con reflection ----------
    struct TipInfo { public int key; public Vector3 localMeters; }

    IEnumerable<TipInfo> EnumerateFingerTips(Hand hand)
    {
        var ht = hand.GetType();

        // 1) Prova proprietà "Fingers" (lista/collezione)
        var fingersProp = ht.GetProperty("Fingers");
        if (fingersProp != null)
        {
            var list = fingersProp.GetValue(hand, null) as IEnumerable;
            if (list != null)
            {
                foreach (var f in list)
                {
                    int key = GetFingerKey(f);
                    if (TryGetTipLocalMeters(f, out var p))
                        yield return new TipInfo { key = keyForHand(hand, key), localMeters = p };
                }
                yield break;
            }
        }

        // 2) Fallback: proprietà singole Thumb/Index/Middle/Ring/Pinky
        string[] names = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        for (int i = 0; i < names.Length; i++)
        {
            var fp = ht.GetProperty(names[i]);
            if (fp == null) continue;
            var f = fp.GetValue(hand, null);
            if (f == null) continue;
            if (TryGetTipLocalMeters(f, out var p))
                yield return new TipInfo { key = keyForHand(hand, i), localMeters = p };
        }

        // helper locale: aggiunge offset in base alla mano per evitare collisioni key
        int keyForHand(Hand h, int baseKey) => (h.Id * 10) + baseKey;
    }

    int GetFingerKey(object fingerObj)
    {
        // prova leggere enum Type (se esiste) come int (0..4)
        var ft = fingerObj.GetType();
        var typeProp = ft.GetProperty("Type");
        if (typeProp != null)
        {
            try { return Convert.ToInt32(typeProp.GetValue(fingerObj, null)); }
            catch { /* ignore */ }
        }
        return 0; // default se non disponibile
    }

    bool TryGetTipLocalMeters(object fingerObj, out Vector3 localMeters)
    {
        localMeters = Vector3.zero;
        var ft = fingerObj.GetType();

        // TipPosition property con campi x,y,z (mm)
        var tipProp = ft.GetProperty("TipPosition");
        if (tipProp != null)
        {
            var tip = tipProp.GetValue(fingerObj, null);
            if (TryReadXYZ(tip, out var mm))
            {
                localMeters = mm * leapToUnity;
                return true;
            }
        }

        // Fallback: NextJoint/Tip (alcuni SDK)
        var nextJointProp = ft.GetProperty("NextJoint");
        if (nextJointProp != null)
        {
            var tip = nextJointProp.GetValue(fingerObj, null);
            if (TryReadXYZ(tip, out var mm))
            {
                localMeters = mm * leapToUnity;
                return true;
            }
        }

        return false;
    }

    bool TryReadXYZ(object vec, out Vector3 valMm)
    {
        valMm = Vector3.zero;
        if (vec == null) return false;
        var vt = vec.GetType();
        // proprietà x,y,z (double/float) presenti sia in Leap.Vector che in alcuni tipi custom
        var px = vt.GetProperty("x"); var py = vt.GetProperty("y"); var pz = vt.GetProperty("z");
        if (px != null && py != null && pz != null)
        {
            float x = Convert.ToSingle(px.GetValue(vec, null));
            float y = Convert.ToSingle(py.GetValue(vec, null));
            float z = Convert.ToSingle(pz.GetValue(vec, null));
            valMm = new Vector3(x, y, z);
            return true;
        }
        return false;
    }

    // --------- trail creation ----------
    Trail GetOrCreateTrail(int key)
    {
        if (trails.TryGetValue(key, out var t)) return t;

        t = new Trail();
        t.go = new GameObject("FingerTrail_" + key);
        t.go.transform.SetParent(transform, worldPositionStays: true);

        var tr = t.go.AddComponent<TrailRenderer>();
        tr.time = lifetime;
        tr.minVertexDistance = minVertexDistance;
        tr.startWidth = startWidth;
        tr.endWidth = endWidth;
        tr.numCornerVertices = 4;
        tr.numCapVertices = 4;
        tr.colorGradient = colorGradient;

        if (trailMaterial != null) tr.material = trailMaterial;
        else
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            tr.material = m;
        }

        t.tr = tr;
        trails[key] = t;
        return t;
    }
}
