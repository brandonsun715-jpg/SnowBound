using System.Collections.Generic;
using UnityEngine;
using SnowBound.Core;
using SnowBound.Mountain;
using SnowBound.Resort;

namespace SnowBound.Buildings
{
    /// <summary>
    /// A building the player put there.
    ///
    /// It is a Facility, which is the whole reason the rest of the game needs
    /// no changes to accommodate it: it is selectable, inspectable,
    /// upgradeable, it costs money to run and it counts towards the rating,
    /// all because those systems ask a Facility rather than asking what kind
    /// of building something is.
    ///
    /// It builds itself from its definition, and while it is being positioned
    /// it wears a translucent skin instead.
    /// </summary>
    public class PlacedBuilding : Facility
    {
        /// <summary>Every building standing on the mountain.</summary>
        public static readonly List<PlacedBuilding> All = new List<PlacedBuilding>();

        public BuildingDefinition definition;
        public bool ghost = true;

        MountainGenerator _mountain;
        readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        readonly List<Material> _skins = new List<Material>();
        readonly List<Collider> _colliders = new List<Collider>();

        Material _wall, _roof, _trim, _glow;
        Material _validGhost, _invalidGhost;

        public override string LevelSummary
        {
            get
            {
                if (definition == null) return "Building";
                return level > 1 ? definition.name + " · expanded" : definition.firstEffect;
            }
        }

        /// <summary>Set up from a catalogue entry. Call once, before Raise.</summary>
        public void Define(BuildingDefinition source, MountainGenerator mountain)
        {
            definition = source;
            _mountain = mountain;

            displayName = source.name;
            baseDailyUpkeep = source.dailyUpkeep;
            upkeepPerLevel = source.dailyUpkeep * 0.7f;
            baseQuality = source.quality;
            qualityPerLevel = 0.16f;
            baseUpgradeCost = source.cost * 0.85f;
            upgradeCostPerLevel = source.cost * 0.8f;
            maxLevel = 3;
        }

        public void Raise()
        {
            _wall = MaterialFactory.Create("BuildWall", definition.wall, 0.06f);
            _roof = MaterialFactory.Create("BuildRoof", definition.roof, 0.10f);
            _trim = MaterialFactory.Create("BuildTrim", definition.trim, 0.30f);
            _glow = MaterialFactory.CreateEmissive("BuildWindow",
                        new Color(0.95f, 0.78f, 0.45f), new Color(1f, 0.74f, 0.34f) * 2f);

            _validGhost = Ghost(new Color(0.47f, 0.82f, 0.59f, 0.45f));
            _invalidGhost = Ghost(new Color(0.90f, 0.47f, 0.45f, 0.45f));

            BuildShell();
            SetGhost(true, true);
        }

        static Material Ghost(Color tint)
        {
            return MaterialFactory.CreateParticle("BuildGhost", tint, PrimitiveTextures.SoftCircle());
        }

        void OnDestroy() { All.Remove(this); }

        void BuildShell()
        {
            float width = definition.footprint.x;
            float depth = definition.footprint.y;
            float wallHeight = definition.wallHeight;

            // Sunk foundation, so it sits on the snow however it undulates.
            Box("Foundation", new Vector3(0f, -2.6f, 0f),
                new Vector3(width + 0.8f, 5.6f, depth + 0.8f), _roof, true);

            Box("Walls", new Vector3(0f, 0.4f + wallHeight * 0.5f, 0f),
                new Vector3(width, wallHeight, depth), _wall, true);

            Box("Trim", new Vector3(0f, 0.4f + wallHeight - 0.2f, 0f),
                new Vector3(width + 0.3f, 0.35f, depth + 0.3f), _trim, false);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            PrimitiveMeshes.AddPrism(verts, tris, Vector3.zero,
                                     depth * 0.5f + 0.9f, definition.roofHeight, width + 1.8f);

            var roof = new GameObject("Roof");
            roof.transform.SetParent(transform, false);
            roof.transform.localPosition = new Vector3(0f, 0.4f + wallHeight, 0f);
            roof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            roof.AddComponent<MeshFilter>().sharedMesh =
                PrimitiveMeshes.BuildMesh("BuildRoofMesh", verts, tris);
            AddRenderer(roof, _roof);

            Box("Door", new Vector3(0f, 0.4f + 1.15f, depth * 0.5f + 0.06f),
                new Vector3(1.9f, 2.3f, 0.12f), _roof, false);

            int windows = Mathf.Max(2, Mathf.RoundToInt(width / 4f));
            for (int i = 0; i < windows; i++)
            {
                float x = Mathf.Lerp(-width * 0.33f, width * 0.33f, windows == 1 ? 0.5f : i / (float)(windows - 1));
                if (Mathf.Abs(x) < 1.4f) continue;

                Box("Window", new Vector3(x, 0.4f + wallHeight * 0.62f, depth * 0.5f + 0.07f),
                    new Vector3(1.5f, 1.2f, 0.14f), _glow, false);
            }
        }

