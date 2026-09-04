using UnityEngine;

namespace SnowBound.Core
{
    /// <summary>
    /// Sound written as arithmetic rather than shipped as files.
    ///
    /// Wind and the hiss of snow under an edge are both filtered noise; a
    /// lift motor is a stack of harmonics with a rumble under it; a landing
    /// is a burst of noise that decays. None of that needs a recording, and
    /// generating it keeps the promise that the prototype depends on no
    /// external assets at all.
    ///
    /// Looping clips are folded end to start so they can run forever without
    /// a click, and tonal clips use whole numbers of cycles per loop so the
    /// waveform meets itself exactly.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        /// <summary>
        /// Pink noise, seamlessly loopable. Pink rather than white because
        /// falling energy with rising frequency is what wind, water and most
        /// natural noise actually sound like; white noise hisses.
        /// </summary>
        public static AudioClip PinkNoiseLoop(string name, float seconds, int seed)
        {
            int length = Mathf.Max(1024, Mathf.RoundToInt(seconds * SampleRate));
            int fade = Mathf.Min(length / 4, SampleRate / 4);

            float[] raw = Pink(length + fade, seed);
            float[] data = FoldLoop(raw, length, fade);

            Normalise(data, 0.85f);
            return Wrap(name, data);
        }

        /// <summary>
        /// A machine note: a fundamental, a few harmonics, a slow wobble and
        /// a bed of rumble. Frequencies are given as whole cycles per loop,
        /// which is what makes the loop join invisible.
        /// </summary>
        public static AudioClip Hum(string name, float seconds, int baseCycles, int seed)
        {
            int length = Mathf.Max(1024, Mathf.RoundToInt(seconds * SampleRate));

            float[] rumble = Pink(length + length / 4, seed);
            rumble = FoldLoop(rumble, length, length / 4);
            LowPass(rumble, 220f);
            Normalise(rumble, 1f);

            var data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float phase = Mathf.PI * 2f * i / length;

                float tone = 0.55f * Mathf.Sin(phase * baseCycles)
                           + 0.24f * Mathf.Sin(phase * baseCycles * 2)
                           + 0.10f * Mathf.Sin(phase * baseCycles * 3)
                           + 0.05f * Mathf.Sin(phase * baseCycles * 5);

                // Three wobbles per loop: the unevenness of real machinery.
                tone *= 0.86f + 0.14f * Mathf.Sin(phase * 3f);

                data[i] = tone * 0.7f + rumble[i] * 0.30f;
            }

            Normalise(data, 0.8f);
            return Wrap(name, data);
        }

        /// <summary>
        /// A one-shot: band-limited noise with a quick attack and a decay.
        /// Landings, footfalls in snow, the whump of taking off.
        /// </summary>
        public static AudioClip Burst(string name, float seconds, float lowHz, float highHz,
                                      float decayShape, int seed)
        {
            int length = Mathf.Max(256, Mathf.RoundToInt(seconds * SampleRate));

            var rnd = new System.Random(seed);
            var data = new float[length];
            for (int i = 0; i < length; i++) data[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            LowPass(data, highHz);
            HighPass(data, lowHz);

            int attack = Mathf.Max(1, SampleRate / 500);   // 2 ms, so it does not click

            for (int i = 0; i < length; i++)
            {
                float rise = i < attack ? i / (float)attack : 1f;
                float fall = Mathf.Pow(1f - i / (float)length, decayShape);
                data[i] *= rise * fall;
            }

            Normalise(data, 0.9f);
            return Wrap(name, data);
        }

        // ---------------- workings ---------------------------------------

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
                float t = i / (float)fade;
                data[i] = raw[length + i] * (1f - t) + raw[i] * t;
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
