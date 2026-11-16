using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct TrackSample
{
    public Vector3 position;
    public Vector3 tangent;
    public Vector3 side;
    public float distance;
}

public sealed class TrackPath : MonoBehaviour
{
    /// <summary>
    /// Checkpoint count comes from the generated layout, not from a constant
    /// here: the JSON is the single source of truth and track_design_studio.py
    /// writes it into TrackLayout.generated.cs. A hard-coded 10 here silently
    /// overrode the layout (COSTA NOVA asked for 12 and got 10).
    /// </summary>
    public static int CheckpointCount { get { return TrackLayout.CheckpointCount; } }
    public TrackSample[] Samples { get; private set; }
    public float TotalLength { get; private set; }
    public int Count { get { return Samples.Length; } }

    private void Awake()
    {
        if (Samples == null || Samples.Length == 0) Samples = BuildSamples();
        TotalLength = Samples[Samples.Length - 1].distance + Vector3.Distance(Samples[Samples.Length - 1].position, Samples[0].position);
    }

    public void Initialize()
    {
        Awake();
    }

    /// <summary>
    /// Track design source of truth: assets/track_layout.json, exported by
    /// tools/track_design_studio.py --emit-csharp into TrackLayout.generated.cs.
    /// The original hand-written control array (uniformly scaled by 0.55 around
    /// centroid 121.5/27.2/306.9) is preserved in that JSON; the scale is already
    /// baked into every generated point, so no second transform happens here.
    /// Edit the JSON, preview in docs/track_design/track_preview.html, re-emit,
    /// then rebuild -- never hand-edit the generated C# file.
    /// </summary>
    public static Vector3[] ControlPoints()
    {
        return TrackLayout.ControlPoints;
    }

    public static TrackSample[] BuildSamples()
    {
        var controls = ControlPoints();
        var result = new List<TrackSample>(controls.Length * 24);
        float distance = 0f;
        Vector3 previous = Vector3.zero;
        for (int i = 0; i < controls.Length; i++)
        {
            Vector3 p0 = controls[(i - 1 + controls.Length) % controls.Length];
            Vector3 p1 = controls[i];
            Vector3 p2 = controls[(i + 1) % controls.Length];
            Vector3 p3 = controls[(i + 2) % controls.Length];
            int steps = Mathf.Max(12, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 4f));
            for (int j = 0; j < steps; j++)
            {
                float t = j / (float)steps;
                Vector3 p = 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                if (result.Count > 0) distance += Vector3.Distance(previous, p);
                result.Add(new TrackSample { position = p, distance = distance });
                previous = p;
            }
        }
        for (int i = 0; i < result.Count; i++)
        {
            Vector3 tangent = (result[(i + 1) % result.Count].position - result[(i - 1 + result.Count) % result.Count].position).normalized;
            var sample = result[i];
            sample.tangent = tangent;
            sample.side = Vector3.Cross(Vector3.up, tangent).normalized;
            result[i] = sample;
        }
        return result.ToArray();
    }

    public int Wrap(int index) { return (index % Count + Count) % Count; }
    public TrackSample At(int index) { return Samples[Wrap(index)]; }
    public Vector3 PointAt(int index, float lane = 0f) { var s = At(index); return s.position + s.side * lane; }
    public Quaternion RotationAt(int index) { return Quaternion.LookRotation(At(index).tangent, Vector3.up); }
    public int IndexAtDistance(float distance)
    {
        float d = Mathf.Repeat(distance, TotalLength);
        for (int i = 0; i < Count; i++) if (Samples[i].distance >= d) return i;
        return 0;
    }
    public int IndexAhead(int index, float meters) { return IndexAtDistance(At(index).distance + meters); }

    public int NearestIndex(Vector3 position, int hint = -1, int window = 55)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        if (hint >= 0)
        {
            for (int j = -window; j <= window; j++)
            {
                int i = Wrap(hint + j);
                float d = (Samples[i].position - position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            if (bestDistance < 45f * 45f) return best;
        }
        for (int i = 0; i < Count; i++)
        {
            float d = (Samples[i].position - position).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    public float LateralDistance(Vector3 position, int index)
    {
        Vector3 delta = position - At(index).position;
        return Mathf.Abs(Vector3.Dot(delta, At(index).side));
    }

    public float SignedLateral(Vector3 position, int index)
    {
        return Vector3.Dot(position - At(index).position, At(index).side);
    }

    public int CircularDistance(int a, int b)
    {
        int direct = Mathf.Abs(a - b);
        return Mathf.Min(direct, Count - direct);
    }

    public float Curvature(int index, float aheadMeters = 34f)
    {
        int ahead = IndexAhead(index, aheadMeters);
        return Vector3.Angle(At(index).tangent, At(ahead).tangent) / aheadMeters;
    }

    public float TargetSpeed(int index, float skill = 1f)
    {
        float c1 = Curvature(index, 25f);
        float c2 = Curvature(index, 65f);
        float curvature = Mathf.Max(c1, c2 * 1.25f);
        float target = Mathf.Lerp(61f, 15f, Mathf.InverseLerp(0.015f, 0.19f, curvature));
        return target * Mathf.Lerp(0.87f, 1.03f, skill);
    }
}
