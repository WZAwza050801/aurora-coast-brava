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
    private const string Root = "Assets/";
    private const string ScenePath = "Assets/Scenes/DrivingLab.unity";

    [MenuItem("Aurora/Build Minimal Driving Lab")]
    public static void BuildMinimalAndWindows()
    {
        try
        {
            ConfigureAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureRendering();
            CreateLighting();
            CreateGround();
            CreateRoad();
            var car = CreateCar(new Vector3(0f, 0.45f, 4f), Quaternion.identity, "Aurora GT - Player");
            var camera = CreateCamera(car);
            var hud = new GameObject("HUD and smoke test");
            hud.AddComponent<MinimalHud>().car = car.GetComponent<ArcadeCarController>();
            var smoke = hud.AddComponent<MinimalSmokeTest>();
            smoke.car = car.GetComponent<ArcadeCarController>();
            smoke.chaseCamera = camera.GetComponent<ChaseCamera>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            BuildWindows("AuroraDrivingLab");
        }
        catch (Exception ex)
        {
            Debug.LogError("AURORA_BUILD_FAILED " + ex);
            if (Application.isBatchMode) EditorApplication.Exit(2);
        }
    }

    public static void ConfigureAssets()
    {
        Directory.CreateDirectory("Assets/Materials");
        Directory.CreateDirectory("Assets/Rendering");
        Directory.CreateDirectory("Assets/Scenes");
        // GeneratedMeshes is rebuilt from scratch on every build. PersistMesh()
        // uses GenerateUniqueAssetPath(), so without this cleanup the folder
        // accumulates ~150 new .asset files per build. After ~10 builds that
        // churn produced a serialized level0 with BuildReport warnings=1 and
        // the player then crashed at startup with "level0 is corrupted /
        // Position out of bounds" (verified 2026-11-15). Clearing here keeps
        // every rebuild deterministic and small.
        Directory.CreateDirectory("Assets/GeneratedMeshes");
        foreach (string stale in Directory.GetFiles("Assets/GeneratedMeshes", "*.asset"))
            AssetDatabase.DeleteAsset(stale.Replace('\\', '/'));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string hdr = "Assets/Environment/VeniceSunset.hdr";
        if (AssetImporter.GetAtPath(hdr) is TextureImporter hdrImporter)
        {
            hdrImporter.textureShape = TextureImporterShape.TextureCube;
            hdrImporter.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            hdrImporter.mipmapEnabled = true;
            hdrImporter.sRGBTexture = false;
            hdrImporter.SaveAndReimport();
        }
        string normal = "Assets/Textures/AsphaltNormal.jpg";
        if (AssetImporter.GetAtPath(normal) is TextureImporter normalImporter)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }
        string fbx = "Assets/Vehicles/AuroraGT.fbx";
        if (AssetImporter.GetAtPath(fbx) is ModelImporter model)
        {
            model.globalScale = 1f;
            model.importNormals = ModelImporterNormals.Import;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.SaveAndReimport();
        }
        PlayerSettings.companyName = "Ember Roadworks";
        PlayerSettings.productName = "AURORA - Costa Brava";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
    }

    public static void ConfigureRendering()
    {
        const string path = "Assets/Rendering/AuroraHDRP.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
        if (!pipeline)
        {
            pipeline = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
            AssetDatabase.CreateAsset(pipeline, path);
        }
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        QualitySettings.vSyncCount = 0;
        Physics.gravity = new Vector3(0f, -18f, 0f);
        Time.fixedDeltaTime = 1f / 60f;
    }

    public static Material MakeMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.5f, Texture colorMap = null, Texture normalMap = null)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (colorMap) mat.SetTexture("_BaseColorMap", colorMap);
        if (normalMap) mat.SetTexture("_NormalMap", normalMap);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static void CreateLighting()
    {
        // A low sun (8.5 degrees above the horizon) gives long raking shadows;
        // the cool fill keeps the shadow side blue instead of flat black.
        var sun = new GameObject("Golden hour sun");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.56f, 0.28f);
        light.intensity = 34000f; // HDRP directional lights are measured in lux.
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(8.5f, -34f, 0f);
        var hd = sun.AddComponent<HDAdditionalLightData>();
        hd.angularDiameter = 2.2f;
        hd.shadowUpdateMode = ShadowUpdateMode.EveryFrame;

        var fillObject = new GameObject("Cool Atlantic sky fill");
        var fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.40f, 0.56f, 0.92f);
        fill.intensity = 4300f;
        fill.shadows = LightShadows.None;
        fillObject.transform.rotation = Quaternion.Euler(42f, 146f, 0f);
        fillObject.AddComponent<HDAdditionalLightData>();

        // A visible emissive disc in the sun direction; bloom turns it into a
        // sunset glow even when the HDRI's own sun sits elsewhere in the sky.
        var disc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        disc.name = "Golden hour sun disc";
        disc.transform.position = new Vector3(100f, 90f, 350f) - sun.transform.forward * 1750f;
        disc.transform.localScale = Vector3.one * 120f;
        var discMat = MakeMaterial("Golden hour sun disc emissive", new Color(1f, 0.55f, 0.24f), 0f, 1f);
        discMat.SetColor("_EmissiveColor", new Color(1f, 0.42f, 0.12f) * 14f);
        discMat.EnableKeyword("_EMISSION");
        disc.GetComponent<Renderer>().sharedMaterial = discMat;
        UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());

        var volumeObject = new GameObject("Golden hour HDR environment");
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        AssetDatabase.DeleteAsset("Assets/Rendering/GoldenHourVolume.asset");
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, "Assets/Rendering/GoldenHourVolume.asset");
        volume.sharedProfile = profile;
        var environment = profile.Add<VisualEnvironment>();
        environment.skyType.overrideState = true;
        environment.skyType.value = (int)SkyType.HDRI;
        var sky = profile.Add<HDRISky>();
        sky.hdriSky.overrideState = true;
        sky.hdriSky.value = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Environment/VeniceSunset.hdr");
        sky.exposure.overrideState = true;
        sky.exposure.value = 0.32f;
        sky.rotation.overrideState = true;
        sky.rotation.value = 125f;
        var exposure = profile.Add<Exposure>();
        exposure.mode.overrideState = true;
        exposure.mode.value = ExposureMode.Fixed;
        exposure.fixedExposure.overrideState = true;
        exposure.fixedExposure.value = 11.2f;
        var tone = profile.Add<Tonemapping>();
        tone.mode.overrideState = true;
        tone.mode.value = TonemappingMode.ACES;
        var bloom = profile.Add<Bloom>();
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.85f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.34f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.68f;
        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(1f, 0.82f, 0.60f);
        var fog = profile.Add<Fog>();
        fog.enabled.overrideState = true;
        fog.enabled.value = true;
        fog.meanFreePath.overrideState = true;
        fog.meanFreePath.value = 620f;
        fog.baseHeight.overrideState = true;
        fog.baseHeight.value = -28f;
        fog.maximumHeight.overrideState = true;
        fog.maximumHeight.value = 420f;
        fog.maxFogDistance.overrideState = true;
        fog.maxFogDistance.value = 2600f;
        fog.tint.overrideState = true;
        fog.tint.value = new Color(1f, 0.63f, 0.36f);
        fog.albedo.overrideState = true;
        fog.albedo.value = new Color(0.72f, 0.46f, 0.30f);
        var motionBlur = profile.Add<MotionBlur>();
        motionBlur.intensity.overrideState = true;
        motionBlur.intensity.value = 0f;
        var color = profile.Add<ColorAdjustments>();
        color.postExposure.overrideState = true;
        color.postExposure.value = 0.28f;
        color.contrast.overrideState = true;
        color.contrast.value = 10f;
        color.colorFilter.overrideState = true;
        color.colorFilter.value = new Color(1f, 0.87f, 0.72f);
        color.saturation.overrideState = true;
        color.saturation.value = 12f;
    }

    /// <summary>Procedural, tileable ground albedo so the coastal terrain is no
    /// longer a single flat colour. Stored as a real asset for player builds.</summary>
    public static Texture2D CreateCoastalGroundTexture()
    {
        const string path = "Assets/Textures/CoastalGround.asset";
        const int size = 256;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (!tex)
        {
            tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            AssetDatabase.CreateAsset(tex, path);
        }
        tex.name = "Coastal dry grass and soil";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 4;
        var pixels = new Color[size * size];
        // Wider separation so the albedo actually reads at driving distance.
        Color grass = new Color(0.18f, 0.23f, 0.09f);
        Color soil = new Color(0.46f, 0.25f, 0.11f);
        Color stone = new Color(0.35f, 0.34f, 0.30f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size * 7f, v = y / (float)size * 7f;
                float macro = Mathf.PerlinNoise(u * 0.65f, v * 0.65f);
                float meso = Mathf.PerlinNoise(u * 2.1f + 13.7f, v * 2.1f + 7.3f);
                float fine = Mathf.PerlinNoise(u * 7.5f + 31.1f, v * 7.5f + 17.9f);
                float blend = Mathf.Clamp01(0.46f * macro + 0.36f * meso + 0.18f * fine);
                Color c = Color.Lerp(grass, soil, Mathf.SmoothStep(0.26f, 0.68f, blend));
                c = Color.Lerp(c, stone, Mathf.SmoothStep(0.68f, 0.95f, blend) * 0.65f);
                c *= 0.78f + fine * 0.44f;
                c.a = 1f;
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(true, false);
        EditorUtility.SetDirty(tex);
        return tex;
    }

    public static void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Mediterranean ground";
        ground.transform.position = new Vector3(30f, -0.22f, 110f);
        ground.transform.localScale = new Vector3(90f, 1f, 90f);
        ground.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Dry coastal grass", new Color(0.18f, 0.23f, 0.105f), 0f, 0.2f);
    }

    public static List<Vector3> SampleLine(Vector3[] controls, int steps)
    {
        var points = new List<Vector3>();
        for (int i = 0; i < controls.Length - 1; i++)
        {
            Vector3 p0 = controls[Mathf.Max(0, i - 1)];
            Vector3 p1 = controls[i];
            Vector3 p2 = controls[i + 1];
            Vector3 p3 = controls[Mathf.Min(controls.Length - 1, i + 2)];
            for (int j = 0; j < steps; j++)
            {
                float t = j / (float)steps;
                points.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
            }
        }
        points.Add(controls[controls.Length - 1]);
        return points;
    }

    public static void CreateRoad()
    {
        var points = SampleLine(new[] { new Vector3(0, 0, -25), new Vector3(0, 0, 60), new Vector3(0, 1, 110), new Vector3(18, 5, 155), new Vector3(55, 7, 185), new Vector3(100, 5, 205), new Vector3(150, 1, 220) }, 16);
        var road = new GameObject("Test road - straight hill and bend");
        var mesh = BuildStrip(points, 11f, 0f);
        road.AddComponent<MeshFilter>().sharedMesh = mesh;
        road.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial("Asphalt 02 PBR", new Color(0.65f, 0.67f, 0.7f), 0f, 0.32f,
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/AsphaltColor.jpg"), AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/AsphaltNormal.jpg"));
        road.AddComponent<MeshCollider>().sharedMesh = mesh;
        var lineMat = MakeMaterial("Warm lane paint", new Color(0.92f, 0.78f, 0.46f), 0f, 0.4f);
        float distance = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            distance += Vector3.Distance(points[i - 1], points[i]);
            if (distance < 9f) continue;
            distance = 0f;
            Vector3 tangent = (points[i] - points[i - 1]).normalized;
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Center dash";
            stripe.transform.position = (points[i] + points[i - 1]) * 0.5f + Vector3.up * 0.025f;
            stripe.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            stripe.transform.localScale = new Vector3(0.14f, 0.025f, 3.6f);
            stripe.GetComponent<Renderer>().sharedMaterial = lineMat;
            UnityEngine.Object.DestroyImmediate(stripe.GetComponent<Collider>());
        }
    }

    public static Mesh BuildStrip(List<Vector3> points, float width, float yOffset)
    {
        if (points == null || points.Count < 2) throw new ArgumentException("BuildStrip requires at least two points");
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        float length = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0) length += Vector3.Distance(points[i - 1], points[i]);
            Vector3 tangent = i == points.Count - 1 ? (points[i] - points[i - 1]).normalized : (points[i + 1] - points[Mathf.Max(0, i - 1)]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
            vertices.Add(points[i] - side * width * 0.5f + Vector3.up * yOffset);
            vertices.Add(points[i] + side * width * 0.5f + Vector3.up * yOffset);
            uv.Add(new Vector2(0f, length / 6f)); uv.Add(new Vector2(width / 6f, length / 6f));
            if (i < points.Count - 1)
            {
                int n = i * 2;
                triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1);
                triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n + 3);
            }
        }
        var mesh = new Mesh { name = "Continuous asphalt ribbon" };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return PersistMesh(mesh);
    }

    public static Mesh PersistMesh(Mesh mesh)
    {
        int[] indices = mesh.triangles;
        for (int i = 0; i < indices.Length; i++)
            if (indices[i] < 0 || indices[i] >= mesh.vertexCount)
                throw new InvalidDataException(mesh.name + " contains an out-of-range triangle index: " + indices[i]);
        Directory.CreateDirectory("Assets/GeneratedMeshes");
        string safeName = string.Join("_", mesh.name.Split(Path.GetInvalidFileNameChars()));
        string assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/GeneratedMeshes/" + safeName + ".asset");
        AssetDatabase.CreateAsset(mesh, assetPath);
        return mesh;
    }

    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return root;
        foreach (Transform child in root)
        {
            var found = FindDeep(child, name);
            if (found) return found;
        }
        return null;
    }

    /// <summary>Default single-car build (used by the driving lab).</summary>
    public static GameObject CreateCar(Vector3 position, Quaternion rotation, string name)
    {
        return CreateCar(position, rotation, name, null);
    }

    /// <summary>
    /// Builds one physics rig plus one or more switchable body variants.
    /// `specIndices` indexes VehicleCatalog.Cars: index 0 is the initially
    /// selected body; rivals pass a single index. Physics (rigidbody, box,
    /// WheelColliders, nitro) is shared by every variant so switching cars can
    /// never change race fairness or the validated handling.
    /// </summary>
    public static GameObject CreateCar(Vector3 position, Quaternion rotation, string name, int[] specIndices)
    {
        var root = new GameObject(name);
        root.transform.SetPositionAndRotation(position, rotation);
        var body = root.AddComponent<Rigidbody>();
        body.mass = 1350f;
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.65f, 0f);
        box.size = new Vector3(1.82f, 0.68f, 4.25f);

        // Shared HDRP material set. Paint is per-variant so each classic car
        // keeps its own colour; MakeMaterial caches .mat files by name.
        var alloy = MakeMaterial("Polished forged aluminium", new Color(0.74f, 0.79f, 0.86f), 0.95f, 0.88f);
        var rubber = MakeMaterial("Performance rubber", new Color(0.012f, 0.015f, 0.02f), 0f, 0.22f);
        var glass = MakeMaterial("Smoked glass", new Color(0.025f, 0.055f, 0.075f), 0.25f, 0.94f);
        var carbon = MakeMaterial("Satin carbon", new Color(0.015f, 0.018f, 0.024f), 0.35f, 0.42f);
        var tail = MakeMaterial("Tail lamp red", new Color(0.38f, 0.005f, 0.008f), 0.2f, 0.8f);
        tail.SetColor("_EmissiveColor", new Color(1f, 0.015f, 0.005f) * 4.2f);
        tail.EnableKeyword("_EMISSION");
        var lightBar = MakeMaterial("Aurora rear LED bar", new Color(0.35f, 0.01f, 0.012f), 0.1f, 0.85f);
        lightBar.SetColor("_EmissiveColor", new Color(1f, 0.06f, 0.02f) * 10f);
        lightBar.EnableKeyword("_EMISSION");
        var drl = MakeMaterial("Aurora DRL cold white", new Color(0.55f, 0.72f, 0.88f), 0.1f, 0.9f);
        drl.SetColor("_EmissiveColor", new Color(0.72f, 0.88f, 1f) * 5f);
        drl.EnableKeyword("_EMISSION");

        if (specIndices == null || specIndices.Length == 0)
            specIndices = new[] { VehicleCatalog.IndexOf("auroraGt") };

        string[] wheelNames = { "wheel_rl", "wheel_rr", "wheel_fl", "wheel_fr" };
        // Fixed wheel layout in root-local space (the validated BMW layout).
        // Visual wheels are driven to these poses every frame by
        // ArcadeCarController, so a different body never changes the physics.
        Vector3[] wheelLocal =
        {
            new Vector3(0.78f, 0.36f, -1.45f), new Vector3(-0.75f, 0.36f, -1.45f),
            new Vector3(0.78f, 0.36f, 1.31f), new Vector3(-0.75f, 0.36f, 1.31f)
        };
        var colliders = new WheelCollider[4];
        for (int i = 0; i < 4; i++)
        {
            var wcObject = new GameObject("WheelCollider " + wheelNames[i]);
            wcObject.transform.SetParent(root.transform, false);
            wcObject.transform.localPosition = wheelLocal[i];
            var wc = wcObject.AddComponent<WheelCollider>();
            wc.radius = 0.358f;
            wc.suspensionDistance = 0.17f;
            wc.mass = 35f;
            wc.wheelDampingRate = 1.2f;
            var spring = wc.suspensionSpring;
            spring.spring = 39000f; spring.damper = 4600f; spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;
            var forward = wc.forwardFriction;
            forward.extremumSlip = 0.32f; forward.extremumValue = 1.25f; forward.asymptoteSlip = 0.75f; forward.asymptoteValue = 0.95f; forward.stiffness = 1f;
            wc.forwardFriction = forward;
            colliders[i] = wc;
        }

        var variants = new VehicleGarage.Variant[specIndices.Length];
        var specIds = new string[specIndices.Length];
        for (int v = 0; v < specIndices.Length; v++)
        {
            var spec = VehicleCatalog.Cars[specIndices[v]];
            specIds[v] = spec.id;
            var variantRoot = new GameObject("BODY / " + spec.id);
            variantRoot.transform.SetParent(root.transform, false);
            // Apply the per-model heading on the wrapper, NOT on the FBX root.
            // GLB-derived FBX files carry an axis-conversion transform on their
            // root (270 deg about X, scale 100). Overwriting localRotation here
            // tips those cars upright / flattens them into a panel (verified by
            // -garagecheck screenshots on 2026-11-15). Only the original BMW
            // has an identity root, which is why it alone looked correct.
            variantRoot.transform.localRotation = Quaternion.Euler(0f, spec.yaw, 0f);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.assetPath);
            if (!modelAsset) throw new Exception("Car model did not import: " + spec.assetPath);
            var model = UnityEngine.Object.Instantiate(modelAsset, variantRoot.transform, false);
            model.name = spec.displayName;
            model.transform.localPosition = Vector3.zero;
            // Intentionally leave model.transform.localRotation/localScale alone.

            var paint = MakeMaterial("Car paint " + spec.id, spec.paint, 0.72f, 0.93f);
            paint.SetFloat("_CoatMask", 0.85f);
            // The HD fleet pack tags meshes by material class: body / glass /
            // rim / tire / trim / interior / lightwhite / lightred / lightamber /
            // lightgreen. The older packs used body / glass / rim / tire / trim
            // plus the factory lights_red / lights names.
            var interiorMat = MakeMaterial("Car interior " + spec.id, new Color(0.055f, 0.052f, 0.05f), 0.05f, 0.68f);
            var lampWhite = MakeMaterial("Car lamp white " + spec.id, new Color(0.78f, 0.83f, 0.9f), 0.05f, 0.12f);
            var lampAmber = MakeMaterial("Car lamp amber " + spec.id, new Color(0.82f, 0.36f, 0.05f), 0.05f, 0.14f);
            var lampGreen = MakeMaterial("Car lamp green " + spec.id, new Color(0.08f, 0.55f, 0.18f), 0.05f, 0.14f);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                string n = renderer.gameObject.name.ToLowerInvariant();
                if (n.StartsWith("body")) renderer.sharedMaterial = paint;
                else if (n.StartsWith("rim_")) renderer.sharedMaterial = alloy;
                else if (n.StartsWith("tire")) renderer.sharedMaterial = rubber;
                else if (n.Contains("glass")) renderer.sharedMaterial = glass;
                else if (n.StartsWith("interior")) renderer.sharedMaterial = interiorMat;
                else if (n.StartsWith("trim") || n.Contains("carbon") || n.Contains("grills")) renderer.sharedMaterial = carbon;
                else if (n.StartsWith("lightred") || n.Contains("lights_red")) renderer.sharedMaterial = tail;
                else if (n.StartsWith("lightamber")) renderer.sharedMaterial = lampAmber;
                else if (n.StartsWith("lightgreen")) renderer.sharedMaterial = lampGreen;
                else if (n.StartsWith("lightwhite")) renderer.sharedMaterial = lampWhite;
                else if (n.Contains("lights") || n.Contains("leds")) renderer.sharedMaterial = drl;
                else
                {
                    var old = renderer.sharedMaterial;
                    if (old && !old.shader.name.Contains("HDRP"))
                    {
                        Color c = old.HasProperty("_BaseColor") ? old.GetColor("_BaseColor") : (old.HasProperty("_Color") ? old.GetColor("_Color") : Color.gray);
                        renderer.sharedMaterial = MakeMaterial("Car " + spec.id + " " + old.name.Replace(" ", "_"), c, 0.15f, 0.45f);
                    }
                }
            }

            var variant = new VehicleGarage.Variant { root = variantRoot };
            for (int i = 0; i < 4; i++)
            {
                var wheel = FindDeep(model.transform, wheelNames[i]);
                if (!wheel) throw new Exception("Missing independent wheel node " + wheelNames[i] + " in " + spec.id);
                variant.offsets[i] = Quaternion.Inverse(root.transform.rotation) * wheel.rotation;
                wheel.SetParent(variantRoot.transform, true);
                variant.wheels[i] = wheel;
            }
            // Measure the body's real roof height so the procedural wing can be
            // placed relative to each car instead of using hand-tuned numbers.
            // The HD fleet spans 1.24 m (executive saloon) to 1.93 m (city car).
            float roof = 0f;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                string rn = r.gameObject.name.ToLowerInvariant();
                if (!rn.StartsWith("body")) continue;
                Bounds b = r.localBounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;
                Vector3[] corners =
                {
                    new Vector3(c.x - e.x, c.y - e.y, c.z - e.z), new Vector3(c.x + e.x, c.y - e.y, c.z - e.z),
                    new Vector3(c.x - e.x, c.y - e.y, c.z + e.z), new Vector3(c.x + e.x, c.y - e.y, c.z + e.z),
                    new Vector3(c.x - e.x, c.y + e.y, c.z - e.z), new Vector3(c.x + e.x, c.y + e.y, c.z - e.z),
                    new Vector3(c.x - e.x, c.y + e.y, c.z + e.z), new Vector3(c.x + e.x, c.y + e.y, c.z + e.z),
                };
                foreach (var corner in corners)
                {
                    Vector3 world = r.transform.TransformPoint(corner);
                    Vector3 local = root.transform.InverseTransformPoint(world);
                    if (local.y > roof) roof = local.y;
                }
            }
            variant.roofHeight = roof;
            // Also record the body's width and the lowest point so the
            // procedural side skirts and diffuser fins land on the body
            // instead of floating inside or below it.
            float halfWidth = 0f, lowY = 0f;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (!r.gameObject.name.ToLowerInvariant().StartsWith("body")) continue;
                Bounds b = r.localBounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = new Vector3(
                        (k & 1) == 0 ? c.x - e.x : c.x + e.x,
                        (k & 2) == 0 ? c.y - e.y : c.y + e.y,
                        (k & 4) == 0 ? c.z - e.z : c.z + e.z);
                    Vector3 local = root.transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
                    if (local.y < lowY) lowY = local.y;
                }
            }
            variant.halfWidth = halfWidth;
            variant.floorY = lowY;
            variants[v] = variant;
            variantRoot.SetActive(v == 0);
        }

        // ---- Shared part helper: all primitive colliders are stripped -----
        Action<GameObject, Vector3, Vector3, Material, string> addPart = (parent, localPos, localScale, mat, label) =>
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label;
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = mat;
        };

        // ---- AURORA light kit: ALWAYS ON for every one of the bodies -------
        // The generic cars have a flat, featureless rear; emissive lamps and
        // a diffuser are what make them read as cars from the chase camera.
        var lightRoot = new GameObject("AURORA light kit");
        lightRoot.transform.SetParent(root.transform, false);
        addPart(lightRoot, new Vector3(0f, 0.80f, -2.30f), new Vector3(1.44f, 0.065f, 0.045f), lightBar, "Rear LED light bar");
        for (int s = -1; s <= 1; s += 2)
            addPart(lightRoot, new Vector3(s * 0.63f, 0.66f, 2.30f), new Vector3(0.40f, 0.055f, 0.04f), drl, "Front DRL " + s);
        // The skirts sit just outside the body sides, whose measured widths
        // range from 0.90 m to 1.14 m half-width across the fleet.
        float skirtX = variants[0].halfWidth + 0.02f;
        float skirtY = Mathf.Max(0.16f, variants[0].floorY + 0.22f);
        for (int s = -1; s <= 1; s += 2)
            addPart(lightRoot, new Vector3(s * skirtX, skirtY, -0.05f), new Vector3(0.055f, 0.11f, 2.55f), carbon, "Side skirt " + s);

        // ---- AURORA rear wing: closed performance coupes only --------------
        // One child so VehicleGarage can hide it and shift it to the body's
        // measured roof height. The plane sits 6 cm above the roof, so it
        // never intersects the body and never floats above it.
        var aeroRoot = new GameObject("AURORA aero kit");
        aeroRoot.transform.SetParent(root.transform, false);
        // A single flat slab spanning the whole tail reads as a floating
        // panel, so the wing is a narrow, slightly angled blade on two
        // swan-neck posts instead. The fleet runs from 1.79 m to 2.27 m wide.
        float wingW = Mathf.Max(1.05f, variants[0].halfWidth * 1.15f);
        addPart(aeroRoot, new Vector3(0f, 0f, -2.02f), new Vector3(wingW, 0.045f, 0.30f), carbon, "Rear wing plane");
        for (int s = -1; s <= 1; s += 2)
        {
            addPart(aeroRoot, new Vector3(s * wingW * 0.42f, -0.13f, -1.99f), new Vector3(0.05f, 0.26f, 0.10f), carbon, "Rear wing post " + s);
        }
        // Diffuser fins sit under the rear bumper, just above the body's
        // lowest point.
        float finY = Mathf.Max(0.12f, variants[0].floorY + 0.20f);
        for (int f = -1; f <= 1; f++)
            addPart(aeroRoot, new Vector3(f * 0.46f, finY, -2.20f), new Vector3(0.045f, 0.26f, 0.32f), carbon, "Diffuser fin " + f);
        var firstSpec = VehicleCatalog.Cars[specIndices[0]];
        aeroRoot.SetActive(firstSpec.aeroKit);
        // VehicleGarage.Apply() re-seats the wing against the selected body's
        // measured roof, so this initial placement only has to be reasonable.
        aeroRoot.transform.localPosition = new Vector3(0f, variants[0].roofHeight + 0.06f, 0f);

        var controller = root.AddComponent<ArcadeCarController>();
        controller.wheels = colliders;
        controller.wheelVisuals = variants[0].wheels;
        controller.wheelRotationOffsets = variants[0].offsets;
        if (specIndices.Length > 1)
        {
            var garage = root.AddComponent<VehicleGarage>();
            garage.variants = variants;
            garage.specIds = specIds;
            garage.controller = controller;
            garage.aeroKit = aeroRoot;
            garage.lightKit = lightRoot;
            garage.selected = 0;
        }
        var flames = new List<ParticleSystem>();
        var lights = new List<Light>();
        for (int i = 0; i < 2; i++)
        {
            var flameObject = new GameObject("Nitro exhaust " + i);
            flameObject.transform.SetParent(root.transform, false);
            flameObject.transform.localPosition = new Vector3(i == 0 ? -0.48f : 0.48f, 0.36f, -2.12f);
            flameObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var ps = flameObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.18f; main.startSpeed = 14f; main.startSize = 0.17f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.30f, 0.90f, 1f), new Color(0.68f, 0.35f, 1f));
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission; emission.rateOverTime = 95f; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.07f;
            var particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
            // The plasma must be unlit. With HDRP/Lit the golden-hour sun washes
            // the flame out to white, which reads as smoke instead of energy.
            string flamePath = "Assets/Materials/Nitro plasma.mat";
            var flameMaterial = AssetDatabase.LoadAssetAtPath<Material>(flamePath);
            if (!flameMaterial)
            {
                var shader = Shader.Find("HDRP/Unlit");
                flameMaterial = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(flameMaterial, flamePath);
            }
            flameMaterial.SetColor("_BaseColor", new Color(0.35f, 0.8f, 1f));
            flameMaterial.SetColor("_EmissiveColor", new Color(0.30f, 0.85f, 1f) * 5.5f);
            flameMaterial.EnableKeyword("_EMISSION");
            if (flameMaterial.HasProperty("_SurfaceType")) flameMaterial.SetFloat("_SurfaceType", 1f); // transparent
            flameMaterial.SetFloat("_AlphaClip", 0f);
            EditorUtility.SetDirty(flameMaterial);
            particleRenderer.material = flameMaterial;
            flames.Add(ps);
            var lamp = flameObject.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.color = new Color(0.18f, 0.6f, 1f); lamp.intensity = 9f; lamp.range = 6f; lamp.enabled = false;
            lights.Add(lamp);
        }
        controller.boostFlames = flames.ToArray();
        controller.boostLights = lights.ToArray();
        return root;
    }

    public static GameObject CreateCamera(GameObject car)
    {
        var cameraObject = new GameObject("Chase camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 60f;
        cameraObject.AddComponent<HDAdditionalCameraData>();
        cameraObject.AddComponent<AudioListener>();
        var chase = cameraObject.AddComponent<ChaseCamera>();
        chase.target = car.transform;
        chase.targetBody = car.GetComponent<Rigidbody>();
        chase.car = car.GetComponent<ArcadeCarController>();
        chase.Snap();
        cameraObject.tag = "MainCamera";
        return cameraObject;
    }

    public static void BuildWindows(string executableName)
    {
        string directory = Path.GetFullPath("build/Windows");
        Directory.CreateDirectory(directory);
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = Path.Combine(directory, executableName + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        string result = $"result={report.summary.result}\nerrors={report.summary.totalErrors}\nwarnings={report.summary.totalWarnings}\nsize={report.summary.totalSize}\noutput={report.summary.outputPath}\n";
        Directory.CreateDirectory("tests/minimal");
        File.WriteAllText("tests/minimal/build_result.txt", result);
        Debug.Log("AURORA_BUILD_RESULT " + result);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 3);
    }
}
