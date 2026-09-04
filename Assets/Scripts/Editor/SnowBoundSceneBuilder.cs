#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using SnowBound.Mountain;
using SnowBound.Buildings;
using SnowBound.Player;

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
            CreatePlayer();
            AttachCamera();

            Directory.CreateDirectory(SceneFolder);
            AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[SnowBound] Mountain scene built and saved to " + ScenePath);
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

            var lodge = Object.FindAnyObjectByType<LodgeBuilder>();
            if (lodge != null) lodge.Build();

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

            gen.Build();
            props.Build();
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
            go.AddComponent<PlayerController>();

            var lodge = Object.FindAnyObjectByType<LodgeBuilder>();
            if (lodge != null) go.transform.position = lodge.EntrancePosition + Vector3.up * 0.3f;
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
