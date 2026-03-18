using UnityEngine;

namespace OpenEmpires
{
    public enum SFXType
    {
        ArrowFire,
        ArrowImpact,
        MeleeAttack,
        UnitHurt,
        UnitDeath,
        BuildingPlace,
        UnitTrained,
        UnitSelect,
        BuildingSelect,
        CommandMove,
        QueueUnit,
        ConstructionComplete,
        GatherStrike,
        ChatMessage,
        SurrenderVote,
        LobbyJoin,
        SheepConvert,
        UnderAttack,
    }

    public class SFXManager : MonoBehaviour
    {
        public static SFXManager Instance { get; private set; }

        private const int SampleRate = 44100;
        private const int PoolSize = 16;
        private const float SfxVolume = 0.5f;

        private AudioClip[] clips;
        private AudioSource[] pool;
        private int poolIndex;

        private float[] lastPlayTime;

        private static readonly float[] Cooldowns = new float[]
        {
            0.05f,  // ArrowFire
            0.05f,  // ArrowImpact
            0.08f,  // MeleeAttack
            0.06f,  // UnitHurt
            0.1f,   // UnitDeath
            0.15f,  // BuildingPlace
            0.3f,   // UnitTrained
            0.08f,  // UnitSelect
            0.08f,  // BuildingSelect
            0.1f,   // CommandMove
            0.05f,  // QueueUnit
            0.3f,   // ConstructionComplete
            0.06f,  // GatherStrike
            0.1f,   // ChatMessage
            0.1f,   // SurrenderVote
            0.3f,   // LobbyJoin
            0.3f,   // SheepConvert
            5.0f,   // UnderAttack
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;

            var go = new GameObject("SFXManager");
            Instance = go.AddComponent<SFXManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            int typeCount = System.Enum.GetValues(typeof(SFXType)).Length;
            clips = new AudioClip[typeCount];
            lastPlayTime = new float[typeCount];
            for (int i = 0; i < typeCount; i++)
                lastPlayTime[i] = -100f;

            GenerateClips();
            CreatePool();
        }

        private void CreatePool()
        {
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.minDistance = 5f;
                source.maxDistance = 80f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                pool[i] = source;
            }
        }

        public void Play(SFXType type, Vector3 worldPos, float volumeScale = 1f)
        {
            int idx = (int)type;
            if (Time.time - lastPlayTime[idx] < Cooldowns[idx])
                return;
            lastPlayTime[idx] = Time.time;

            var source = pool[poolIndex];
            poolIndex = (poolIndex + 1) % PoolSize;

            source.transform.position = worldPos;
            source.clip = clips[idx];
            source.volume = SfxVolume * volumeScale;
            source.Play();
        }

        /// <summary>Play a non-positional (2D) sound, e.g. UI clicks.</summary>
        public void PlayUI(SFXType type, float volumeScale = 1f)
        {
            int idx = (int)type;
            if (Time.time - lastPlayTime[idx] < Cooldowns[idx])
                return;
            lastPlayTime[idx] = Time.time;

            var source = pool[poolIndex];
            poolIndex = (poolIndex + 1) % PoolSize;

            source.transform.position = Vector3.zero;
            source.clip = clips[idx];
            source.volume = SfxVolume * volumeScale;
            source.spatialBlend = 0f;
            source.Play();
            source.spatialBlend = 1f; // restore for next 3D use
        }

        private void GenerateClips()
        {
            clips[(int)SFXType.ArrowFire] = GenerateArrowFire();
            clips[(int)SFXType.ArrowImpact] = GenerateArrowImpact();
            clips[(int)SFXType.MeleeAttack] = GenerateMeleeAttack();
            clips[(int)SFXType.UnitHurt] = GenerateUnitHurt();
            clips[(int)SFXType.UnitDeath] = GenerateUnitDeath();
            clips[(int)SFXType.BuildingPlace] = GenerateBuildingPlace();
            clips[(int)SFXType.UnitTrained] = GenerateUnitTrained();
            clips[(int)SFXType.UnitSelect] = GenerateUnitSelect();
            clips[(int)SFXType.BuildingSelect] = GenerateBuildingSelect();
            clips[(int)SFXType.CommandMove] = GenerateCommandMove();
            clips[(int)SFXType.QueueUnit] = GenerateQueueUnit();
            clips[(int)SFXType.ConstructionComplete] = GenerateConstructionComplete();
            clips[(int)SFXType.GatherStrike] = GenerateGatherStrike();
            clips[(int)SFXType.ChatMessage] = GenerateChatMessage();
            clips[(int)SFXType.SurrenderVote] = GenerateSurrenderVote();
            clips[(int)SFXType.LobbyJoin] = GenerateLobbyJoin();
            clips[(int)SFXType.SheepConvert] = GenerateSheepConvert();
            clips[(int)SFXType.UnderAttack] = GenerateUnderAttack();
        }

