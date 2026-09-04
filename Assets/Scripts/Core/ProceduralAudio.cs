using UnityEngine;

namespace SnowBound.Core
{
    /// <summary>
    /// Sound written as arithmetic rather than shipped as files.
    ///
    /// The thing that makes generated audio sound harsh is high frequency
    /// energy that nothing in the real world would have. Snow, wind and a
    /// distant motor are all dark; a burst of unfiltered noise is a hiss.
    /// So every generator here takes an explicit ceiling and a slope, and
    /// steep slopes are made by cascading poles rather than hoping one is
    /// enough.
    ///
    /// Looping clips are folded end over start so they never click, and
    /// tonal clips are rounded to a whole number of cycles per loop so the
    /// waveform meets itself exactly.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        /// <summary>
        /// Pink noise, band limited and seamlessly loopable.
        /// </summary>
        /// <param name="ceilingHz">Roll off above this.</param>
        /// <param name="ceilingPoles">1 is gentle (6 dB/oct), 3 is dark and soft.</param>
        /// <param name="floorHz">Remove rumble below this. 0 keeps it all.</param>
        public static AudioClip PinkNoiseLoop(string name, float seconds, int seed,
                                              float ceilingHz, int ceilingPoles, float floorHz)
        {
            int length = Mathf.Max(1024, Mathf.RoundToInt(seconds * SampleRate));
            int fade = Mathf.Min(length / 4, SampleRate / 4);

            float[] raw = Pink(length + fade, seed);
            Shape(raw, ceilingHz, ceilingPoles, floorHz);

            float[] data = FoldLoop(raw, length, fade);

            Normalise(data, 0.7f);
            return Wrap(name, data);
        }

        /// <summary>
        /// A machine note: a fundamental, a few harmonics, a slow wobble and
        /// a bed of rumble underneath. The frequency is rounded to a whole
        /// number of cycles per loop, which is what hides the loop join.
        /// </summary>
        public static AudioClip Hum(string name, float seconds, float hz, int seed)
        {
            int length = Mathf.Max(1024, Mathf.RoundToInt(seconds * SampleRate));
            int cycles = Mathf.Max(1, Mathf.RoundToInt(hz * seconds));

            float[] rumble = Pink(length + length / 4, seed);
            Shape(rumble, 180f, 2, 0f);
            rumble = FoldLoop(rumble, length, length / 4);
            Normalise(rumble, 1f);

            var data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float phase = Mathf.PI * 2f * i / length;

                // A real motor is mostly its fundamental. Piling on harmonics
                // is what turns a hum into a buzz.
                float tone = 0.62f * Mathf.Sin(phase * cycles)
                           + 0.18f * Mathf.Sin(phase * cycles * 2)
                           + 0.05f * Mathf.Sin(phase * cycles * 3);

                // Three wobbles per loop: the unevenness of real machinery.
                tone *= 0.88f + 0.12f * Mathf.Sin(phase * 3f);

                data[i] = tone * 0.75f + rumble[i] * 0.25f;
            }

            Shape(data, 2200f, 2, 0f);
            Normalise(data, 0.6f);
            return Wrap(name, data);
        }

        /// <summary>
        /// A one-shot: band limited noise with a soft attack and a decay.
        /// The attack matters most. Anything under about five milliseconds
        /// reads as a click rather than as an impact.
        /// </summary>
        public static AudioClip Burst(string name, float seconds, int seed,
                                      float ceilingHz, int ceilingPoles, float floorHz,
                                      float attackSeconds, float decayShape, float peak)
        {
            int length = Mathf.Max(256, Mathf.RoundToInt(seconds * SampleRate));

            var rnd = new System.Random(seed);
            var data = new float[length];
            for (int i = 0; i < length; i++) data[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            Shape(data, ceilingHz, ceilingPoles, floorHz);

            int attack = Mathf.Clamp(Mathf.RoundToInt(attackSeconds * SampleRate), 1, length / 2);
            int tail = Mathf.Min(length / 8, SampleRate / 200);   // 5 ms, so the end cannot click

            for (int i = 0; i < length; i++)
            {
                // Raised cosine in, so the front of the sound swells.
                float rise = i < attack
                    ? 0.5f - 0.5f * Mathf.Cos(Mathf.PI * i / attack)
                    : 1f;

                float fall = Mathf.Pow(1f - i / (float)length, decayShape);

                int fromEnd = length - 1 - i;
                if (fromEnd < tail) fall *= fromEnd / (float)tail;

                data[i] *= rise * fall;
            }

            Normalise(data, peak);
            return Wrap(name, data);
        }

        // ---------------- workings ---------------------------------------

        static void Shape(float[] data, float ceilingHz, int ceilingPoles, float floorHz)
        {
            for (int pole = 0; pole < Mathf.Max(1, ceilingPoles); pole++)
                LowPass(data, ceilingHz);

            if (floorHz > 1f) HighPass(data, floorHz);
        }

        /// <summary>Paul Kellet's pink filter: white noise shaped to -3 dB per octave.</summary>
        static float[] Pink(int length, int seed)
        {
            var rnd = new System.Random(seed);
            var data = new float[length];

            float b0 = 0f, b1 = 0f, b2 = 0f, b3 = 0f, b4 = 0f, b5 = 0f, b6 = 0f;

            for (int i = 0; i < length; i++)
            {
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);

                b0 = 0.99886f * b0 + white * 0.0555179f;
                b1 = 0.99332f * b1 + white * 0.0750759f;
                b2 = 0.96900f * b2 + white * 0.1538520f;
                b3 = 0.86650f * b3 + white * 0.3104856f;
                b4 = 0.55000f * b4 + white * 0.5329522f;
                b5 = -0.7616f * b5 - white * 0.0168980f;

                data[i] = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f) * 0.11f;
                b6 = white * 0.115926f;
            }

            return data;
        }

        /// <summary>
        /// Crossfade the overrun back over the start, so the end of the loop
        /// already sounds like the beginning and the join cannot be heard.
        /// </summary>
        static float[] FoldLoop(float[] raw, int length, int fade)
        {
            var data = new float[length];
            System.Array.Copy(raw, data, length);

            for (int i = 0; i < fade && length + i < raw.Length; i++)
            {
                // Equal power, so the crossfade does not dip in level.
                float t = i / (float)fade;
                float a = Mathf.Cos(t * Mathf.PI * 0.5f);
                float b = Mathf.Sin(t * Mathf.PI * 0.5f);
                data[i] = raw[length + i] * a + raw[i] * b;
            }

            return data;
        }

        static void LowPass(float[] data, float cutoffHz)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / SampleRate);
            float y = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                y += a * (data[i] - y);
                data[i] = y;
            }
        }

        /// <summary>Everything the matching low pass would have kept, removed.</summary>
        static void HighPass(float[] data, float cutoffHz)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / SampleRate);
            float y = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                y += a * (data[i] - y);
                data[i] -= y;
            }
        }

        static void Normalise(float[] data, float peak)
        {
            float loudest = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float magnitude = Mathf.Abs(data[i]);
                if (magnitude > loudest) loudest = magnitude;
            }

            if (loudest < 1e-6f) return;

            float scale = peak / loudest;
            for (int i = 0; i < data.Length; i++) data[i] *= scale;
        }

        static AudioClip Wrap(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }
    }
}
