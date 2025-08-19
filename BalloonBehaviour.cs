using UnityEngine;

/// Stile palloncino: resta vicino a un punto di riposo, con oscillazione e lieve “brezza”.
[RequireComponent(typeof(Rigidbody))]
public class BalloonBehaviour : MonoBehaviour
{
    public Transform anchor;              // punto di riposo (obbligatorio)
    [Header("Molla verso anchor (Hooke)")]
    [Tooltip("Forza di richiamo per metro di distanza (m/s^2). 2–6 dà un buon feeling.")]
    public float stiffnessHorizontal = 4.0f;
    public float stiffnessVertical   = 5.0f;
    [Tooltip("Smorzamento proporzionale alla velocità (m/s^2 per m/s).")]
    public float damping = 1.6f;

    [Header("Brezza (micro rumore)")]
    public float breezeAccel = 0.25f;       // intensità (m/s^2)
    public float breezeScale = 0.2f;        // scala del rumore
    public float breezeFreq  = 0.25f;       // frequenza del rumore

    [Header("Limiti & sicurezza")]
    public float maxSpeed = 3.0f;
    public float tetherLength = 3.0f;       // opzionale: come un filo invisibile
    public bool clampToTether = true;

    Rigidbody rb;
    Vector3 noiseOffset;

    void Awake(){
        rb = GetComponent<Rigidbody>();
        // Suggeriti: li setto se sono diversi
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        if (rb.drag < 1.5f) rb.drag = 2.5f;
        if (rb.angularDrag < 3f) rb.angularDrag = 4f;

        // offset casuale per la brezza
        noiseOffset = new Vector3(
            Random.Range(0f,1000f),
            Random.Range(0f,1000f),
            Random.Range(0f,1000f)
        );
    }

    void FixedUpdate(){
        if (!anchor) return;

        Vector3 pos = rb.position;
        Vector3 to  = anchor.position - pos;

        // Molla anisotropa: un filo più “forte” in verticale
        Vector3 toXZ = new Vector3(to.x, 0f, to.z);
        Vector3 toY  = new Vector3(0f, to.y, 0f);

        Vector3 accelSpring =
            toXZ * stiffnessHorizontal + toY * stiffnessVertical;

        // Smorzamento proporzionale alla velocità
        Vector3 accelDamp = -rb.velocity * damping;

        // Brezza leggera tipo Perlin
        float t = Time.time * breezeFreq;
        Vector3 accelBreeze = breezeAccel * new Vector3(
            Mathf.PerlinNoise(noiseOffset.x + t, noiseOffset.x) - 0.5f,
            Mathf.PerlinNoise(noiseOffset.y + t, noiseOffset.y) - 0.5f,
            Mathf.PerlinNoise(noiseOffset.z + t, noiseOffset.z) - 0.5f
        ) * (2.0f * breezeScale);

        // Applica accelerazioni (indipendenti dalla massa)
        Vector3 accel = accelSpring + accelDamp + accelBreeze;
        rb.AddForce(accel, ForceMode.Acceleration);

        // Tether: se oltre la lunghezza, tira indietro
        if (clampToTether){
            Vector3 fromAnchor = rb.position - anchor.position;
            float d = fromAnchor.magnitude;
            if (d > tetherLength){
                Vector3 pull = -fromAnchor.normalized * (d - tetherLength) * Mathf.Max(stiffnessHorizontal, stiffnessVertical);
                rb.AddForce(pull, ForceMode.Acceleration);
            }
        }

        // Limita velocità per evitare “schizzi” con le mani
        if (rb.velocity.sqrMagnitude > maxSpeed*maxSpeed){
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }
    }

    // Gizmo utile in editor
    void OnDrawGizmosSelected(){
        if (!anchor) return;
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(anchor.position, 0.05f);
        if (tetherLength > 0f){
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireSphere(anchor.position, tetherLength);
        }
    }
}
