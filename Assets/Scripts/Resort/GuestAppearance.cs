using UnityEngine;
using SnowBound.Core;
using SnowBound.Player;

namespace SnowBound.Resort
{
    /// <summary>
    /// A guest's body.
    ///
    /// The player is a rig of fifteen parts because the game bends their
    /// knees to sit them on a chairlift. A guest is never bent, only moved,
    /// so a guest is the same rider welded into one mesh: one renderer for
    /// the body and one for whatever is on their feet. That is fewer
    /// renderers than the four primitives this used to be, and it is the
    /// difference between a resort full of people and a resort full of
    /// capsules.
    ///
    /// Nobody in the crowd is identical. The jacket colour tints the whole
    /// rider — it multiplies the one baked texture, so forty guests cost one
    /// material and one texture — and everybody is a slightly different
    /// height and stands a little differently.
    /// </summary>
    public static class GuestAppearance
    {
        public static void Build(Transform parent, LocomotionKind kind, Material jacket,
                                 Material trousers, Material skin, Material gear,
                                 out Transform skis, out Transform board)
        {
            // One trigger so a guest can be clicked. A trigger rather than a
            // solid collider, because a crowd you can shove is worse than a
            // crowd you walk through.
            var pick = parent.gameObject.AddComponent<CapsuleCollider>();
            pick.isTrigger = true;
            pick.radius = 0.42f;
            pick.height = 1.9f;
            pick.center = new Vector3(0f, 0.95f, 0f);

            if (!Modelled(parent, kind, jacket, out skis, out board))
                Primitives(parent, jacket, trousers, skin, gear, out skis, out board);

            skis.gameObject.SetActive(false);
            board.gameObject.SetActive(false);
        }

        /// <summary>
        /// The real rider, welded. Returns false if the models are not in the
        /// project, and the boxes below stand in for them.
        /// </summary>
        static bool Modelled(Transform parent, LocomotionKind kind, Material jacket,
                             out Transform skis, out Transform board)
        {
            skis = board = null;

            string rider = kind == LocomotionKind.Snowboard
                ? HeroAssets.RiderBoard : HeroAssets.RiderSki;

            Mesh body = HeroAssets.Merged(rider);
            if (body == null) return false;

            // Everybody is a different size, and the jacket colour carries
            // through the whole outfit rather than being painted on one panel.
            float height = Random.Range(0.93f, 1.07f);
            Color worn = Color.Lerp(Color.white, Tint(jacket), 0.55f);

            GameObject stood = HeroAssets.Stand(
                rider, body, parent, "Guest Body", Vector3.zero,
                Quaternion.Euler(0f, Random.Range(-8f, 8f), 0f),
                new Vector3(height, height, height), worn);

            if (stood == null) return false;

            var onFeet = new GameObject("Skis");
            onFeet.transform.SetParent(parent, false);
            skis = onFeet.transform;

            Mesh pair = HeroAssets.Pair(HeroAssets.Ski,
                                        new Vector3(-0.14f, 0f, 0f), new Vector3(0.14f, 0f, 0f));
            HeroAssets.Stand(HeroAssets.Ski, pair, skis, "Pair", Vector3.zero,
                             Quaternion.identity, Vector3.one * height, Color.white);

            var deck = new GameObject("Board");
            deck.transform.SetParent(parent, false);
            board = deck.transform;

            HeroAssets.Stand(HeroAssets.Board, HeroAssets.Merged(HeroAssets.Board), board,
                             "Deck", new Vector3(0f, 0f, 0.02f), Quaternion.identity,
                             Vector3.one * height, Color.white);

            return true;
        }

        static Color Tint(Material jacket)
        {
            if (jacket == null) return Color.white;
            if (jacket.HasProperty("_BaseColor")) return jacket.GetColor("_BaseColor");
            if (jacket.HasProperty("_Color")) return jacket.GetColor("_Color");

            return Color.white;
        }

        /// <summary>
        /// Four primitives, for when the models are not there. The player is
        /// worth twelve pieces because you look at them all day; a crowd of
        /// forty is worth four each.
        /// </summary>
        static void Primitives(Transform parent, Material jacket, Material trousers,
                               Material skin, Material gear,
                               out Transform skis, out Transform board)
        {
            Part(parent, PrimitiveType.Capsule, "Legs", new Vector3(0f, 0.42f, 0f),
                 new Vector3(0.30f, 0.40f, 0.30f), trousers);

            Part(parent, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.14f, 0f),
                 new Vector3(0.56f, 0.32f, 0.40f), jacket);

            Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.60f, 0f),
                 new Vector3(0.30f, 0.32f, 0.30f), skin);

            var skiRoot = new GameObject("Skis");
            skiRoot.transform.SetParent(parent, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(skiRoot.transform, PrimitiveType.Cube, "Ski",
                     new Vector3(side * 0.13f, 0.03f, 0.16f),
                     new Vector3(0.11f, 0.05f, 1.65f), gear);
            }
            skis = skiRoot.transform;

            var boardRoot = new GameObject("Board");
            boardRoot.transform.SetParent(parent, false);
            Part(boardRoot.transform, PrimitiveType.Cube, "Board", new Vector3(0f, 0.03f, 0.04f),
                 new Vector3(0.32f, 0.05f, 1.45f), gear);
            board = boardRoot.transform;
        }

        static void Part(Transform parent, PrimitiveType shape, string name,
                         Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            // Guests are not physics objects. They walk on the height field.
            Object.Destroy(go.GetComponent<Collider>());
        }

        public static Material Jacket(int index)
        {
            Color[] palette =
            {
                new Color(0.85f, 0.26f, 0.14f),
                new Color(0.18f, 0.45f, 0.72f),
                new Color(0.92f, 0.72f, 0.20f),
                new Color(0.24f, 0.60f, 0.42f),
                new Color(0.72f, 0.30f, 0.55f),
                new Color(0.90f, 0.92f, 0.95f),
                new Color(0.32f, 0.34f, 0.40f)
            };

            return Surfaces.Fabric("GuestJacket" + index, palette[index % palette.Length]);
        }
    }
}
