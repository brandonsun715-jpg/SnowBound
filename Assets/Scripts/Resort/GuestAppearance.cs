using UnityEngine;
using SnowBound.Core;

namespace SnowBound.Resort
{
    /// <summary>
    /// A guest's body: four primitives and no more.
    ///
    /// The player is worth twelve pieces because you look at them all day. A
    /// crowd of forty is worth four each, because what reads from the
    /// management camera is the crowd, not any one person in it. Materials
    /// are shared across the whole crowd for the same reason.
    /// </summary>
    public static class GuestAppearance
    {
        public static void Build(Transform parent, Material jacket, Material trousers,
                                 Material skin, Material gear,
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

            skis.gameObject.SetActive(false);
            board.gameObject.SetActive(false);
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

            return MaterialFactory.Create("GuestJacket" + index, palette[index % palette.Length], 0.15f);
        }
    }
}