        void Box(string name, Vector3 position, Vector3 size, Material material, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;

            AddRenderer(go, material);

            var collider = go.GetComponent<Collider>();
            if (solid) { _colliders.Add(collider); return; }

            // Destroy is deferred a frame, so switch it off now as well: a
            // ghost must never catch the ray that is positioning it.
            collider.enabled = false;
            Destroy(collider);
        }

        /// <summary>
        /// Every piece remembers the material it is meant to wear, so putting
        /// the ghost skin on and taking it off again is two loops rather than
        /// a rebuild.
        /// </summary>
        MeshRenderer AddRenderer(GameObject go, Material material)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = go.AddComponent<MeshRenderer>();

            renderer.sharedMaterial = material;

            _renderers.Add(renderer);
            _skins.Add(material);

            return renderer;
        }

        /// <summary>Translucent while positioning, solid once paid for.</summary>
        public void SetGhost(bool isGhost, bool valid)
        {
            ghost = isGhost;

            for (int i = 0; i < _colliders.Count; i++)
                if (_colliders[i] != null) _colliders[i].enabled = !isGhost;

            if (!isGhost)
            {
                if (!All.Contains(this)) All.Add(this);
                RestoreMaterials();
                return;
            }

            Material skin = valid ? _validGhost : _invalidGhost;
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) _renderers[i].sharedMaterial = skin;
        }

        void RestoreMaterials()
        {
            for (int i = 0; i < _renderers.Count && i < _skins.Count; i++)
                if (_renderers[i] != null) _renderers[i].sharedMaterial = _skins[i];
        }

        /// <summary>Sit the building on the snow at this point.</summary>
        public void MoveTo(Vector3 point, float yaw)
        {
            if (_mountain != null) point.y = LowestGround(point, yaw);
            transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        }

        float LowestGround(Vector3 point, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            float half = definition.footprint.x * 0.5f + 1f;
            float deep = definition.footprint.y * 0.5f + 1f;

            float lowest = float.MaxValue;
            for (int ix = -1; ix <= 1; ix++)
            {
                for (int iz = -1; iz <= 1; iz++)
                {
                    Vector3 corner = rotation * new Vector3(ix * half, 0f, iz * deep);
                    float h = _mountain.SampleHeight(point.x + corner.x, point.z + corner.z);
                    if (h < lowest) lowest = h;
                }
            }

            return lowest;
        }

        /// <summary>Steepest ground under the footprint, for placement checks.</summary>
        public float SlopeUnder(Vector3 point)
        {
            if (_mountain == null) return 0f;
            return Vector3.Angle(_mountain.SampleNormal(point.x, point.z), Vector3.up);
        }

        /// <summary>What a passing guest spends here. Zero if they walk past.</summary>
        public float Trade()
        {
            if (definition == null || ghost) return 0f;
            if (Random.value >= definition.visitChance) return 0f;

            return definition.spendPerVisit * (1f + (level - 1) * 0.55f);
        }

        public LedgerLine Line { get { return definition != null ? definition.line : LedgerLine.Rentals; } }

        public float HappinessBonus
        {
            get { return definition != null && !ghost ? definition.happiness : 0f; }
        }
    }
}
