using System;
using UnityEngine;

namespace RoadSafety.UI
{
    /// <summary>
    /// Small, friendly cartoon sounds made in code, so the HUD has sound before we pick
    /// real (royalty-free) sound files. Drag real clips onto the HUDController to replace them.
    /// </summary>
    public static class HUDSounds
    {
        private const int SampleRate = 44100;
        private const float TwoPi = Mathf.PI * 2f;

        /// <summary>"Tuut-tuut": two short, round beeps (a major third, so it sounds friendly, not angry).</summary>
        public static AudioClip Horn()
        {
            const float beep = 0.14f, gap = 0.05f;
            return Make("Horn", beep * 2f + gap, t =>
            {
                float local = t < beep ? t : t - beep - gap;
                if (local < 0f)
                    return 0f;
                float env = Envelope(local, beep, 0.012f, 0.05f);
                return 0.45f * env * (Warm(t, 466.2f) * 0.6f + Warm(t, 587.3f) * 0.4f);
            });
        }

        /// <summary>Joyful "ding-ding" when a star is won.</summary>
        public static AudioClip StarWon()
        {
            return Make("StarWon", 0.7f, t =>
                0.35f * Bell(t, 1046.5f, 7f) +
                (t > 0.09f ? 0.35f * Bell(t - 0.09f, 1568f, 6f) : 0f));
        }

        /// <summary>Seatbelt "click" followed by a bright chime.</summary>
        public static AudioClip SeatbeltClick()
        {
            var noise = new System.Random(7);
            return Make("SeatbeltClick", 0.55f, t =>
            {
                float click = t < 0.03f ? (float)(noise.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 180f) * 0.6f : 0f;
                float clack = t > 0.06f && t < 0.09f ? (float)(noise.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-(t - 0.06f) * 200f) * 0.5f : 0f;
                float chime = t > 0.12f ? 0.3f * Bell(t - 0.12f, 1318.5f, 6f) : 0f;
                return click + clack + chime;
            });
        }

        /// <summary>Gentle "ding-dong" to get attention (seatbelt reminder). Not a punishment sound.</summary>
        public static AudioClip Reminder()
        {
            return Make("Reminder", 0.75f, t =>
                0.35f * Bell(t, 784f, 5f) +
                (t > 0.22f ? 0.35f * Bell(t - 0.22f, 659.3f, 5f) : 0f));
        }

        /// <summary>Soft cartoon "boing" when a star is lost.</summary>
        public static AudioClip StarLost()
        {
            const float length = 0.4f;
            float phase = 0f;
            return Make("StarLost", length, t =>
            {
                float frequency = Mathf.Lerp(520f, 240f, t / length) * (1f + 0.04f * Mathf.Sin(TwoPi * 14f * t));
                phase += TwoPi * frequency / SampleRate;
                return 0.4f * Envelope(t, length, 0.01f, 0.15f) * Mathf.Sin(phase);
            });
        }

        /// <summary>Three soft falling notes when a heart is lost.</summary>
        public static AudioClip HeartLost()
        {
            const float note = 0.13f;
            float[] notes = { 659.3f, 523.3f, 440f };
            return Make("HeartLost", note * notes.Length + 0.1f, t =>
            {
                int index = Mathf.Min((int)(t / note), notes.Length - 1);
                float local = t - index * note;
                float length = index == notes.Length - 1 ? note + 0.1f : note;
                return 0.35f * Envelope(local, length, 0.01f, 0.06f) * Warm(t, notes[index]);
            });
        }

        /// <summary>Short happy fanfare (badge, end of level): an arpeggio going up.</summary>
        public static AudioClip Fanfare()
        {
            float[] notes = { 523.3f, 659.3f, 784f, 1046.5f };
            const float step = 0.11f;
            return Make("Fanfare", step * notes.Length + 0.6f, t =>
            {
                float sum = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * step;
                    if (t >= start)
                        sum += 0.22f * Bell(t - start, notes[i], i == notes.Length - 1 ? 3f : 7f);
                }
                return sum;
            });
        }

        /// <summary>Soft "tick" for menu buttons.</summary>
        public static AudioClip Click()
        {
            return Make("Click", 0.08f, t => 0.35f * Envelope(t, 0.08f, 0.002f, 0.06f) * Mathf.Sin(TwoPi * 1200f * t));
        }

        /// <summary>Two quick soft notes when something is in front of the car.</summary>
        public static AudioClip Danger()
        {
            return Make("Danger", 0.36f, t =>
                0.28f * (t < 0.16f ? Envelope(t, 0.16f, 0.01f, 0.05f) * Warm(t, 880f)
                                   : Envelope(t - 0.18f, 0.16f, 0.01f, 0.05f) * Warm(t, 880f)));
        }

        // ---------- helpers ----------

        private static AudioClip Make(string name, float seconds, Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(sample(i / (float)SampleRate), -1f, 1f);

            AudioClip clip = AudioClip.Create("HUD_" + name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A round tone: a sine with a little 2nd and 3rd harmonic.</summary>
        private static float Warm(float t, float frequency)
        {
            float x = TwoPi * frequency * t;
            return (Mathf.Sin(x) + 0.3f * Mathf.Sin(2f * x) + 0.12f * Mathf.Sin(3f * x)) / 1.42f;
        }

        /// <summary>A bell-like note that fades out.</summary>
        private static float Bell(float t, float frequency, float decay)
        {
            float x = TwoPi * frequency * t;
            return Mathf.Exp(-t * decay) * (Mathf.Sin(x) + 0.25f * Mathf.Exp(-t * decay) * Mathf.Sin(2.01f * x) + 0.1f * Mathf.Sin(3f * x)) / 1.35f;
        }

        private static float Envelope(float t, float length, float attack, float release)
        {
            if (t < 0f || t > length) return 0f;
            if (t < attack) return t / attack;
            if (t > length - release) return (length - t) / release;
            return 1f;
        }
    }
}
