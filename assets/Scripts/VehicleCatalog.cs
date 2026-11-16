using System;
using UnityEngine;

/// <summary>
/// Runtime-visible description of one car in the garage. The editor builder
/// uses the same table to instantiate the scene, so adding a car is a single
/// row here plus a converted FBX under Assets/Vehicles.
/// </summary>
[Serializable]
public sealed class VehicleSpec
{
    public string id;
    public string displayName;
    public string era;
    public string credit;
    public string assetPath;
    public float yaw;
    public Color paint;
    /// <summary>Show the procedural rear wing. Closed performance coupes get it;
    /// open-top classics and the Ferrari keep their factory silhouette. The LED
    /// light kit is always on and is not controlled by this flag.</summary>
    public bool aeroKit;
    /// <summary>Legacy manual wing height. The wing is now placed relative to
    /// each body's measured roof height (VehicleGarage.Variant.roofHeight);
    /// this field is kept only so existing data rows stay readable.</summary>
    public float wingY = 1.18f;

    public VehicleSpec(string id, string displayName, string era, string credit,
                       string assetPath, float yaw, Color paint, bool aeroKit = false, float wingY = 1.18f)
    {
        this.id = id;
        this.displayName = displayName;
        this.era = era;
        this.credit = credit;
        this.assetPath = assetPath;
        this.yaw = yaw;
        this.paint = paint;
        this.aeroKit = aeroKit;
        this.wingY = wingY;
    }
}

/// <summary>
/// The legal, curated garage. Every entry is either CC0 or CC BY 4.0 with the
/// attribution kept in `credit` (shown in the menu and in docs/ASSET_LICENSES.md).
/// No asset here is extracted from a commercial game.
/// </summary>
public static class VehicleCatalog
{
    public static readonly VehicleSpec[] Cars =
    {
        // Ferrari 458 Italia by vicent091036, bundled with the three.js examples.
        // CC BY 4.0 - attribution is mandatory, see docs/ASSET_LICENSES.md.
        new VehicleSpec("ferrari458", "FERRARI 458 ITALIA", "2010s / mid-engine V8",
            "vicent091036 - CC BY 4.0 (three.js examples)",
            "Assets/Vehicles/aurora_gt.fbx", 180f, new Color(0.80f, 0.09f, 0.045f)),

        // ---- HD fleet: 30k-48k triangles each, CC0 1.0 --------------------
        // "Car Park and Road Vehicle Fleet" on 3DAssets.dev. These replaced the
        // earlier 7.6k-triangle Road Car Showroom Lineup cars, which read as
        // blocky prototypes next to the 358k-triangle Ferrari.
        // Nose is -Z in the source GLB, so every yaw below is 180 deg.
        new VehicleSpec("hdSportsCar", "HD MID-ENGINE SPORTS CAR", "modern / wedge GT",
            "3DAssets.dev Car Park & Road Vehicle Fleet - CC0 1.0",
            "Assets/Vehicles/FleetHD/SportsCarHD.fbx", 180f, new Color(0.82f, 0.11f, 0.05f), true, 1.08f),

        new VehicleSpec("hdMuscleCoupe", "HD CLASSIC MUSCLE COUPE", "1970s / V8",
            "3DAssets.dev Car Park & Road Vehicle Fleet - CC0 1.0",
            "Assets/Vehicles/FleetHD/MuscleCoupeHD.fbx", 180f, new Color(0.55f, 0.09f, 0.62f), true, 1.12f),

        new VehicleSpec("hdTouringCoupe", "HD GRAND TOURING COUPE", "modern / GT",
            "3DAssets.dev Car Park & Road Vehicle Fleet - CC0 1.0",
            "Assets/Vehicles/FleetHD/TouringCoupeHD.fbx", 180f, new Color(0.06f, 0.40f, 0.72f), true, 1.14f),

        new VehicleSpec("hdSportsSaloon", "HD SPORTS SALOON", "modern / four-door",
            "3DAssets.dev Car Park & Road Vehicle Fleet - CC0 1.0",
            "Assets/Vehicles/FleetHD/SportsSaloonHD.fbx", 180f, new Color(0.10f, 0.52f, 0.46f), true, 1.18f),

        new VehicleSpec("hdClassicSaloon", "HD CLASSIC SALOON", "1960s / classic",
            "3DAssets.dev Car Park & Road Vehicle Fleet - CC0 1.0",
            "Assets/Vehicles/FleetHD/ClassicSaloonHD.fbx", 180f, new Color(0.80f, 0.82f, 0.85f)),

        new VehicleSpec("hdHotHatch", "HD HOT HATCH", "1980s / hot hatch",
            "3DAssets.dev Car Park & Road Vehicle Fleet - CC0 1.0",
            "Assets/Vehicles/FleetHD/HotHatchHD.fbx", 180f, new Color(0.88f, 0.60f, 0.09f)),

        new VehicleSpec("hdThreeDoor", "HD THREE-DOOR HATCHBACK", "modern / compact",
            "3DAssets.dev Car Park & Road Vehicle Fleet - CC0 1.0",
            "Assets/Vehicles/FleetHD/ThreeDoorHD.fbx", 180f, new Color(0.86f, 0.28f, 0.10f)),

        new VehicleSpec("hdExecutive", "HD EXECUTIVE SALOON", "modern / luxury",
            "3DAssets.dev Car Park & Road Vehicle Fleet - CC0 1.0",
            "Assets/Vehicles/FleetHD/ExecutiveHD.fbx", 180f, new Color(0.13f, 0.16f, 0.22f)),

        new VehicleSpec("hdCityCar", "HD CITY CAR", "modern / city",
            "3DAssets.dev Car Park & Road Vehicle Fleet - CC0 1.0",
            "Assets/Vehicles/FleetHD/CityCarHD.fbx", 180f, new Color(0.92f, 0.86f, 0.30f)),

        new VehicleSpec("hdElectricHatch", "HD ELECTRIC HATCHBACK", "2020s / EV",
            "3DAssets.dev Car Park & Road Vehicle Fleet - CC0 1.0",
            "Assets/Vehicles/FleetHD/ElectricHatchHD.fbx", 180f, new Color(0.24f, 0.62f, 0.34f)),

        // Original high-poly BMW from the McGuire Computer Graphics Archive (CC0).
        new VehicleSpec("auroraGt", "AURORA GT (BMW)", "modern / grand tourer",
            "Mike Pan / Morgan McGuire - CC0",
            "Assets/Vehicles/AuroraGT.fbx", 270f, new Color(0.86f, 0.25f, 0.055f), true),
    };

    public static int IndexOf(string id)
    {
        for (int i = 0; i < Cars.Length; i++) if (Cars[i].id == id) return i;
        return 0;
    }
}