        private AudioClip GenerateArrowFire()
        {
            float duration = 0.15f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            uint noiseState = 12345;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;
                float envelope = 1f - norm;
                envelope *= envelope;

                // Sine sweep 800 -> 200 Hz
                float freq = Mathf.Lerp(800f, 200f, norm);
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f;

                // Filtered noise whoosh
                noiseState = XorShift(noiseState);
                float noise = ((noiseState & 0xFFFF) / 32768f - 1f) * 0.3f;

                data[i] = (sine + noise) * envelope;
            }

            return CreateClip("ArrowFire", data);
        }

        private AudioClip GenerateArrowImpact()
        {
            float duration = 0.1f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            uint noiseState = 67890;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;
                float envelope = (1f - norm);
                envelope = envelope * envelope * envelope;

                float sine = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.6f;

                noiseState = XorShift(noiseState);
                float noise = ((noiseState & 0xFFFF) / 32768f - 1f) * 0.4f;

                data[i] = (sine + noise) * envelope;
            }

            return CreateClip("ArrowImpact", data);
        }

        private AudioClip GenerateMeleeAttack()
        {
            float duration = 0.2f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            uint noiseState = 11111;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;

                // Noise whoosh (early)
                float whooshEnv = Mathf.Max(0f, 1f - norm * 4f);
                whooshEnv *= whooshEnv;
                noiseState = XorShift(noiseState);
                float noise = ((noiseState & 0xFFFF) / 32768f - 1f) * whooshEnv * 0.4f;

                // Delayed clang (starts at ~30% through)
                float clangT = Mathf.Max(0f, norm - 0.3f) / 0.7f;
                float clangEnv = Mathf.Max(0f, 1f - clangT * 2f);
                clangEnv *= clangEnv;
                float clang = (Mathf.Sin(2f * Mathf.PI * 440f * t)
                             + Mathf.Sin(2f * Mathf.PI * 587f * t) * 0.7f
                             + Mathf.Sin(2f * Mathf.PI * 783f * t) * 0.4f)
                             * clangEnv * 0.25f;

                data[i] = noise + clang;
            }

            return CreateClip("MeleeAttack", data);
        }

        private AudioClip GenerateUnitHurt()
        {
            float duration = 0.12f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;
                float envelope = (1f - norm);
                envelope = envelope * envelope * envelope;

                float s1 = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.5f;
                float s2 = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.4f;

                data[i] = (s1 + s2) * envelope;
            }

            return CreateClip("UnitHurt", data);
        }

        private AudioClip GenerateUnitDeath()
        {
            float duration = 0.35f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;
                float envelope = (1f - norm);
                envelope *= envelope;

                float s1 = Mathf.Sin(2f * Mathf.PI * 80f * t) * 0.6f;
                float s2 = Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.3f;

                data[i] = (s1 + s2) * envelope;
            }

            return CreateClip("UnitDeath", data);
        }

        private AudioClip GenerateBuildingPlace()
        {
            float duration = 0.3f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            uint noiseState = 54321;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;
                float envelope = (1f - norm);
                envelope = envelope * envelope * envelope;

                // Deep thump
                float thump = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.7f;

                // Low-freq noise
                noiseState = XorShift(noiseState);
                float noise = ((noiseState & 0xFFFF) / 32768f - 1f) * 0.3f;
                // Simple low-pass: reduce high-freq content by averaging
                noise *= (1f - norm);

                data[i] = (thump + noise) * envelope;
            }

            return CreateClip("BuildingPlace", data);
        }

        private AudioClip GenerateUnitTrained()
        {
            float duration = 0.4f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;

                // Two-note horn: first half 440 Hz, second half 554 Hz
                float freq = norm < 0.5f ? 440f : 554f;

                // Vibrato
                float vibrato = Mathf.Sin(2f * Mathf.PI * 6f * t) * 8f;

                // Envelope: attack + sustain + decay
                float envelope;
                if (norm < 0.05f)
                    envelope = norm / 0.05f; // attack
                else if (norm < 0.85f)
                    envelope = 1f; // sustain
                else
                    envelope = (1f - norm) / 0.15f; // decay

                float sample = Mathf.Sin(2f * Mathf.PI * (freq + vibrato) * t) * 0.5f;

                // Add slight odd harmonics for brass character
                sample += Mathf.Sin(2f * Mathf.PI * (freq + vibrato) * 3f * t) * 0.1f;

                data[i] = sample * envelope;
            }

            return CreateClip("UnitTrained", data);
        }

        private AudioClip GenerateUnitSelect()
        {
            // Short bright click — high-pitched tap
            float duration = 0.08f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;
                float envelope = (1f - norm);
                envelope = envelope * envelope * envelope;

                // Bright tap: 1200 Hz + 1800 Hz harmonics
                float s1 = Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.4f;
                float s2 = Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.2f;

                data[i] = (s1 + s2) * envelope;
            }

            return CreateClip("UnitSelect", data);
        }

        private AudioClip GenerateBuildingSelect()
        {
            // Lower-pitched thud click for buildings
            float duration = 0.1f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;
                float envelope = (1f - norm);
                envelope = envelope * envelope * envelope;

                // Lower tone: 600 Hz + 900 Hz
                float s1 = Mathf.Sin(2f * Mathf.PI * 600f * t) * 0.4f;
                float s2 = Mathf.Sin(2f * Mathf.PI * 900f * t) * 0.25f;

                data[i] = (s1 + s2) * envelope;
            }

            return CreateClip("BuildingSelect", data);
        }

        private AudioClip GenerateCommandMove()
        {
            // Two quick descending tones — "acknowledged" blip
            float duration = 0.12f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;

                // Two-tone: first half higher, second half lower
                float freq = norm < 0.4f ? 900f : 650f;
                float envelope;
                if (norm < 0.05f)
                    envelope = norm / 0.05f;
                else
                    envelope = Mathf.Max(0f, 1f - (norm - 0.05f) / 0.95f);
                envelope *= envelope;

                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f * envelope;
            }

            return CreateClip("CommandMove", data);
        }

        private AudioClip GenerateQueueUnit()
        {
            // Quick click/pop — UI feedback
            float duration = 0.06f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;
                float envelope = (1f - norm);
                envelope = envelope * envelope * envelope * envelope;

                // Sharp pop: 1000 Hz
                data[i] = Mathf.Sin(2f * Mathf.PI * 1000f * t) * 0.35f * envelope;
            }

            return CreateClip("QueueUnit", data);
        }

        private AudioClip GenerateConstructionComplete()
        {
            // Triumphant three-note ascending chime
            float duration = 0.5f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;

                // Three ascending notes: C5 (523), E5 (659), G5 (784)
                float freq;
                if (norm < 0.33f) freq = 523f;
                else if (norm < 0.66f) freq = 659f;
                else freq = 784f;

                // Per-note envelope with attack and decay
                float noteNorm;
                if (norm < 0.33f) noteNorm = norm / 0.33f;
                else if (norm < 0.66f) noteNorm = (norm - 0.33f) / 0.33f;
                else noteNorm = (norm - 0.66f) / 0.34f;

                float envelope;
                if (noteNorm < 0.1f)
                    envelope = noteNorm / 0.1f;
                else
                    envelope = Mathf.Max(0f, 1f - (noteNorm - 0.1f) / 0.9f);

                // Overall fade-out for last note
                if (norm > 0.8f)
                    envelope *= (1f - norm) / 0.2f;

                float sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;
                sample += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.15f; // octave harmonic

                data[i] = sample * envelope;
            }

            return CreateClip("ConstructionComplete", data);
        }

        private AudioClip GenerateGatherStrike()
        {
            // Dull thud — chopping/mining
            float duration = 0.1f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            uint noiseState = 33333;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;
                float envelope = (1f - norm);
                envelope = envelope * envelope * envelope;

                // Low thud
                float thud = Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.4f;

                // Noisy crack
                noiseState = XorShift(noiseState);
                float noise = ((noiseState & 0xFFFF) / 32768f - 1f) * 0.25f;
                noise *= (1f - norm); // low-pass

                data[i] = (thud + noise) * envelope;
            }

            return CreateClip("GatherStrike", data);
        }

        private AudioClip GenerateChatMessage()
        {
            // Short pleasant two-tone ascending ding
            float duration = 0.15f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;

                // Two-tone ascending: 800 Hz first half, 1100 Hz second half
                float freq = norm < 0.4f ? 800f : 1100f;

                // Fast attack, quick decay
                float envelope;
                if (norm < 0.05f)
                    envelope = norm / 0.05f;
                else
                    envelope = Mathf.Max(0f, 1f - (norm - 0.05f) / 0.95f);
                envelope *= envelope;

                float sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f;
                sample += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.1f; // octave shimmer

                data[i] = sample * envelope;
            }

            return CreateClip("ChatMessage", data);
        }

        private AudioClip GenerateSurrenderVote()
        {
            // Urgent warning tone — two-note descending horn with vibrato
            float duration = 0.35f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            uint noiseState = 77777;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;

                // Descending two-note: 400 Hz first half, 280 Hz second half
                float freq = norm < 0.45f ? 400f : 280f;

                // Slight vibrato
                float vibrato = Mathf.Sin(2f * Mathf.PI * 7f * t) * 6f;

                // Envelope: attack + sustain + decay
                float envelope;
                if (norm < 0.05f)
                    envelope = norm / 0.05f;
                else if (norm < 0.75f)
                    envelope = 1f;
                else
                    envelope = (1f - norm) / 0.25f;

                float sample = Mathf.Sin(2f * Mathf.PI * (freq + vibrato) * t) * 0.45f;
                // Odd harmonics for brass/horn character
                sample += Mathf.Sin(2f * Mathf.PI * (freq + vibrato) * 3f * t) * 0.12f;

                // Short noise burst at the start for urgency
                float noiseMix = Mathf.Max(0f, 1f - norm * 8f);
                noiseMix *= noiseMix;
                noiseState = XorShift(noiseState);
                float noise = ((noiseState & 0xFFFF) / 32768f - 1f) * noiseMix * 0.2f;

                data[i] = (sample + noise) * envelope;
            }

            return CreateClip("SurrenderVote", data);
        }

        private AudioClip GenerateLobbyJoin()
        {
            float duration = 0.2f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;

                // Rising two-tone chime: 600 Hz first half, 900 Hz second half
                float freq = norm < 0.4f ? 600f : 900f;

                float envelope;
                if (norm < 0.05f)
                    envelope = norm / 0.05f;
                else
                    envelope = Mathf.Max(0f, 1f - (norm - 0.05f) / 0.95f);
                envelope *= envelope;

                float sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f;
                sample += Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.12f; // fifth harmonic

                data[i] = sample * envelope;
            }

            return CreateClip("LobbyJoin", data);
        }

        private AudioClip GenerateSheepConvert()
        {
            // Bell ring — metallic chime with harmonics and decay
            float duration = 0.5f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;

                // Bell-like exponential decay
                float envelope = Mathf.Exp(-norm * 5f);

                // Fundamental + inharmonic partials (bell characteristic)
                float fundamental = 880f;
                float sample = Mathf.Sin(2f * Mathf.PI * fundamental * t) * 0.35f;
                sample += Mathf.Sin(2f * Mathf.PI * fundamental * 2.09f * t) * 0.25f;
                sample += Mathf.Sin(2f * Mathf.PI * fundamental * 3.01f * t) * 0.15f;
                sample += Mathf.Sin(2f * Mathf.PI * fundamental * 4.23f * t) * 0.08f;

                // Slight initial attack transient
                float attack = norm < 0.01f ? norm / 0.01f : 1f;

                data[i] = sample * envelope * attack;
            }

            return CreateClip("SheepConvert", data);
        }

        private AudioClip GenerateUnderAttack()
        {
            // Two-note descending warning horn, repeated twice (~0.6s)
            float duration = 0.6f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float norm = (float)i / samples;

                // Two repetitions of the two-note pattern
                // Each rep is 0.3s: 0.15s high note + 0.15s low note
                float repNorm = (norm % 0.5f) * 2f; // 0..1 within each repetition
                float freq = repNorm < 0.5f ? 520f : 390f;

                // Vibrato for urgency
                float vibrato = Mathf.Sin(2f * Mathf.PI * 8f * t) * 6f;

                // Per-note envelope
                float noteNorm = (repNorm % 0.5f) * 2f;
                float envelope;
                if (noteNorm < 0.05f)
                    envelope = noteNorm / 0.05f;
                else if (noteNorm < 0.7f)
                    envelope = 1f;
                else
                    envelope = (1f - noteNorm) / 0.3f;

                // Overall fade at the very end
                if (norm > 0.85f)
                    envelope *= (1f - norm) / 0.15f;

                // Brass-like odd harmonics
                float sample = Mathf.Sin(2f * Mathf.PI * (freq + vibrato) * t) * 0.45f;
                sample += Mathf.Sin(2f * Mathf.PI * (freq + vibrato) * 3f * t) * 0.15f;
                sample += Mathf.Sin(2f * Mathf.PI * (freq + vibrato) * 5f * t) * 0.06f;

                data[i] = sample * envelope;
            }

            return CreateClip("UnderAttack", data);
        }

        private AudioClip CreateClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static uint XorShift(uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
