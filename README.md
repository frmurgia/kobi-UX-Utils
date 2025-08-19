# Finger Trails & Utils (URP / Ultraleap / XR Hands / HoloLens 2)

Utilities

---

## ✨ Features

* **Finger trails** sottili con **TrailRenderer** (indice/polpastrelli), pallino di aggancio, spessore costante in pixel.
* **Wireframe** via baricentriche nei vertex colors → stabile su Metal/URP (no geometry shader).
* **Balloon physics**: molla + smorzamento + brezza + tether (filo) → oggetto fluttuante e reattivo al tocco.
* **Flycam** con frecce della tastiera, inerzia regolabile, lock quota.
* Pronti per **Ultraleap** (desktop/VR) **e/o** **XR Hands / MRTK** (HoloLens 2).

---

## 📦 Contenuto / Scripts

> Tutti gli script sono in `Assets/Scripts` (o come preferisci). Gli shader sono in `Assets/Shaders`.


### `ArrowKeysFlyCam.cs`

Muove la camera con **↑ ↓ ← →**, **E/Q** (su/giù), **Shift/Ctrl** (veloce/lento) e **inerzia** al rilascio (half‑life separata per decelerazione). Opzione per usare le frecce come **rotazione**.

**Uso**

* Aggancia alla **Main Camera** (per XR solo come camera di debug nell’Editor).
* Parametri chiave: `decelHalfLife`, `acceleration`, `rotateWithArrows`.

---

### `AttachTrailsToLeapTips.cs`

Cerca i **tip** per nome (`index_end`, `*Distal*`, ecc.) sotto una root e attacca un **TrailRenderer** ad ogni dito (SX/DX). Non dipende direttamente dall’SDK Leap: se il rig espone i transform dei tip, funziona.

**Uso**

* Metti il componente sulla root delle mani (Ultraleap Ghost/Physical Hands o rig MRTK/XR Hands).
* Se non trova i tip, popolali in `manualTips` (10 Transform: pollice‑mignolo SX/DX).
* Materiale consigliato: `URP/Particles/Unlit`, **Transparent/Alpha o Additive**, RenderQueue ≥ 3050.

---

### `GhostHandFingerTrails.cs`

Versione “completa” per Ghost Hands Ultraleap. Crea scie **più sottili** + **pallino** al tip e gestisce lo spessore in pixel.

**Uso**

* Assegna `searchRoot` alla gerarchia delle mani.
* Imposta `trailMaterial` e (opzionale) `dotMaterial`.

---

### `LeapFingerTrails.cs`

Scie guidate direttamente dall’**API Ultraleap** (richiede `LeapProvider`). Per HoloLens 2 preferisci `AttachTrailsToLeapTips` oppure l’adapter XR Hands (vedi sotto).

---

### `BalloonBehaviour.cs`

Fisica stile **palloncino**: l’oggetto oscilla attorno a un **anchor** (molla anisotropa + damping), supporta “brezza”, **buoyancy** opzionale e **tether** (filo) con raggio max.

**Uso**

1. Aggiungi **Rigidbody** (Gravity **OFF**). Consigli: `Drag 2.5–3.5`, `Angular Drag 4–6`.
2. Physic Material: `Bounciness 0.2–0.35`, frizioni 0.
3. Metti `BalloonBehaviour` e assegna l’**anchor** (o lascia auto‑anchor in Start).

---

## 🖌️ Shader inclusi (facoltativi)

* **`URP_WireframeBary.shader`** — Wireframe anti‑alias in **Transparent**, spessore in pixel (`_LineWidth`), fill opzionale (`_Fill`). *Richiede `AddBarycentric`.*
* **`URP_SolidUnlit.shader`** — Colore pieno unlit (`Queue = Geometry`).
* **`URP_TransparentGreyUnlit.shader`** — Unlit trasparente grigio con toggle `ZWrite` per gestire ordinamenti.

> Copiali in `Assets/Shaders` e crea i **Material** relativi.

---

## ✅ Requisiti

* **Unity**: 2021.3+ / 2022.3+ (URP attivo).
* **URP** per gli shader custom.
* **Ultraleap** *oppure* **XR Hands/MRTK** per il tracciamento mani (a seconda della piattaforma).
* **HoloLens 2**: 

---



## 🪟 HoloLens 2 / XR Hands

* Abilita **OpenXR** con **Hand Tracking** e installa **XR Hands** (Package Manager).
* Con MRTK3 puoi ottenere i transform dei joint via utilità dedicate; in alternativa lascia fare a `AttachTrailsToLeapTips` (search per nome) o compila i `manualTips`.
* Performance: per trail su HL2 usa `lifetime 0.3–0.6 s`, `minVertexDistance 0.003–0.006 m`, materiali **Unlit**.

> Se vuoi un adapter specifico `XRHandsFingerTrails.cs` (scie guidate direttamente da `UnityEngine.XR.Hands`), puoi aggiungerlo nella tua repo come sample opzionale.

---

## 📁 Struttura consigliata

```
Assets/
  Scripts/
    AddBarycentric.cs
    ArrowKeysFlyCam.cs
    AttachTrailsToLeapTips.cs
    GhostHandFingerTrails.cs
    LeapFingerTrails.cs
    BalloonBehaviour.cs
  Shaders/
    URP_WireframeBary.shader
    URP_SolidUnlit.shader
    URP_TransparentGreyUnlit.shader
  Materials/           (materiali per trail/wireframe/trasparenze)
  Prefabs/             (opzionale: mani, balloon, ecc.)
```

---
