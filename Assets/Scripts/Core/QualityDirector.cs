using UnityEngine;
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
        [Tooltip("Trees, undergrowth and rocks at Ultra. Every level is a share of these.")]
        public int trees = 1800;
        public int undergrowth = 1100;
        public int rocks = 900;

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
                    case Level.Low: return 2600f;
                    case Level.Medium: return 4000f;
                    case Level.High: return 5600f;
                    default: return 7200f;
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
            // Anti-aliasing costs almost nothing next to the geometry and is
            // the single biggest difference on a snow scene, where every edge
            // is a hard white line against a dark tree.
            switch (level)
            {
                case Level.Low: QualitySettings.antiAliasing = 0; break;
                case Level.Medium: QualitySettings.antiAliasing = 2; break;
                default: QualitySettings.antiAliasing = 4; break;
            }

            QualitySettings.lodBias = level == Level.Low ? 0.7f : level == Level.Ultra ? 2f : 1.2f;
            QualitySettings.skinWeights = SkinWeights.TwoBones;

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
