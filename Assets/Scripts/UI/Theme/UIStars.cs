using UnityEngine;
using UnityEngine.UI;

namespace SnowBound.Hud
{
    /// <summary>
    /// A five star rating drawn from the icon set rather than from text.
    ///
    /// Star characters are not in every font, and a rating that renders as
    /// five empty boxes on someone else's machine is not a rating. Drawing
    /// them means they also match the rest of the iconography exactly.
    /// </summary>
    public class UIStars : MonoBehaviour
    {
        Image[] _stars;

        public static UIStars Create(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                                     Vector2 offset, float size, float spacing)
        {
            RectTransform root = UIBuilder.Place(UIBuilder.Node(parent, name), anchor, pivot, offset,
                                                 new Vector2(size * 5f + spacing * 4f, size));

            var stars = root.gameObject.AddComponent<UIStars>();
            stars._stars = new Image[5];

            for (int i = 0; i < 5; i++)
            {
                stars._stars[i] = UIBuilder.Icon(root, "Star" + i, UIIcons.StarHollow, UITheme.InkFaint,
                                                 new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                                 new Vector2(i * (size + spacing), 0f), size);
            }

            return stars;
        }

        public void Set(float stars)
        {
            if (_stars == null) return;

            for (int i = 0; i < _stars.Length; i++)
            {
                bool lit = stars >= i + 0.5f;
                _stars[i].sprite = lit ? UIIcons.Star : UIIcons.StarHollow;
                _stars[i].color = lit ? UITheme.Ice : UITheme.InkFaint;
            }
        }
    }
}
