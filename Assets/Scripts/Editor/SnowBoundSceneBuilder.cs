#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SnowBound.Mountain;
using SnowBound.Buildings;
using SnowBound.Player;
using SnowBound.Lifts;
using SnowBound.Game;
using SnowBound.Hud;
using SnowBound.Weather;
using SnowBound.Audio;
using SnowBound.Resort;

namespace SnowBound.EditorTools
{
    /// <summary>
    /// Builds the game scene from code instead of from clicks, so scene setup
    /// is version controlled and repeatable. Everything lives under the
    /// "SnowBound" menu in the top menu bar.
    ///
    /// This file must stay inside a folder called "Editor" — that is how Unity
    /// knows to strip it out of the shipped game.
    /// </summary>
    public static class SnowBoundSceneBuilder
    {
        const string SceneFolder = "Assets/Scenes";
        const string ScenePath = SceneFolder + "/Mountain.unity";

        [MenuItem("SnowBound/Build Mountain Scene", false, 0)]
        public static void BuildMountainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ConfigureCamera();
            ConfigureSun();
            ConfigureEnvironment();
            CreateMountain();
            CreateLodge();
            CreateChairlift();
            CreatePlayer();
            AttachCamera();
            CreateWeather();
            CreateGameRules();
            CreateResort();

            Directory.CreateDirectory(SceneFolder);
            AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[SnowBound] Mountain scene built and saved to " + ScenePath);
        }

        /// <summary>
        /// Render quality lives on the pipeline asset rather than in the
        /// scene, so it survives rebuilding the scene and has to be set
        /// separately. Full render scale, four times multisampling, and a
        /// shadow distance that actually reaches the far side of the piste.
        /// </summary>
        [MenuItem("SnowBound/Improve Render Quality", false, 40)]
        public static void ImproveRenderQuality()
        {
            var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;

            if (asset == null)
            {
                Debug.LogWarning("[SnowBound] No URP asset found. Is this project still on URP?");
                return;
            }

            asset.renderScale = 1f;
            asset.msaaSampleCount = 4;
            asset.shadowDistance = 260f;
            asset.shadowCascadeCount = 3;

            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 1;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log("[SnowBound] Render scale 1.0, MSAA 4x, shadow distance 260 m. " +
                      "If the game still looks soft, check the Game view Scale slider is at 1x " +
                      "and that Low Resolution Aspect Ratios is off.");
        }

        [MenuItem("SnowBound/Rebuild Mountain In Open Scene", false, 20)]
        public static void RebuildMountain()
        {
            var gen = Object.FindAnyObjectByType<MountainGenerator>();
            if (gen == null)
            {
                Debug.LogWarning("[SnowBound] No MountainGenerator in the open scene. " +
                                 "Use SnowBound > Build Mountain Scene first.");
                return;
            }

            gen.Build();
            var props = gen.GetComponent<MountainProps>();
            if (props != null) props.Build();

            var park = gen.GetComponent<TerrainPark>();
            if (park != null) park.Build();

            var lodge = Object.FindAnyObjectByType<LodgeBuilder>();
            if (lodge != null) lodge.Build();

            var lift = Object.FindAnyObjectByType<Chairlift>();
            if (lift != null) lift.Build();

            var rack = Object.FindAnyObjectByType<GearRack>();
            if (rack != null) rack.Build();

            var gates = Object.FindAnyObjectByType<RunTimer>();
            if (gates != null) gates.Build();

            Debug.Log("[SnowBound] Mountain rebuilt.");
        }

        // ---------------------------------------------------------------

        static void ConfigureCamera()
        {
            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            // Temporary look-at-the-mountain view. The player camera replaces
            // this once the character controller exists.
            cam.transform.position = new Vector3(0f, 28f, -75f);
            cam.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 1500f;
            cam.nearClipPlane = 0.1f;
        }

        static void ConfigureSun()
        {
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }

