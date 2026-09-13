using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SnowBound.Mountain;
using SnowBound.Weather;

namespace SnowBound.Core
{
    /// <summary>
    /// One dial for how much machine the mountain is allowed to use.
    ///
    /// The resort is a forest of eighteen hundred trees, a thousand pieces of
    /// undergrowth, hundreds of rocks, a lift, a park and a crowd. On a good
    /// machine all of it should be there; on a laptop it should still be a
    /// mountain. Nothing here is a switch that turns an effect off and leaves
    /// the scene looking broken — each level is a whole setting, chosen so
    /// the mountain still reads at every one of them.
    ///
    /// What scales, in the order it costs:
    ///   how much is planted, how far you can see, how far shadows are cast,
    ///   how many cascades they use, how big the distant ranges are, and
    ///   whether the grade runs at all.
    ///
    /// Changing the level replants the mountain, which takes a moment, so it
    /// only happens when the level actually changes.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-40)]
    public class QualityDirector : MonoBehaviour
    {
        public enum Level { Low, Medium, High, Ultra }

        [Tooltip("Low runs on anything. Ultra is what the mountain was built to look like.")]
        public Level level = Level.High;

        [Header("What it drives")]
        [Tooltip("Leave empty to find them in the scene.")]
        public MountainProps props;
        public FarRange range;
        public Camera view;
        public PostProcessing grade;

        [Header("Planting")]
        [Tooltip("Trees, undergrowth and rocks at Ultra. Every level is a share of these.\nThese are the numbers, not MountainProps' — this overwrites them — so a\nmap that grew and a forest that did not is this field being left behind.")]
        public int trees = 8200;
        public int undergrowth = 5000;
        public int rocks = 4200;

        [Header("Frame pacing")]
        [Tooltip("What the game aims for. A steady sixty beats an unsteady ninety:\nthe eye reads the change, not the number.")]
        public int targetFrameRate = 60;
        [Tooltip("On, so frames are handed over when the screen is ready for them.")]
        public bool verticalSync = true;

        Level _applied = (Level)(-1);

        void OnEnable() { Apply(); }
        void OnValidate() { if (isActiveAndEnabled) Apply(); }

        /// <summary>How much of the planting this level keeps.</summary>
        public float Density
        {
            get
            {
                switch (level)
                {
                    case Level.Low: return 0.30f;
                    case Level.Medium: return 0.55f;
                    case Level.High: return 0.80f;
                    default: return 1.00f;
                }
            }
        }

        /// <summary>How far the world is drawn, in metres.</summary>
        public float Sight
        {
            get
            {
                switch (level)
                {
                    case Level.Low: return 2000f;
                    case Level.Medium: return 2800f;
                    case Level.High: return 3600f;
                    default: return 4400f;
                }
            }
        }

        [ContextMenu("Apply")]
        public void Apply()
        {
            bool replant = _applied != level;
            _applied = level;

            Find();
            Screen();
            Shadows();
            Distance();
            Replant(replant);
        }

        void Find()
        {
            if (props == null) props = FindAnyObjectByType<MountainProps>();
            if (range == null) range = FindAnyObjectByType<FarRange>();
            if (grade == null) grade = FindAnyObjectByType<PostProcessing>();
            if (view == null) view = Camera.main;
        }

        void Screen()
        {
            // Multisampling, and nothing on top of it.
            //
            // The scene used to run SMAA over the top of four times MSAA. The
            // hardware had already resolved every edge by then, so all the
            // second pass could do was soften what was underneath it — which
            // is exactly what "blurry" looks like on a snowfield, where most
            // of the picture is fine texture rather than edges. Post-process
            // anti-aliasing is now only used at Low, where there is no MSAA
            // for it to fight with.
            int samples = level == Level.Low ? 1 : level == Level.Medium ? 2 : 4;

            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                pipeline.msaaSampleCount = samples;

                // Never below one. Rendering small and stretching up is the
                // other way a game goes blurry, and it is not worth the frames.
                pipeline.renderScale = 1f;
            }

            QualitySettings.antiAliasing = samples > 1 ? samples : 0;

            if (view != null)
            {
                var data = view.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.antialiasing = samples > 1
                        ? AntialiasingMode.None
                        : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.Medium;
                }
            }

            QualitySettings.lodBias = level == Level.Low ? 0.7f : level == Level.Ultra ? 2f : 1.2f;
            QualitySettings.skinWeights = SkinWeights.TwoBones;

            // A steady rate, because what reads as stutter is the change in
            // frame time rather than its size.
            QualitySettings.vSyncCount = verticalSync ? 1 : 0;
            Application.targetFrameRate = verticalSync ? -1 : Mathf.Max(30, targetFrameRate);

            if (grade != null) grade.enabled = level != Level.Low;
        }

        void Shadows()
        {
            switch (level)
            {
                case Level.Low:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowDistance = 90f;
                    QualitySettings.shadowCascades = 1;
                    break;

                case Level.Medium:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 160f;
                    QualitySettings.shadowCascades = 2;
                    break;

                case Level.High:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 260f;
                    QualitySettings.shadowCascades = 4;
                    break;

                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 420f;
                    QualitySettings.shadowCascades = 4;
                    break;
            }
        }

        void Distance()
        {
            if (view != null) view.farClipPlane = Sight;

            // The distant ranges are scenery: at Low they are the first thing
            // that can go, because the fog is thick enough by then to hide
            // that they went.
            if (range != null && range.gameObject.activeSelf != (level != Level.Low))
                range.gameObject.SetActive(level != Level.Low);
        }

        void Replant(bool needed)
        {
            if (!needed || props == null) return;

            float share = Density;

            props.treeCount = Mathf.RoundToInt(trees * share);
            props.undergrowthCount = Mathf.RoundToInt(undergrowth * share);
            props.rockCount = Mathf.RoundToInt(rocks * share);

            props.Build();
        }
    }
}
