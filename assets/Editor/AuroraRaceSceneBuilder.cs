using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static partial class AuroraProjectBuilder
{
    private const string RaceScenePath = "Assets/Scenes/CostaBrava.unity";
    private static TrackPath path;
    private static Material whitePaint, darkPaint, railPaint, stonePaint, roofPaint, glassPaint, ochrePaint, warmWindowPaint;
    // Village clusters are spread over the whole lap instead of one short segment.
    private static readonly float[][] VillageSpans =
    {
        new[] { 0.055f, 0.105f },  // cliff-side hamlet before the jump
        new[] { 0.315f, 0.385f },  // pine village before the tunnel
        new[] { 0.575f, 0.705f },  // Colada, the main seaside town
        new[] { 0.905f, 0.985f }   // finish-side hamlet
    };

    [MenuItem("Aurora/Build Complete Coast Race")]
    public static void BuildCompleteRace()
    {
        try
        {
            ConfigureAssets();
            ConfigureRaceAssetImports();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureRendering();
            CreateLighting();
            CreateRaceMaterials();
            var trackObject = new GameObject("COSTA BRAVA / continuous route");
            path = trackObject.AddComponent<TrackPath>();
            path.Initialize();
            CreateTerrainAndSea();
            Debug.Log("AURORA_STEP terrain done");
            CreateRaceRoad();
            Debug.Log("AURORA_STEP road done");
            CreateGuardrailsAndMarkings();
            Debug.Log("AURORA_STEP guardrails done");
            CreateTunnel();
            Debug.Log("AURORA_STEP tunnel done");
            CreateJumpAndShortcut();
            Debug.Log("AURORA_STEP jump done");
            CreateVillage();
            Debug.Log("AURORA_STEP village done");
            CreateVegetationAndRocks();
            Debug.Log("AURORA_STEP vegetation done");
            CreateDistantLandscape();
            Debug.Log("AURORA_STEP landscape done");
            CreateStartAndRoadSigns();
            Debug.Log("AURORA_STEP signs done");
            var racers = CreateRaceGrid();
            var managerObject = new GameObject("RACE / two laps four drivers");
            var manager = managerObject.AddComponent<RaceManager>();
            manager.track = path;
            manager.player = racers[0];
            manager.cars = new List<ArcadeCarController>(racers);
            managerObject.AddComponent<RaceHud>().race = manager;
            managerObject.GetComponent<RaceHud>().track = path;
            managerObject.AddComponent<FullRaceSmokeTest>().race = manager;
            var camera = CreateCamera(racers[0].gameObject);
            camera.GetComponent<ChaseCamera>().effects = manager.EffectStrength;
            AddAudio(racers[0]);
            CreateAmbientAudio();
            EditorSceneManager.SaveScene(scene, RaceScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RaceScenePath, true) };
            AssetDatabase.SaveAssets();
            BuildRaceWindows();
        }
        catch (Exception ex)
        {
            Debug.LogError("AURORA_RACE_BUILD_FAILED " + ex);
            if (Application.isBatchMode) EditorApplication.Exit(2);
        }
    }

    private static void ConfigureRaceAssetImports()
    {
        foreach (string file in new[] { "Assets/Vegetation/IslandTree.fbx", "Assets/Vegetation/CoastRocks.fbx", "Assets/Vegetation/Shrub.fbx" })
        {
            if (AssetImporter.GetAtPath(file) is ModelImporter importer)
            {
                importer.globalScale = 1f;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(file);
            if (asset) ConvertVegetationMaterials(asset);
        }
    }

    private static void ConvertVegetationMaterials(GameObject asset)
    {
        foreach (var renderer in asset.GetComponentsInChildren<Renderer>())
        {
            var source = renderer.sharedMaterials;
            var converted = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var old = source[i];
                if (!old) continue;
                Texture color = old.HasProperty("_BaseColorMap") ? old.GetTexture("_BaseColorMap") : (old.HasProperty("_MainTex") ? old.GetTexture("_MainTex") : null);
                Texture normal = old.HasProperty("_BumpMap") ? old.GetTexture("_BumpMap") : null;
                var mat = MakeMaterial("Vegetation " + old.name.Replace(" ", "_"), Color.white, 0f, 0.28f, color, normal);
                mat.SetFloat("_DoubleSidedEnable", 1f);
                string n = old.name.ToLowerInvariant();
                if (n.Contains("leaves") || n.Contains("twig") || n.Contains("shrub"))
                {
                    mat.SetFloat("_AlphaCutoffEnable", 1f);
                    mat.SetFloat("_AlphaCutoff", 0.32f);
                }
                converted[i] = mat;
            }
            renderer.sharedMaterials = converted;
        }
    }

    private static void CreateRaceMaterials()
    {
        whitePaint = MakeMaterial("Mediterranean lime plaster", new Color(0.78f, 0.75f, 0.65f), 0f, 0.35f);
        darkPaint = MakeMaterial("Graphite race furniture", new Color(0.025f, 0.04f, 0.055f), 0.55f, 0.4f);
        railPaint = MakeMaterial("Weathered galvanized guardrail", new Color(0.38f, 0.43f, 0.46f), 0.75f, 0.48f);
        railPaint.SetFloat("_DoubleSidedEnable", 1f);
        stonePaint = MakeMaterial("Tunnel limestone", new Color(0.22f, 0.20f, 0.16f), 0f, 0.3f);
        roofPaint = MakeMaterial("Terracotta roof tiles", new Color(0.36f, 0.095f, 0.045f), 0f, 0.32f);
        glassPaint = MakeMaterial("Village window shadow", new Color(0.015f, 0.055f, 0.075f), 0.25f, 0.75f);
        ochrePaint = MakeMaterial("Ochre plaster", new Color(0.55f, 0.33f, 0.17f), 0f, 0.34f);
        warmWindowPaint = MakeMaterial("Village warm window glow", new Color(0.30f, 0.16f, 0.06f), 0f, 0.7f);
        warmWindowPaint.SetColor("_EmissiveColor", new Color(1f, 0.52f, 0.16f) * 3.2f);
        warmWindowPaint.EnableKeyword("_EMISSION");
    }

    private static List<Vector3> Positions()
    {
        var list = new List<Vector3>(path.Count + 1);
        foreach (var s in path.Samples) list.Add(s.position);
        list.Add(path.At(0).position);
        return list;
    }

    private static void CreateTerrainAndSea()
    {
        Vector3 min = new Vector3(float.MaxValue, 0, float.MaxValue), max = new Vector3(float.MinValue, 0, float.MinValue);
        foreach (var s in path.Samples) { min = Vector3.Min(min, s.position); max = Vector3.Max(max, s.position); }
        min -= new Vector3(220, 0, 220); max += new Vector3(220, 0, 220);
        const int xSteps = 112, zSteps = 112;
        var vertices = new List<Vector3>((xSteps + 1) * (zSteps + 1));
        var uv = new List<Vector2>(); var triangles = new List<int>();
        for (int z = 0; z <= zSteps; z++)
        {
            for (int x = 0; x <= xSteps; x++)
            {
                Vector3 p = new Vector3(Mathf.Lerp(min.x, max.x, x / (float)xSteps), 0, Mathf.Lerp(min.z, max.z, z / (float)zSteps));
                p.y = TerrainHeight(p);
                vertices.Add(p); uv.Add(new Vector2(p.x / 16f, p.z / 16f));
                if (x < xSteps && z < zSteps)
                {
                    int n = z * (xSteps + 1) + x;
                    triangles.Add(n); triangles.Add(n + xSteps + 1); triangles.Add(n + 1);
                    triangles.Add(n + 1); triangles.Add(n + xSteps + 1); triangles.Add(n + xSteps + 2);
                }
            }
        }
        var mesh = new Mesh { name = "Continuous coastal terrain" };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        PersistMesh(mesh);
        var terrain = new GameObject("Coastal terrain / real offroad collision");
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        terrain.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial("Dry grass and coastal soil", Color.white, 0f, 0.22f, CreateCoastalGroundTexture());
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
        var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sea.name = "Mediterranean sea to horizon";
        sea.transform.position = new Vector3(100f, -19f, 350f);
        sea.transform.localScale = new Vector3(650f, 1f, 650f);
        var water = MakeMaterial("Layered Mediterranean water", new Color(0.015f, 0.16f, 0.24f), 0.25f, 0.94f, null, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/AsphaltNormal.jpg"));
        water.SetTextureScale("_NormalMap", new Vector2(55f, 55f));
        water.SetFloat("_NormalScale", 0.22f);
        sea.GetComponent<Renderer>().sharedMaterial = water;
        sea.AddComponent<CoastalWater>();
        UnityEngine.Object.DestroyImmediate(sea.GetComponent<Collider>());
    }

    private static float TerrainHeight(Vector3 p)
    {
        int nearest = path.NearestIndex(p);
        var s = path.At(nearest);
        float distance = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(s.position.x, s.position.z));
        float noise = Mathf.PerlinNoise(p.x * 0.012f, p.z * 0.012f) * 4f + Mathf.PerlinNoise(p.x * 0.035f, p.z * 0.035f) * 1.5f;
        // Broad low-frequency hills break the flat ramp silhouette away from the road.
        float hills = (Mathf.PerlinNoise(p.x * 0.0042f + 3.1f, p.z * 0.0042f + 8.7f) - 0.35f) * 46f
                    + (Mathf.PerlinNoise(p.x * 0.013f + 19f, p.z * 0.013f + 7f) - 0.5f) * 15f;
        float height = s.position.y - 0.45f - Mathf.Max(0f, distance - 8f) * 0.095f
                     + noise * Mathf.InverseLerp(8f, 50f, distance)
                     + hills * Mathf.InverseLerp(55f, 205f, distance);
        if (distance > 110f) height -= (distance - 110f) * 0.14f;
        return Mathf.Max(-24f, height);
    }

    private static void CreateRaceRoad()
    {
        var shoulder = new GameObject("Continuous road shoulder");
        var shoulderMesh = BuildStrip(Positions(), 14.4f, -0.09f);
        shoulder.AddComponent<MeshFilter>().sharedMesh = shoulderMesh;
        shoulder.AddComponent<MeshRenderer>().sharedMaterial = stonePaint;
        shoulder.AddComponent<MeshCollider>().sharedMesh = shoulderMesh;
        var road = new GameObject("Costa Brava / asphalt collision surface");
        var mesh = BuildStrip(Positions(), 12f, 0f);
        road.AddComponent<MeshFilter>().sharedMesh = mesh;
        road.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial("Coast asphalt PBR", new Color(0.66f, 0.68f, 0.71f), 0f, 0.36f,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/AsphaltColor.jpg"), AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/AsphaltNormal.jpg"));
        road.AddComponent<MeshCollider>().sharedMesh = mesh;
        for (int lane = -1; lane <= 1; lane += 2)
        {
            var edge = new GameObject("Continuous white road edge " + lane);
            edge.AddComponent<MeshFilter>().sharedMesh = BuildOffsetStrip(5.55f * lane, 0.14f, 0.025f);
            edge.AddComponent<MeshRenderer>().sharedMaterial = whitePaint;
        }
        float lastDash = -20f;
        for (int i = 0; i < path.Count; i += 2)
        {
            if (path.At(i).distance - lastDash < 10f) continue;
            lastDash = path.At(i).distance;
            var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = "Lane dash";
            dash.transform.SetPositionAndRotation(path.At(i).position + Vector3.up * 0.035f, path.RotationAt(i));
            dash.transform.localScale = new Vector3(0.13f, 0.025f, 4.2f);
            dash.GetComponent<Renderer>().sharedMaterial = whitePaint;
            UnityEngine.Object.DestroyImmediate(dash.GetComponent<Collider>());
        }
        for (float t = 0.27f; t < 0.31f; t += 0.012f)
        {
            int i = path.IndexAtDistance(path.TotalLength * t);
            var wet = new GameObject("Damp asphalt patch");
            var list = new List<Vector3>();
            for (int j = -2; j <= 2; j++) list.Add(path.At(i + j).position);
            wet.AddComponent<MeshFilter>().sharedMesh = BuildStrip(list, 4.2f, 0.018f);
            wet.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial("Wet road " + t, new Color(0.12f, 0.15f, 0.18f), 0.1f, 0.86f);
        }
    }

    private static Mesh BuildOffsetStrip(float lateral, float width, float height)
    {
        var points = new List<Vector3>();
        for (int i = 0; i <= path.Count; i++) points.Add(path.PointAt(i, lateral));
        return BuildStrip(points, width, height);
    }

    private static void CreateGuardrailsAndMarkings()
    {
        for (int side = -1; side <= 1; side += 2)
        {
            var rail = new GameObject("Continuous safety rail " + side);
            rail.AddComponent<MeshFilter>().sharedMesh = BuildRailMesh(side * 7f);
            rail.AddComponent<MeshRenderer>().sharedMaterial = railPaint;
            rail.AddComponent<MeshCollider>().sharedMesh = rail.GetComponent<MeshFilter>().sharedMesh;
            for (int i = 0; i < path.Count; i += 4)
            {
                if (IsVillage(path.At(i).distance / path.TotalLength)) continue;
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Guardrail post";
                post.transform.SetPositionAndRotation(path.PointAt(i, side * 7f) + Vector3.up * 0.45f, path.RotationAt(i));
                post.transform.localScale = new Vector3(0.12f, 0.95f, 0.12f);
                post.GetComponent<Renderer>().sharedMaterial = railPaint;
            }
        }
        for (int i = 0; i < path.Count; i++)
        {
            float curve = path.Curvature(i, 45f);
            if (curve < 0.12f || i % 3 != 0) continue;
            var s = path.At(i);
            float side = Vector3.Cross(s.tangent, Vector3.up).y > 0 ? 1f : -1f;
            Vector3 next = path.At(path.IndexAhead(i, 25f)).tangent;
            side = Vector3.Dot(Vector3.Cross(s.tangent, next), Vector3.up) > 0 ? 1f : -1f;
            for (int stripe = -1; stripe <= 1; stripe++)
            {
                var curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                curb.name = "Hairpin red white curb";
                curb.transform.SetPositionAndRotation(path.PointAt(i + stripe, side * 5.9f) + Vector3.up * 0.06f, path.RotationAt(i + stripe));
                curb.transform.localScale = new Vector3(0.55f, 0.1f, 3.2f);
                curb.GetComponent<Renderer>().sharedMaterial = stripe % 2 == 0 ? whitePaint : MakeMaterial("Curb red", new Color(0.58f, 0.025f, 0.025f), 0f, 0.4f);
                UnityEngine.Object.DestroyImmediate(curb.GetComponent<Collider>());
            }
        }
    }

    private static Mesh BuildRailMesh(float lateral)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= path.Count; i++)
        {
            Vector3 p = path.PointAt(i, lateral);
            vertices.Add(p + Vector3.up * 0.55f); vertices.Add(p + Vector3.up * 0.95f);
            if (i < path.Count)
            {
                int n = i * 2; triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1); triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n + 3);
            }
        }
        var mesh = new Mesh { name = "Guardrail ribbon" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return PersistMesh(mesh);
    }

    private static void CreateTunnel()
    {
        int start = path.IndexAtDistance(path.TotalLength * 0.405f), end = path.IndexAtDistance(path.TotalLength * 0.452f);
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        int rings = 0;
        for (int i = start; i != path.Wrap(end + 1); i = path.Wrap(i + 1))
        {
            for (int j = 0; j <= 10; j++)
            {
                float angle = Mathf.PI * j / 10f;
                vertices.Add(path.PointAt(i) + path.At(i).side * Mathf.Cos(angle) * 8f + Vector3.up * (0.3f + Mathf.Sin(angle) * 7f));
            }
            rings++;
            if (i == end) break;
        }
        for (int ring = 0; ring < rings - 1; ring++)
        {
            for (int j = 0; j < 10; j++)
            {
                int n = ring * 11 + j;
                triangles.Add(n); triangles.Add(n + 11); triangles.Add(n + 1);
                triangles.Add(n + 1); triangles.Add(n + 11); triangles.Add(n + 12);
            }
        }
        var tunnel = new GameObject("Pedra Seca / short stone tunnel");
        var mesh = new Mesh { name = "Open tunnel vault" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        PersistMesh(mesh);
        tunnel.AddComponent<MeshFilter>().sharedMesh = mesh;
        var material = stonePaint; material.SetFloat("_DoubleSidedEnable", 1f);
        tunnel.AddComponent<MeshRenderer>().sharedMaterial = material;
        tunnel.AddComponent<MeshCollider>().sharedMesh = mesh;
        // Lamps advance by track distance, not by index equality: stepping by 8
        // samples can never land exactly on `end` and would loop forever.
        float tunnelStart = path.TotalLength * 0.405f;
        float tunnelLength = path.TotalLength * (0.452f - 0.405f);
        for (float d = 0f; d < tunnelLength; d += 30f)
        {
            int i = path.IndexAtDistance(tunnelStart + d);
            var lamp = new GameObject("Tunnel warm ceiling light");
            lamp.transform.position = path.PointAt(i) + Vector3.up * 5.8f;
            var light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1f, 0.62f, 0.28f); light.range = 18f; light.intensity = 7f;
            var fixture = GameObject.CreatePrimitive(PrimitiveType.Cube); fixture.transform.SetParent(lamp.transform, false); fixture.transform.localScale = new Vector3(1.2f, 0.12f, 0.35f); fixture.GetComponent<Renderer>().sharedMaterial = whitePaint;
        }
    }

    private static void CreateJumpAndShortcut()
    {
        int jump = path.IndexAtDistance(path.TotalLength * 0.115f);
        var s = path.At(jump);
        Vector3 side = s.side, forward = s.tangent, origin = s.position + side * 2.8f;
        var vertices = new[]
        {
            origin - side * 2f - forward * 8f, origin + side * 2f - forward * 8f, origin - side * 2f + forward * 8f + Vector3.up * 2.1f, origin + side * 2f + forward * 8f + Vector3.up * 2.1f,
            origin - side * 2f - forward * 8f - Vector3.up * 0.2f, origin + side * 2f - forward * 8f - Vector3.up * 0.2f
        };
        var mesh = new Mesh { name = "Stable optional jump wedge", vertices = vertices, triangles = new[] { 0, 2, 1, 1, 2, 3, 0, 1, 4, 1, 5, 4, 0, 4, 2, 1, 3, 5, 2, 4, 3, 3, 4, 5 } };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        PersistMesh(mesh);
        var ramp = new GameObject("Optional jump / normal lane remains open");
        ramp.AddComponent<MeshFilter>().sharedMesh = mesh; ramp.AddComponent<MeshRenderer>().sharedMaterial = darkPaint; ramp.AddComponent<MeshCollider>().sharedMesh = mesh;
        CreateSign(path.PointAt(jump - 8, 3.5f) + Vector3.up * 2.2f, path.RotationAt(jump), "JUMP  >", new Color(0.1f, 0.75f, 1f));
        int a = path.IndexAtDistance(path.TotalLength * 0.806f), b = path.IndexAtDistance(path.TotalLength * 0.886f);
        Vector3 pa = path.At(a).position, pb = path.At(b).position;
        Vector3 middle = (pa + pb) * 0.5f + Vector3.up * 2f;
        var points = new List<Vector3>();
        for (int i = 0; i <= 36; i++)
        {
            float t = i / 36f;
            points.Add((1 - t) * (1 - t) * pa + 2 * (1 - t) * t * middle + t * t * pb);
        }
        var shortcut = new GameObject("Optional gravel shortcut / rejoins before checkpoint");
        var shortcutMesh = BuildStrip(points, 6.5f, 0.08f);
        shortcut.AddComponent<MeshFilter>().sharedMesh = shortcutMesh;
        shortcut.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial("Compacted gravel shortcut", new Color(0.32f, 0.27f, 0.17f), 0f, 0.2f);
        shortcut.AddComponent<MeshCollider>().sharedMesh = shortcutMesh;
        CreateSign(pa + Vector3.up * 2.4f, path.RotationAt(a), "SHORTCUT", new Color(1f, 0.66f, 0.18f));
    }

    private static bool IsVillage(float t)
    {
        foreach (var span in VillageSpans) if (t >= span[0] && t <= span[1]) return true;
        return false;
    }

    /// <summary>Cube batcher: hundreds of houses collapse into a handful of
    /// draw calls. CombineMeshes keeps Unity's built-in cube winding/normals.</summary>
    private sealed class BoxBatch
    {
        private static Mesh cube;
        private readonly Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
        private static Mesh Cube
        {
            get
            {
                if (!cube)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube = go.GetComponent<MeshFilter>().sharedMesh;
                    UnityEngine.Object.DestroyImmediate(go);
                }
                return cube;
            }
        }
        public void Add(Material mat, Vector3 center, Vector3 size, Quaternion rotation)
        {
            if (!mat) return;
            List<CombineInstance> list;
            if (!groups.TryGetValue(mat, out list)) { list = new List<CombineInstance>(); groups[mat] = list; }
            list.Add(new CombineInstance { mesh = Cube, transform = Matrix4x4.TRS(center, rotation, size) });
        }
        public int Flush(string label)
        {
            int objects = 0;
            foreach (var pair in groups)
            {
                if (pair.Value.Count == 0) continue;
                var mesh = new Mesh { name = label + " " + pair.Key.name };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                PersistMesh(mesh);
                var go = new GameObject(label + " / " + pair.Key.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = pair.Key;
                objects++;
            }
            return objects;
        }
    }

    private static void CreateVillage()
    {
        var random = new System.Random(41);
        var batch = new BoxBatch();
        int[] counts = { 22, 20, 42, 24 };
        int houses = 0;
        for (int cluster = 0; cluster < VillageSpans.Length; cluster++)
        {
            float a = VillageSpans[cluster][0], b = VillageSpans[cluster][1];
            for (int i = 0; i < counts[cluster]; i++, houses++)
            {
                float t = Mathf.Lerp(a, b, (i + 0.35f + (float)random.NextDouble() * 0.3f) / counts[cluster]);
                int index = path.IndexAtDistance(path.TotalLength * t);
                float side = (i % 2 == 0 ? -1f : 1f) * Mathf.Lerp(19f, 62f, (float)random.NextDouble());
                Vector3 p = path.PointAt(index, side);
                p.y = TerrainHeight(p) - 1.1f; // sink the base into the hill mesh
                Quaternion rot = Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(path.At(index).position - p, Vector3.up).normalized, Vector3.up);
                float width = 6.5f + (float)random.NextDouble() * 6.5f;
                float depth = 6.5f + (float)random.NextDouble() * 5f;
                float height = 4.8f + (float)random.NextDouble() * 5.5f;
                Material wall = houses % 4 == 0 ? ochrePaint : whitePaint;
                batch.Add(wall, p + rot * new Vector3(0, height * 0.5f, 0), new Vector3(width, height, depth), rot);
                batch.Add(roofPaint, p + rot * new Vector3(0, height + 0.3f, 0), new Vector3(width + 0.6f, 0.6f, depth + 0.6f), rot);
                int floors = Mathf.Clamp(Mathf.FloorToInt(height / 2.6f), 1, 3);
                for (int floor = 0; floor < floors; floor++)
                {
                    for (int w = -1; w <= 1; w += 2)
                    {
                        Material window = random.NextDouble() < 0.28 ? warmWindowPaint : glassPaint;
                        Vector3 local = new Vector3(w * width * 0.26f, 1.6f + floor * 2.45f, depth * 0.5f + 0.04f);
                        batch.Add(window, p + rot * local, new Vector3(1.05f, 1.3f, 0.08f), rot);
                    }
                }
            }
        }
        int renderers = batch.Flush("Mediterranean coastal village");
        Debug.Log("AURORA_STEP village houses=" + houses + " merged_renderers=" + renderers);
    }

    private static void CreateVegetationAndRocks()
    {
        var tree = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vegetation/IslandTree.fbx");
        var rocks = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vegetation/CoastRocks.fbx");
        var shrub = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vegetation/Shrub.fbx");
        var random = new System.Random(73);
        for (int i = 0; i < 72; i++)
        {
            int index = path.Wrap(i * 11 + 17);
            float side = i % 2 == 0 ? -1f : 1f;
            float lane = side * (17f + (float)random.NextDouble() * 38f);
            Vector3 p = path.PointAt(index, lane); p.y = TerrainHeight(p);
            if (tree && i % 3 != 0)
            {
                var instance = UnityEngine.Object.Instantiate(tree, p, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                instance.name = "Poly Haven island tree"; instance.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.18f, (float)random.NextDouble());
            }
            else CreateProceduralTree(p, 0.8f + (float)random.NextDouble() * 0.5f);
            if (shrub && i % 2 == 0)
            {
                var instance = UnityEngine.Object.Instantiate(shrub, p + path.At(index).side * 3f, Quaternion.Euler(0f, i * 29f, 0f));
                instance.name = "Poly Haven coastal shrub";
            }
        }
        for (int i = 0; i < 32; i++)
        {
            int index = path.Wrap(i * 19 + 5);
            float lane = (i % 2 == 0 ? -1f : 1f) * (10f + (float)random.NextDouble() * 32f);
            Vector3 p = path.PointAt(index, lane); p.y = TerrainHeight(p);
            if (rocks)
            {
                var rock = UnityEngine.Object.Instantiate(rocks, p, Quaternion.Euler(0f, i * 43f, 0f));
                rock.name = "Poly Haven scanned coastal rock";
                rock.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.25f, (float)random.NextDouble());
            }
        }
    }

    private static void CreateProceduralTree(Vector3 position, float scale)
    {
        var tree = new GameObject("Mediterranean stone pine fallback"); tree.transform.position = position; tree.transform.localScale = Vector3.one * scale;
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); trunk.transform.SetParent(tree.transform, false); trunk.transform.localPosition = new Vector3(0, 3.5f, 0); trunk.transform.localScale = new Vector3(0.45f, 3.5f, 0.45f); trunk.GetComponent<Renderer>().sharedMaterial = stonePaint;
        var leaves = MakeMaterial("Pine canopy", new Color(0.07f, 0.16f, 0.065f), 0f, 0.22f);
        for (int i = 0; i < 4; i++)
        {
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere); crown.transform.SetParent(tree.transform, false); crown.transform.localPosition = new Vector3(Mathf.Sin(i * 2.1f) * 1.6f, 7f + (i % 2), Mathf.Cos(i * 2.1f) * 1.6f); crown.transform.localScale = new Vector3(4.5f, 1.8f, 4f); crown.GetComponent<Renderer>().sharedMaterial = leaves;
        }
    }

    private static void CreateDistantLandscape()
    {
        // Three continuous serrated ridge rings replace the 15 smooth spheres that
        // read as black domes. Noise is sampled on the unit circle so each ring
        // closes seamlessly; three tints fake aerial perspective into the fog.
        Vector3 center = new Vector3(100f, 0f, 350f);
        Color[] tints =
        {
            new Color(0.17f, 0.22f, 0.17f),
            new Color(0.25f, 0.28f, 0.31f),
            new Color(0.36f, 0.36f, 0.43f)
        };
        const int segments = 160;
        for (int layer = 0; layer < 3; layer++)
        {
            float radius = 1250f + layer * 430f;
            float spread = 300f + layer * 70f;
            float peak = 230f + layer * 75f;
            var vertices = new List<Vector3>((segments + 1) * 3);
            var uv = new List<Vector2>((segments + 1) * 3);
            var triangles = new List<int>(segments * 12);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                float n1 = Mathf.PerlinNoise(ca * 2.3f + layer * 11.3f + 21f, sa * 2.3f + layer * 5.7f + 21f);
                float n2 = Mathf.PerlinNoise(ca * 6.7f + layer * 3.1f + 7f, sa * 6.7f + layer * 9.2f + 7f);
                float n3 = Mathf.PerlinNoise(ca * 17f + layer * 2.2f + 41f, sa * 17f + layer * 7.9f + 41f);
                float ridge = Mathf.Pow(1f - Mathf.Abs(n1 * 2f - 1f), 1.7f);
                float h = -26f + peak * (0.26f + 0.50f * ridge + 0.18f * n2 + 0.06f * n3);
                Vector3 dir = new Vector3(ca, 0f, sa);
                vertices.Add(center + dir * (radius - spread) + Vector3.down * 28f);
                vertices.Add(center + dir * radius + Vector3.up * h);
                vertices.Add(center + dir * (radius + spread) + Vector3.down * 28f);
                uv.Add(new Vector2(i / (float)segments * 12f, 0f));
                uv.Add(new Vector2(i / (float)segments * 12f, 1f));
                uv.Add(new Vector2(i / (float)segments * 12f, 2f));
            }
            for (int i = 0; i < segments; i++)
            {
                int n = i * 3;
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 3);
                triangles.Add(n + 1); triangles.Add(n + 4); triangles.Add(n + 3);
                triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n + 4);
                triangles.Add(n + 2); triangles.Add(n + 5); triangles.Add(n + 4);
            }
            var mesh = new Mesh { name = "Layered coastal ridge " + layer };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            PersistMesh(mesh);
            var mat = MakeMaterial("Distant sage ridge " + layer, tints[layer], 0f, 0.12f);
            mat.SetFloat("_DoubleSidedEnable", 1f);
            // A little emissive lift keeps the back-lit ridges from reading as a
            // solid black cut-out; the outer layers are brighter/bluer (aerial haze).
            mat.SetColor("_EmissiveColor", tints[layer] * (1.3f + layer * 0.5f));
            mat.EnableKeyword("_EMISSION");
            var ridgeObject = new GameObject("Layered coastal mountain ridge " + layer);
            ridgeObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            ridgeObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }

    private static void CreateStartAndRoadSigns()
    {
        var start = path.At(0);
        for (int side = -1; side <= 1; side += 2)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube); pillar.name = "Start gantry pillar"; pillar.transform.SetPositionAndRotation(start.position + start.side * side * 7f + Vector3.up * 3.5f, path.RotationAt(0)); pillar.transform.localScale = new Vector3(0.45f, 7f, 0.45f); pillar.GetComponent<Renderer>().sharedMaterial = darkPaint;
        }
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube); beam.name = "AURORA start gantry"; beam.transform.SetPositionAndRotation(start.position + Vector3.up * 6.8f, path.RotationAt(0)); beam.transform.localScale = new Vector3(14.5f, 1.25f, 0.5f); beam.GetComponent<Renderer>().sharedMaterial = darkPaint;
        var text = new GameObject("Start gantry title"); text.transform.SetPositionAndRotation(start.position + Vector3.up * 6.8f - start.tangent * 0.28f, Quaternion.LookRotation(-start.tangent, Vector3.up));
        var mesh = text.AddComponent<TextMesh>(); mesh.text = "A U R O R A  /  C O S T A  B R A V A"; mesh.fontSize = 56; mesh.characterSize = 0.12f; mesh.anchor = TextAnchor.MiddleCenter; mesh.color = new Color(1f, 0.65f, 0.24f);
        int last = -200;
        for (int i = 0; i < path.Count; i++)
        {
            if (path.Curvature(i, 55f) < 0.105f || i - last < 35) continue;
            last = i;
            CreateSign(path.PointAt(i - 18, -8f) + Vector3.up * 2.1f, path.RotationAt(i - 18), "150", Color.white);
            CreateSign(path.PointAt(i - 10, -8f) + Vector3.up * 2.1f, path.RotationAt(i - 10), "100", Color.white);
            CreateSign(path.PointAt(i - 4, -8f) + Vector3.up * 2.1f, path.RotationAt(i - 4), "50", Color.white);
        }
    }

    private static void CreateSign(Vector3 position, Quaternion rotation, string text, Color color)
    {
        var sign = new GameObject("Road sign " + text); sign.transform.SetPositionAndRotation(position, rotation);
        var board = GameObject.CreatePrimitive(PrimitiveType.Cube); board.transform.SetParent(sign.transform, false); board.transform.localScale = new Vector3(1.5f, 1.1f, 0.12f); board.GetComponent<Renderer>().sharedMaterial = darkPaint;
        var label = new GameObject("Sign legend"); label.transform.SetParent(sign.transform, false); label.transform.localPosition = new Vector3(0, 0, -0.07f); label.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        var mesh = label.AddComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 56; mesh.characterSize = 0.018f; mesh.anchor = TextAnchor.MiddleCenter; mesh.color = color;
    }

    private static ArcadeCarController[] CreateRaceGrid()
    {
        var cars = new ArcadeCarController[4];
        int[] indices = { 5, 1, -3, -7 };
        float[] lanes = { -1.7f, 1.7f, -1.7f, 1.7f };
        float[] skills = { 0.72f, 0.47f, 0.62f, 0.78f };
        // The player gets the full switchable garage; rivals are three distinct
        // classic bodies from the same CC0 / CC BY catalogue, so the grid reads
        // as a mixed field instead of four copies of one car.
        int[] rivalSpecs =
        {
            VehicleCatalog.IndexOf("hdSportsCar"),
            VehicleCatalog.IndexOf("hdMuscleCoupe"),
            VehicleCatalog.IndexOf("hdTouringCoupe")
        };
        for (int i = 0; i < 4; i++)
        {
            int index = path.Wrap(indices[i]);
            int[] specs;
            if (i == 0)
            {
                specs = new int[VehicleCatalog.Cars.Length];
                for (int k = 0; k < specs.Length; k++) specs[k] = k;
            }
            else specs = new[] { rivalSpecs[i - 1] };
            var carObject = CreateCar(path.PointAt(index, lanes[i]) + Vector3.up * 0.5f, path.RotationAt(index),
                i == 0 ? "GARAGE / PLAYER" : "RIVAL " + i + " / " + VehicleCatalog.Cars[specs[0]].id, specs);
            var car = carObject.GetComponent<ArcadeCarController>();
            car.track = path; car.trackIndex = index;
            var driver = carObject.AddComponent<RivalDriver>(); driver.track = path; driver.race = null; driver.skill = skills[i]; driver.preferredLane = lanes[i] * 0.5f;
            if (i == 0) { driver.enabled = false; car.aiControlled = false; }
            cars[i] = car;
        }
        return cars;
    }

    private static void AddAudio(ArcadeCarController car)
    {
        var audio = car.gameObject.AddComponent<VehicleAudio>(); audio.car = car;
        var clips = new AudioClip[6];
        for (int i = 0; i < 6; i++) clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Engine" + i + ".wav");
        audio.engineLoops = clips;
        audio.skidClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/TireSkid.wav");
        audio.windClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/WindLoop.ogg");
        audio.boostStart = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BoostStart.ogg");
        audio.boostLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BoostLoop.ogg");
        audio.boostEnd = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BoostEnd.ogg");
        audio.impactClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Impact.wav");
    }

    private static void CreateAmbientAudio()
    {
        var ambient = new GameObject("Mediterranean ambience");
        var source = ambient.AddComponent<AudioSource>(); source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/OceanWaves.flac"); source.loop = true; source.volume = 0.2f; source.spatialBlend = 0f; source.playOnAwake = true;
    }

    private static void BuildRaceWindows()
    {
        string directory = Path.GetFullPath("build/Windows"); Directory.CreateDirectory(directory); Directory.CreateDirectory("tests/race");
        var options = new BuildPlayerOptions { scenes = new[] { RaceScenePath }, locationPathName = Path.Combine(directory, "AuroraCoast.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        string result = $"result={report.summary.result}\nerrors={report.summary.totalErrors}\nwarnings={report.summary.totalWarnings}\nsize={report.summary.totalSize}\noutput={report.summary.outputPath}\n";
        File.WriteAllText("tests/race/build_result.txt", result);
        Debug.Log("AURORA_RACE_BUILD_RESULT " + result);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 3);
    }
}