            if (sun == null)
            {
                var go = new GameObject("Directional Light");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            // Low winter sun from the side: long shadows, readable snow shapes.
            sun.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.957f, 0.839f);
            sun.shadows = LightShadows.Soft;
        }

        static void ConfigureEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0025f;
            RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.88f);

            // Cool sky bounce, slightly warmer bounce off the snow.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.70f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.60f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.70f, 0.72f, 0.75f);
        }

        static void CreateMountain()
        {
            var existing = Object.FindAnyObjectByType<MountainGenerator>();
            if (existing != null) return;

            var go = new GameObject("Mountain");
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var gen = go.AddComponent<MountainGenerator>();
            var props = go.AddComponent<MountainProps>();
            var park = go.AddComponent<TerrainPark>();

            gen.Build();
            props.Build();
            park.Build();
        }

        static void CreateChairlift()
        {
            if (Object.FindAnyObjectByType<Chairlift>() != null) return;

            var go = new GameObject("Chairlift");
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<Chairlift>().Build();
            go.AddComponent<LiftAudio>();
        }

        static void CreatePlayer()
        {
            if (Object.FindAnyObjectByType<PlayerController>() != null) return;

            var go = new GameObject("Player");

            var body = go.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.slopeLimit = 45f;
            body.stepOffset = 0.45f;
            body.skinWidth = 0.05f;

            go.AddComponent<PlayerInputReader>();
            go.AddComponent<PlayerVisual>();
            go.AddComponent<WalkMode>();
            go.AddComponent<SkiMode>();
            go.AddComponent<SnowboardMode>();
            go.AddComponent<SnowSpray>();
            go.AddComponent<SnowTrackWriter>();
            go.AddComponent<RideAudio>();
            go.AddComponent<PlayerController>();

            var lodge = Object.FindAnyObjectByType<LodgeBuilder>();
            if (lodge != null) go.transform.position = lodge.EntrancePosition + Vector3.up * 0.3f;
        }

        static void CreateWeather()
        {
            if (Object.FindAnyObjectByType<WeatherSystem>() != null) return;

            var go = new GameObject("Weather");
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var system = go.AddComponent<WeatherSystem>();
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) { system.sun = light; break; }
            }

            var snow = go.AddComponent<Snowfall>();
            snow.weather = system;

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null) snow.follow = player.transform;
        }

        static void CreateGameRules()
        {
            if (Object.FindAnyObjectByType<GearRack>() != null) return;

            var go = new GameObject("Game");
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<GearRack>().Build();
            go.AddComponent<RunTimer>().Build();
            go.AddComponent<SkiHud>();
            go.AddComponent<NotificationStack>();
            go.AddComponent<ManagementScreen>();
            go.AddComponent<DaySummary>();
            go.AddComponent<HudDirector>();
        }

        /// <summary>
        /// The tycoon layer: a clock, a ledger, a demand model, and a
        /// facility component on each thing the resort already owns.
        /// </summary>
        static void CreateResort()
        {
            if (Object.FindAnyObjectByType<ResortClock>() != null) return;

            var go = new GameObject("Resort");
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<ResortIdentity>();
            go.AddComponent<ResortClock>();
            go.AddComponent<Ledger>();
            go.AddComponent<ResortRating>();
            go.AddComponent<ResortTraffic>();

            var lift = Object.FindAnyObjectByType<Chairlift>();
            if (lift != null && lift.GetComponent<LiftFacility>() == null)
            {
                var facility = lift.gameObject.AddComponent<LiftFacility>();
                facility.displayName = "Chairlift";
                facility.lift = lift;
                facility.baseDailyUpkeep = 3200f;
                facility.upkeepPerLevel = 2400f;
                facility.baseQuality = 0.42f;
                facility.qualityPerLevel = 0.20f;
                facility.baseUpgradeCost = 14000f;
                facility.upgradeCostPerLevel = 11000f;
            }

            var lodge = Object.FindAnyObjectByType<LodgeBuilder>();
            if (lodge != null && lodge.GetComponent<LodgeFacility>() == null)
            {
                var facility = lodge.gameObject.AddComponent<LodgeFacility>();
                facility.displayName = "Lodge";
                facility.baseDailyUpkeep = 2100f;
                facility.upkeepPerLevel = 1800f;
                facility.baseQuality = 0.50f;
                facility.qualityPerLevel = 0.18f;
                facility.baseUpgradeCost = 9000f;
                facility.upgradeCostPerLevel = 8000f;
            }

            var park = Object.FindAnyObjectByType<TerrainPark>();
            if (park != null && park.GetComponent<ParkFacility>() == null)
            {
                var facility = park.gameObject.AddComponent<ParkFacility>();
                facility.displayName = "Terrain Park";
                facility.park = park;
                facility.baseDailyUpkeep = 1000f;
                facility.upkeepPerLevel = 900f;
                facility.baseQuality = 0.35f;
                facility.qualityPerLevel = 0.22f;
                facility.baseUpgradeCost = 6000f;
                facility.upgradeCostPerLevel = 5000f;
            }
        }

        static void AttachCamera()
        {
            var cam = Object.FindAnyObjectByType<Camera>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (cam == null || player == null) return;

            var rig = cam.GetComponent<ThirdPersonCamera>();
            if (rig == null) rig = cam.gameObject.AddComponent<ThirdPersonCamera>();
            rig.target = player.transform;
            rig.input = player.GetComponent<PlayerInputReader>();
            rig.player = player;
        }

        static void CreateLodge()
        {
            if (Object.FindAnyObjectByType<LodgeBuilder>() != null) return;

            var go = new GameObject("Lodge");
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<LodgeBuilder>().Build();
        }
    }
}
#endif
