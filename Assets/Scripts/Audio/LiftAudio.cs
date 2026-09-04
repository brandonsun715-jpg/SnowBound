using UnityEngine;
using SnowBound.Core;
using SnowBound.Lifts;

namespace SnowBound.Audio
{
    /// <summary>
    /// The chairlift's motor room. One 3D source at each terminal, because
    /// that is where the machinery actually is: the hum grows as you skate
    /// towards the loading area and fades behind you on the way up.
    /// </summary>
    public class LiftAudio : MonoBehaviour
    {
        public Chairlift lift;

        [Range(0f, 1f)] public float volume = 0.22f;
        [Tooltip("Metres before the hum starts to fall away.")]
        public float nearDistance = 10f;
        [Tooltip("Metres beyond which it cannot be heard.")]
        public float farDistance = 95f;
        [Tooltip("Fundamental of the motor, in hertz. Lower is a heavier machine.")]
        public float motorHz = 88f;

        void Start()
        {
            if (lift == null) lift = Chairlift.Instance;
            if (lift == null) return;

            AudioClip hum = ProceduralAudio.Hum("LiftMotor", 2f, motorHz, 9001);

            // The two ends run the same machinery at slightly different
            // speeds, which stops them phasing into one tone.
            Terminal("BottomStationAudio", lift.BoardingPoint, hum, 1.0f);
            Terminal("TopStationAudio", lift.UnloadPoint, hum, 1.06f);
        }

        void Terminal(string name, Vector3 position, AudioClip clip, float pitch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.hideFlags = HideFlags.DontSaveInEditor;

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = nearDistance;
            source.maxDistance = farDistance;
            source.dopplerLevel = 0f;
            source.Play();
        }
    }
}
