using System;
using UnityEngine;

/// <summary>
/// Swaps the visible body of the player car at runtime. All variants are
/// instantiated by the editor builder and kept inactive; physics (rigidbody,
/// colliders, WheelColliders) is shared and never rebuilt, so switching cars
/// cannot change race fairness.
///
/// This class lives in its own file on purpose: Unity only creates a
/// MonoScript reference for a MonoBehaviour whose file name matches the class
/// name. When VehicleGarage lived inside VehicleCatalog.cs the scene saved a
/// null script reference and the garage silently did nothing at runtime
/// (found by the build report warning on 2026-11-15).
/// </summary>
public sealed class VehicleGarage : MonoBehaviour
{
    [Serializable]
    public sealed class Variant
    {
        public GameObject root;
        public Transform[] wheels = new Transform[4];
        public Quaternion[] offsets = new Quaternion[4];
        /// <summary>Measured roof height above the ground, in metres. The
        /// procedural rear wing is positioned relative to this so one kit
        /// fits bodies from 1.24 m (executive saloon) to 1.93 m (city car).</summary>
        public float roofHeight = 1.18f;
        /// <summary>Half the body's width, in metres. The side skirts are
        /// pushed just outside it.</summary>
        public float halfWidth = 0.99f;
        /// <summary>Lowest point of the body in car space, in metres. The
        /// diffuser fins sit just above it.</summary>
        public float floorY = 0f;
    }

    public Variant[] variants = new Variant[0];
    public ArcadeCarController controller;
    public string[] specIds = new string[0];
    public GameObject aeroKit;
    public GameObject lightKit;
    public int selected;

    private static int persisted = -1;

    public int Count => variants.Length;

    public VehicleSpec Current
    {
        get
        {
            if (specIds.Length == 0 || selected < 0 || selected >= specIds.Length) return VehicleCatalog.Cars[0];
            return VehicleCatalog.Cars[VehicleCatalog.IndexOf(specIds[selected])];
        }
    }

    /// <summary>Remembered across scene reloads (restart / return to menu).</summary>
    public static int PersistedIndex
    {
        get => persisted;
        set => persisted = value;
    }

    private void Awake()
    {
        if (!controller) controller = GetComponent<ArcadeCarController>();
        int start = persisted >= 0 ? persisted : Mathf.Clamp(selected, 0, Mathf.Max(0, variants.Length - 1));
        Apply(start);
    }

    public void Apply(int index)
    {
        if (variants == null || variants.Length == 0) return;
        index = ((index % variants.Length) + variants.Length) % variants.Length;
        selected = index;
        persisted = index;
        for (int v = 0; v < variants.Length; v++)
            if (variants[v].root) variants[v].root.SetActive(v == index);

        // The procedural kits are authored in the car's local space with the
        // nose at +Z, which matches the shared physics root (wheel_fl sits at
        // +Z, wheel_rl at -Z). No yaw correction is needed here: the model
        // wrappers carry their own yaw, but the kits are already aligned with
        // the root's forward axis.
        FitKitsToBody();
        if (aeroKit)
        {
            aeroKit.SetActive(Current.aeroKit);
            // The wing plane is authored at y = 0 inside the kit, so the kit's
            // own height places the plane. Sit it 6 cm above the selected
            // body's roof line: never intersecting the body, never floating.
            aeroKit.transform.localPosition = new Vector3(0f, ChosenRoof + 0.06f, 0f);
        }
        if (!controller) return;
        var chosen = variants[index];
        controller.wheelVisuals = chosen.wheels;
        controller.wheelRotationOffsets = chosen.offsets;
    }

    public void Next() => Apply(selected + 1);
    public void Previous() => Apply(selected - 1);

    /// <summary>Roof height of the currently selected body, in metres.</summary>
    public float ChosenRoof
    {
        get
        {
            if (variants == null || variants.Length == 0) return 1.18f;
            int i = ((selected % variants.Length) + variants.Length) % variants.Length;
            return variants[i].roofHeight > 0f ? variants[i].roofHeight : 1.18f;
        }
    }

    /// <summary>Half width of the currently selected body, in metres.</summary>
    public float ChosenHalfWidth
    {
        get
        {
            if (variants == null || variants.Length == 0) return 0.99f;
            int i = ((selected % variants.Length) + variants.Length) % variants.Length;
            return variants[i].halfWidth > 0f ? variants[i].halfWidth : 0.99f;
        }
    }

    /// <summary>Lowest body point of the currently selected body (m).</summary>
    public float ChosenFloor
    {
        get
        {
            if (variants == null || variants.Length == 0) return 0f;
            int i = ((selected % variants.Length) + variants.Length) % variants.Length;
            return variants[i].floorY;
        }
    }

    /// <summary>
    /// Push the side skirts just outside the body and drop the diffuser fins
    /// onto the underside. Both kits are authored with fixed offsets tuned for
    /// one body; the fleet spans 1.79 m to 2.27 m in width, so a fixed x puts
    /// the skirts inside some bodies and outside others.
    /// </summary>
    private void FitKitsToBody()
    {
        float x = ChosenHalfWidth + 0.02f;
        if (lightKit)
        {
            foreach (Transform t in lightKit.transform)
            {
                if (!t.name.StartsWith("Side skirt")) continue;
                Vector3 p = t.localPosition;
                p.x = (p.x < 0f ? -1f : 1f) * x;
                p.y = Mathf.Max(0.16f, ChosenFloor + 0.22f);
                t.localPosition = p;
            }
        }
        if (aeroKit)
        {
            float finY = Mathf.Max(0.12f, ChosenFloor + 0.20f);
            foreach (Transform t in aeroKit.transform)
            {
                if (!t.name.StartsWith("Diffuser fin")) continue;
                Vector3 p = t.localPosition;
                p.y = finY;
                t.localPosition = p;
            }
            // The wing is a narrow blade, so it scales with the body width.
            float wingW = Mathf.Max(1.05f, ChosenHalfWidth * 1.15f);
            foreach (Transform t in aeroKit.transform)
            {
                Vector3 p = t.localPosition;
                Vector3 s = t.localScale;
                if (t.name == "Rear wing plane") s.x = wingW;
                else if (t.name.StartsWith("Rear wing post")) { p.x = Mathf.Sign(p.x) * wingW * 0.42f; s.x = 0.05f; }
                t.localPosition = p;
                t.localScale = s;
            }
        }
    }

    public string Label()
    {
        var spec = Current;
        return spec.displayName;
    }

    public string Credit() => Current.credit;
}
