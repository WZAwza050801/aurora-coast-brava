using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(Camera))]
public sealed class ChaseCamera : MonoBehaviour
{
    public Transform target;
    public Rigidbody targetBody;
    public ArcadeCarController car;
    public float distance = 7.1f;
    public float height = 2.5f;
    public float lookAhead = 8.5f;
    public float baseFov = 58f;
    public float effects = 0.7f;
    private Vector3 velocity;
    private Camera cam;
    private MotionBlur motionBlur;
    private float shake;
    /// <summary>Field of view currently applied (deg). The nitro overdrive
    /// adds +3 deg on top of the +5 deg boost kick; exposing it lets the
    /// telemetry prove the overdrive branch really moved the camera.</summary>
    public float CurrentFov { get { return cam ? cam.fieldOfView : 0f; } }
    /// <summary>Base field of view without any speed / nitro effect (deg).</summary>
    public float BaseFov { get { return baseFov; } }
    /// <summary>Current shake magnitude 0..1, driven by boost and impacts.</summary>
    public float ShakeAmount { get { return shake; } }
    /// <summary>Amplitude of the positional jitter applied to the camera on the
    /// last frame (metres). This is the number an impact actually produces;
    /// ShakeAmount only tracks the boost component.</summary>
    public float LastShakeAmplitude { get; private set; }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.nearClipPlane = 0.12f;
        cam.farClipPlane = 3500f;
        var volume = FindFirstObjectByType<Volume>();
        if (volume && volume.profile) volume.profile.TryGet(out motionBlur);
    }

    private void LateUpdate()
    {
        if (!target) return;
        if (showroom) { LateUpdateShowroom(); return; }
        float speed = targetBody ? targetBody.linearVelocity.magnitude : 0f;
        bool boosting = car && car.NitroActive;
        float fov = Mathf.Lerp(baseFov, baseFov + 12f, Mathf.InverseLerp(8f, 65f, speed))
                  + (boosting ? 5f * effects : 0f)
                  + (car && car.NitroOverdrive ? 3f * effects : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-5f * Time.deltaTime));
        if (motionBlur != null)
        {
            float blur = effects * Mathf.InverseLerp(18f, 70f, speed) * (boosting ? 0.42f : 0.16f);
            motionBlur.intensity.value = Mathf.Lerp(motionBlur.intensity.value, blur, 1f - Mathf.Exp(-5f * Time.deltaTime));
        }
        float back = distance + Mathf.InverseLerp(10f, 65f, speed) * 1.2f + (boosting ? 0.7f : 0f);
        Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.1f) flatForward = Vector3.forward;
        Vector3 desired = target.position - flatForward * back + Vector3.up * height;
        RaycastHit hit;
        Vector3 pivot = target.position + Vector3.up * 1.15f;
        if (Physics.Raycast(pivot, desired - pivot, out hit, Vector3.Distance(pivot, desired), ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.GetComponentInParent<ArcadeCarController>()) desired = hit.point + hit.normal * 0.35f;
        }
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.12f, 180f);
        Vector3 look = target.position + Vector3.up * 0.8f + flatForward * lookAhead;
        Quaternion wanted = Quaternion.LookRotation(look - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-8f * Time.deltaTime));
        float impact = car ? car.ImpactStrength : 0f;
        if ((boosting && effects > 0.01f) || impact > 0.01f)
        {
            if (boosting) shake = Mathf.Min(1f, shake + Time.deltaTime * 3f);
            float amplitude = 0.018f * effects * shake + 0.075f * impact * effects;
            transform.position += new Vector3(Mathf.Sin(Time.time * 37f), Mathf.Sin(Time.time * 43f), 0f) * amplitude;
            LastShakeAmplitude = amplitude;
        }
        else
        {
            shake = 0f;
            LastShakeAmplitude = 0f;
        }
    }

    public void Snap()
    {
        if (!target) return;
        transform.position = target.position - target.forward * distance + Vector3.up * height;
        transform.rotation = Quaternion.LookRotation(target.position + Vector3.up * 0.8f + target.forward * lookAhead - transform.position);
        velocity = Vector3.zero;
    }

    /// <summary>Menu showroom: the car sits on a turntable and the camera
    /// orbits it, so the player can actually look at the body they picked
    /// instead of staring at the back of a parked car.</summary>
    public bool showroom;
    public float showroomSpin = 12f;
    public float showroomRadius = 6.0f;
    public float showroomHeight = 1.42f;
    /// <summary>Metres the framing is biased sideways. Looking to the left of
    /// the car pushes it into the right-hand third of the screen, which keeps
    /// the menu text column clear.</summary>
    public float showroomBias = 2.0f;
    /// <summary>Metres the framing is biased upwards, which drops the car
    /// slightly below the screen centre.</summary>
    public float showroomLift = 0.30f;

    private void LateUpdateShowroom()
    {
        float t = Time.unscaledTime;
        float angle = t * showroomSpin;
        Vector3 focus = target.position + Vector3.up * 0.72f;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, -showroomRadius);
        Vector3 desired = focus + offset + Vector3.up * (showroomHeight - 0.72f);

        // Keep the camera out of the scenery, same trick as the chase camera.
        RaycastHit hit;
        Vector3 pivot = target.position + Vector3.up * 1.15f;
        if (Physics.Raycast(pivot, desired - pivot, out hit, Vector3.Distance(pivot, desired), ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.GetComponentInParent<ArcadeCarController>()) desired = hit.point + hit.normal * 0.35f;
        }
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.25f, 120f);

        // Frame the car off-centre so the menu column stays readable.
        Vector3 toFocus = focus - transform.position;
        Vector3 right = Vector3.Cross(Vector3.up, toFocus).normalized;
        Vector3 lookTarget = focus - right * showroomBias + Vector3.up * showroomLift;
        Quaternion wanted = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 40f, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
    }

    /// <summary>Stop the turntable and put the camera back behind the car.</summary>
    public void ExitShowroom()
    {
        showroom = false;
        Snap();
    }
}
