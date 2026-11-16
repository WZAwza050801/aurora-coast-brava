using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class ArcadeCarController : MonoBehaviour
{
    [Header("Setup")]
    public bool aiControlled;
    public bool controlsLocked;
    public TrackPath track;
    public RaceManager race;
    public int trackIndex;
    public Transform[] wheelVisuals = new Transform[4];
    public WheelCollider[] wheels = new WheelCollider[4];
    public Quaternion[] wheelRotationOffsets = new Quaternion[4];
    public ParticleSystem[] boostFlames;
    public Light[] boostLights;
    [Header("Tuning")]
    public float maxMotorTorque = 1750f;
    public float maxSpeed = 57f;
    public float nitroMaxSpeed = 76f;
    public float nitro = 55f;
    public bool NitroActive { get; private set; }
    /// <summary>Sustained boost (>1.5 s) turns into a harder violet overdrive.</summary>
    public bool NitroOverdrive { get; private set; }
    /// <summary>Seconds the current boost has been held continuously. This is
    /// what gates the overdrive state, and it is reported in telemetry so the
    /// overdrive branch can be proven at runtime instead of asserted.</summary>
    public float NitroStreak { get { return nitroStreak; } }
    /// <summary>Top-speed budget while boosting (m/s) vs. without (m/s).</summary>
    public float SpeedCapKph { get { return (NitroActive ? nitroMaxSpeed : maxSpeed) * 3.6f; } }
    /// <summary>0..1 impact energy, decays over ~0.45 s; drives camera shake.</summary>
    public float ImpactStrength { get; private set; }
    /// <summary>Real collision events above 3 m/s, written into race telemetry.</summary>
    public int CollisionCount { get; private set; }
    public float SpeedKph { get; private set; }
    public float DriftAmount { get; private set; }
    public int GroundedWheels { get; private set; }
    public float Throttle { get; private set; }
    public float Steer { get; private set; }
    public bool Handbrake { get; private set; }
    public Rigidbody Body { get; private set; }
    public Vector3 LastSafePosition { get; private set; }
    public Quaternion LastSafeRotation { get; private set; }
    private float safeTimer;
    private float airTime;
    private float nitroCooldown;
    private float nitroStreak;
    private float collisionCooldown;
    private float baseRearGrip = 1.35f;
    private float testThrottle;
    private float testSteer;
    private bool testHandbrake;
    private bool testNitro;
    private ParticleSystem.EmissionModule[] flameEmission;

    private void Awake()
    {
        Body = GetComponent<Rigidbody>();
        for (int i = 0; i < wheelRotationOffsets.Length; i++)
        {
            Quaternion offset = wheelRotationOffsets[i];
            if (offset.x == 0f && offset.y == 0f && offset.z == 0f && offset.w == 0f) wheelRotationOffsets[i] = Quaternion.identity;
        }
        Body.mass = 1350f;
        Body.linearDamping = 0.025f;
        Body.angularDamping = 0.55f;
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Body.centerOfMass = new Vector3(0f, -0.32f, 0.08f);
        LastSafePosition = transform.position;
        LastSafeRotation = transform.rotation;
        if (boostFlames != null)
        {
            flameEmission = new ParticleSystem.EmissionModule[boostFlames.Length];
            for (int i = 0; i < boostFlames.Length; i++) if (boostFlames[i]) flameEmission[i] = boostFlames[i].emission;
        }
    }

    public const float StickDeadzone = InputMap.StickDeadzone;

    /// <summary>Legacy Input axes already include joystick axes 0/1 (dead 0.19,
    /// inverted Y); this removes the remaining stick drift around centre.</summary>
    public static float ApplyDeadzone(float value, float deadzone)
    {
        float magnitude = Mathf.Abs(value);
        if (magnitude <= deadzone) return 0f;
        return Mathf.Sign(value) * (magnitude - deadzone) / Mathf.Max(0.0001f, 1f - deadzone);
    }

    private void Update()
    {
        if (!aiControlled)
        {
            Throttle = controlsLocked ? 0f : ApplyDeadzone(Input.GetAxis("Vertical"), StickDeadzone);
            Steer = controlsLocked ? 0f : ApplyDeadzone(Input.GetAxis("Horizontal"), StickDeadzone);
            // A = handbrake, X/RB = nitro, B = reset, Start = pause (RaceManager).
            Handbrake = !controlsLocked && InputMap.Handbrake(
                Input.GetKey(KeyCode.Space),
                Input.GetKey(KeyCode.JoystickButton0));
            if (InputMap.Reset(
                    Input.GetKeyDown(KeyCode.R),
                    Input.GetKeyDown(KeyCode.JoystickButton1)))
            {
                if (track)
                {
                    if (race && race.player == this) race.ResetPlayer(); else ResetToTrack(trackIndex);
                }
                else ResetCar();
            }
            bool nitroHeld = InputMap.Nitro(
                Input.GetKey(KeyCode.LeftShift),
                Input.GetKey(KeyCode.RightShift),
                Input.GetKey(KeyCode.JoystickButton2),
                Input.GetKey(KeyCode.JoystickButton5));
            if (!controlsLocked && nitroHeld) nitroCooldown = 0.35f;
        }
        else
        {
            Throttle = controlsLocked ? 0f : testThrottle;
            Steer = controlsLocked ? 0f : testSteer;
            Handbrake = !controlsLocked && testHandbrake;
            if (!controlsLocked && testNitro) nitroCooldown = 0.35f;
        }
        for (int i = 0; i < wheels.Length; i++)
        {
            if (!wheels[i] || !wheelVisuals[i]) continue;
            Vector3 pos; Quaternion rot;
            wheels[i].GetWorldPose(out pos, out rot);
            wheelVisuals[i].SetPositionAndRotation(pos, rot * wheelRotationOffsets[i]);
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        collisionCooldown = Mathf.Max(0f, collisionCooldown - dt);
        ImpactStrength = Mathf.Max(0f, ImpactStrength - dt * 2.2f);
        Vector3 velocity = Body.linearVelocity;
        float forwardSpeed = Vector3.Dot(velocity, transform.forward);
        SpeedKph = velocity.magnitude * 3.6f;
        GroundedWheels = 0;
        for (int i = 0; i < wheels.Length; i++) if (wheels[i] && wheels[i].isGrounded) GroundedWheels++;
        bool grounded = GroundedWheels >= 2;
        float speedAbs = Mathf.Abs(forwardSpeed);
        float speedFactor = Mathf.InverseLerp(5f, 60f, speedAbs);
        float steerLimit = Mathf.Lerp(29f, 9.5f, speedFactor);
        float steerInput = Steer;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (!wheels[i]) continue;
            if (i >= 2) wheels[i].steerAngle = steerInput * steerLimit;
            WheelFrictionCurve side = wheels[i].sidewaysFriction;
            side.extremumSlip = Handbrake && i < 2 ? 0.48f : 0.28f;
            side.extremumValue = Handbrake && i < 2 ? 0.68f : (i < 2 ? baseRearGrip : 1.55f);
            side.asymptoteSlip = Handbrake && i < 2 ? 0.9f : 0.65f;
            side.asymptoteValue = Handbrake && i < 2 ? 0.52f : (i < 2 ? 0.92f : 1.15f);
            side.stiffness = 1f;
            wheels[i].sidewaysFriction = side;
            bool braking = Handbrake || (Throttle < -0.1f && forwardSpeed > 1f);
            wheels[i].brakeTorque = braking ? (Handbrake ? 5200f : 4600f) : 0f;
            float topSpeed = NitroActive ? nitroMaxSpeed : maxSpeed;
            float torqueFade = Mathf.Clamp01(1f - speedAbs / topSpeed);
            float direction = forwardSpeed < -1f ? Throttle : Mathf.Max(Throttle, braking ? 0f : -0.45f);
            wheels[i].motorTorque = direction * maxMotorTorque * torqueFade * (i < 2 ? 0.62f : 0.38f);
            if (controlsLocked)
            {
                wheels[i].motorTorque = 0f;
                wheels[i].brakeTorque = 9000f;
            }
        }
        bool wantsNitro = nitroCooldown > 0f && nitro > 0.5f && forwardSpeed > 5f && !Handbrake;
        NitroActive = wantsNitro;
        nitroStreak = NitroActive ? nitroStreak + dt : 0f;
        NitroOverdrive = nitroStreak > 1.5f;
        nitroCooldown = Mathf.Max(0f, nitroCooldown - dt);
        if (NitroActive)
        {
            nitro = Mathf.Max(0f, nitro - 24f * dt);
            float boostFade = Mathf.Clamp01(1f - forwardSpeed / nitroMaxSpeed);
            Body.AddForce(transform.forward * (10500f * boostFade * (NitroOverdrive ? 1.22f : 1f)), ForceMode.Force);
        }
        UpdateFlames(NitroActive);
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        DriftAmount = grounded ? Mathf.Clamp01(Mathf.Abs(localVelocity.x) / 11f) : 0f;
        if (grounded)
        {
            Body.AddForce(-transform.up * (18f + speedAbs * 8f), ForceMode.Force);
            float yawAssist = Steer * Mathf.Clamp(speedAbs, 0f, 45f) * (Handbrake ? 25f : 13f);
            Body.AddTorque(Vector3.up * yawAssist, ForceMode.Force);
            Vector3 lateral = transform.right * localVelocity.x;
            Body.AddForce(-lateral * (Handbrake ? 45f : 115f), ForceMode.Force);
            if (Handbrake && speedAbs > 10f && Mathf.Abs(Steer) > 0.12f) nitro = Mathf.Min(100f, nitro + (5f + DriftAmount * 11f) * dt);
            if (Mathf.Abs(Steer) < 0.05f && DriftAmount < 0.08f && speedAbs > 16f) nitro = Mathf.Min(100f, nitro + 0.65f * dt);
            safeTimer += dt;
            if (safeTimer > 0.65f && Vector3.Dot(transform.up, Vector3.up) > 0.75f)
            {
                LastSafePosition = transform.position + Vector3.up * 0.35f;
                LastSafeRotation = transform.rotation;
                safeTimer = 0f;
            }
            airTime = 0f;
        }
        else
        {
            airTime += dt;
            Body.AddTorque(-transform.right * Steer * 280f, ForceMode.Force);
            if (airTime > 0.45f) nitro = Mathf.Min(100f, nitro + 10f * dt);
        }
        ApplyAntiRoll(0, 1);
        ApplyAntiRoll(2, 3);
        if (transform.position.y < -35f || Vector3.Dot(transform.up, Vector3.up) < -0.25f)
        {
            ResetCar();
            if (race) race.NotifyCarReset(this);
        }
    }

    private void ApplyAntiRoll(int a, int b)
    {
        if (!wheels[a] || !wheels[b]) return;
        WheelHit hit;
        float left = 1f, right = 1f;
        bool groundedA = wheels[a].GetGroundHit(out hit);
        if (groundedA) left = (-wheels[a].transform.InverseTransformPoint(hit.point).y - wheels[a].radius) / wheels[a].suspensionDistance;
        bool groundedB = wheels[b].GetGroundHit(out hit);
        if (groundedB) right = (-wheels[b].transform.InverseTransformPoint(hit.point).y - wheels[b].radius) / wheels[b].suspensionDistance;
        float force = (left - right) * 7200f;
        if (groundedA) Body.AddForceAtPosition(wheels[a].transform.up * -force, wheels[a].transform.position);
        if (groundedB) Body.AddForceAtPosition(wheels[b].transform.up * force, wheels[b].transform.position);
    }

    private void UpdateFlames(bool active)
    {
        if (boostFlames != null)
        {
            for (int i = 0; i < boostFlames.Length; i++)
            {
                if (!boostFlames[i]) continue;
                var emission = boostFlames[i].emission;
                emission.enabled = active;
                if (active && !boostFlames[i].isPlaying) boostFlames[i].Play();
            }
        }
        if (boostLights != null) foreach (var l in boostLights) if (l) l.enabled = active;
    }

    public void SetTestInput(float throttle, float steer, bool handbrake, bool boost)
    {
        testThrottle = throttle; testSteer = steer; testHandbrake = handbrake; testNitro = boost;
    }

    public void ResetCar()
    {
        Body.linearVelocity = Vector3.zero;
        Body.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(LastSafePosition + Vector3.up * 0.5f, LastSafeRotation);
    }

    public void ResetToTrack(int index)
    {
        if (!track) { ResetCar(); return; }
        trackIndex = index;
        ResetToPose(track.PointAt(index) + Vector3.up * 1.1f, track.RotationAt(index));
    }

    public void ResetToPose(Vector3 position, Quaternion rotation)
    {
        Body.linearVelocity = Vector3.zero;
        Body.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(position, rotation);
        LastSafePosition = position;
        LastSafeRotation = rotation;
        nitroCooldown = 0f;
        nitroStreak = 0f;
        safeTimer = 0f;
        airTime = 0f;
        collisionCooldown = 0f;
        NitroActive = false;
        NitroOverdrive = false;
        ImpactStrength = 0f;
        SetTestInput(0f, 0f, false, false);
        UpdateFlames(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collisionCooldown > 0f) return;
        collisionCooldown = 0.28f;
        float impulse = collision.relativeVelocity.magnitude;
        if (impulse < 3f) return;                       // ignore gentle terrain contact
        CollisionCount++;
        ImpactStrength = Mathf.Clamp01(impulse / 18f);
        var otherCar = collision.collider.GetComponentInParent<ArcadeCarController>();
        if (otherCar && otherCar != this)
        {
            // Pack racing: trade lateral momentum instead of sticking together.
            Vector3 push = (otherCar.transform.position - transform.position).normalized;
            otherCar.Body.AddForce(push * (impulse * 55f), ForceMode.Impulse);
            Body.AddForce(-push * (impulse * 25f), ForceMode.Impulse);
        }
        else
        {
            // Barrier scrape: capped speed bleed so an AI lap still completes.
            float bleed = Mathf.Min(0.12f, impulse * 0.006f);
            Body.linearVelocity *= 1f - bleed;
        }
        Body.AddTorque(transform.right * Mathf.Min(impulse * 3f, 30f), ForceMode.Impulse);
    }

}
