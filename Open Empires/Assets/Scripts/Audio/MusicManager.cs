using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class MusicManager : MonoBehaviour
    {
        private float musicVolume = 0.1f;
        private bool isMuted;

        private float EffectiveVolume => isMuted ? 0f : musicVolume;

        public static MusicManager Instance => instance;

        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                ApplyVolume();
            }
        }

        public bool IsMuted
        {
            get => isMuted;
            set
            {
                isMuted = value;
                ApplyVolume();
            }
        }

        [Header("Crossfade")]
        [SerializeField] private float crossfadeDuration = 2f;

        [Header("Combat")]
        [SerializeField] private float combatCooldown = 8f;
        [SerializeField] private float combatPollInterval = 0.5f;

        private AudioSource sourceA;
        private AudioSource sourceB;
        private AudioSource activeSource;
        private AudioSource outgoingSource;

        private AudioClip[] peacetimeTracks;
        private AudioClip[] wartimeTracks;
        private Dictionary<Civilization, AudioClip[]> civPeacetimeTracks = new Dictionary<Civilization, AudioClip[]>();

        private List<int> peacetimeOrder = new List<int>();
        private List<int> wartimeOrder = new List<int>();
        private int currentIndex;
        private bool isWartime;
        private bool playedCivIntro;

        private float lastCombatTime = -100f;
        private float nextPollTime;

        private bool gameStarted;
        private bool playingMenuMusic;
        private bool crossfading;
        private float crossfadeTimer;

        private UnitSelectionManager selectionManager;

        private static MusicManager instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance != null) return;

            var go = new GameObject("MusicManager");
            instance = go.AddComponent<MusicManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            sourceA = gameObject.AddComponent<AudioSource>();
            sourceB = gameObject.AddComponent<AudioSource>();

            sourceA.playOnAwake = false;
            sourceB.playOnAwake = false;
            sourceA.loop = false;
            sourceB.loop = false;

            activeSource = sourceA;
            outgoingSource = sourceB;

            peacetimeTracks = Resources.LoadAll<AudioClip>("Music/Peacetime/Generic");
            wartimeTracks = Resources.LoadAll<AudioClip>("Music/Wartime");

            // Load civilization-specific peacetime tracks
            civPeacetimeTracks[Civilization.English] = Resources.LoadAll<AudioClip>("Music/Peacetime/English");
            civPeacetimeTracks[Civilization.French] = Resources.LoadAll<AudioClip>("Music/Peacetime/French");
            civPeacetimeTracks[Civilization.HolyRomanEmpire] = Resources.LoadAll<AudioClip>("Music/Peacetime/HolyRomanEmpire");

            if (peacetimeTracks.Length == 0)
            {
                Debug.LogWarning("MusicManager: No peacetime tracks found in Resources/Music/Peacetime/Generic");
            }
            if (wartimeTracks.Length == 0)
            {
                Debug.LogWarning("MusicManager: No wartime tracks found in Resources/Music/Wartime");
            }
        }

        private void Start()
        {
            ShufflePlaylist(peacetimeOrder, peacetimeTracks.Length);
            ShufflePlaylist(wartimeOrder, wartimeTracks.Length);

            isWartime = false;
            currentIndex = 0;
        }

        private void Update()
        {
            // Keep crossfade running even during menu music
            UpdateCrossfade();

            if (!gameStarted)
            {
                if (GameBootstrapper.Instance?.Simulation != null)
                {
                    gameStarted = true;
                    playedCivIntro = false;

                    if (playingMenuMusic && activeSource.isPlaying)
                    {
                        // Menu music is already the civ track — stop looping, let it finish, then advance to generic
                        activeSource.loop = false;
                        playedCivIntro = true;
                        playingMenuMusic = false;
                    }
                    else
                    {
                        // No menu music — try to play civ-specific intro track
                        AudioClip introClip = GetCivIntroClip();
                        if (introClip != null)
                        {
                            playedCivIntro = true;
                            StartTrack(introClip, fadeIn: true);
                        }
                        else if (peacetimeTracks.Length > 0)
                        {
                            StartTrack(peacetimeTracks[peacetimeOrder[0]], fadeIn: true);
                        }
                    }
                    playingMenuMusic = false;
                }
                return;
            }

            if (Time.time >= nextPollTime)
            {
                nextPollTime = Time.time + combatPollInterval;
                PollCombatState();
            }

            UpdateCrossfade();
            UpdateAutoAdvance();
        }

        private void PollCombatState()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            if (selectionManager == null)
            {
                selectionManager = FindFirstObjectByType<UnitSelectionManager>();
                if (selectionManager == null) return;
            }

            int localPlayer = selectionManager.LocalPlayerId;
            bool inCombat = false;

            // Check if any of the player's units are actively fighting
            var units = sim.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.PlayerId != localPlayer) continue;

                if (u.State == UnitState.InCombat)
                {
                    inCombat = true;
                    break;
                }
            }

            if (inCombat)
            {
                lastCombatTime = Time.time;
            }

            bool shouldBeWartime = inCombat || (Time.time - lastCombatTime) < combatCooldown;

            if (shouldBeWartime && !isWartime && wartimeTracks.Length > 0)
            {
                isWartime = true;
                ShufflePlaylist(wartimeOrder, wartimeTracks.Length);
                currentIndex = 0;
                CrossfadeToTrack(wartimeTracks[wartimeOrder[0]]);
            }
            else if (!shouldBeWartime && isWartime && peacetimeTracks.Length > 0)
            {
                isWartime = false;
                ShufflePlaylist(peacetimeOrder, peacetimeTracks.Length);
                currentIndex = 0;
                CrossfadeToTrack(peacetimeTracks[peacetimeOrder[0]]);
            }
        }

        private void UpdateAutoAdvance()
        {
            if (crossfading) return;
            if (!activeSource.isPlaying) return;

            float timeRemaining = activeSource.clip.length - activeSource.time;
            if (timeRemaining <= crossfadeDuration)
            {
                AdvanceToNextTrack();
            }
        }

        private void AdvanceToNextTrack()
        {
            // After civ intro finishes, start the generic peacetime playlist
            if (playedCivIntro && !isWartime)
            {
                playedCivIntro = false;
                if (peacetimeTracks.Length > 0)
                {
                    ShufflePlaylist(peacetimeOrder, peacetimeTracks.Length);
                    currentIndex = 0;
                    CrossfadeToTrack(peacetimeTracks[peacetimeOrder[0]]);
                    return;
                }
            }

            var tracks = isWartime ? wartimeTracks : peacetimeTracks;
            var order = isWartime ? wartimeOrder : peacetimeOrder;

            if (tracks.Length == 0) return;

            currentIndex++;
            if (currentIndex >= order.Count)
            {
                ShufflePlaylist(order, tracks.Length);
                currentIndex = 0;
            }

            CrossfadeToTrack(tracks[order[currentIndex]]);
        }

        private AudioClip GetCivIntroClip()
        {
            if (selectionManager == null)
                selectionManager = FindFirstObjectByType<UnitSelectionManager>();

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || selectionManager == null) return null;

            Civilization civ = sim.GetPlayerCivilization(selectionManager.LocalPlayerId);
            if (civPeacetimeTracks.TryGetValue(civ, out var clips) && clips.Length > 0)
            {
                return clips[Random.Range(0, clips.Length)];
            }
            return null;
        }

        private void ApplyVolume()
        {
            float vol = EffectiveVolume;
            if (activeSource != null) activeSource.volume = crossfading ? activeSource.volume : vol;
            if (outgoingSource != null && !crossfading) outgoingSource.volume = 0f;
        }

        private void StartTrack(AudioClip clip, bool fadeIn)
        {
            activeSource.clip = clip;
            activeSource.volume = fadeIn ? 0f : EffectiveVolume;
            activeSource.Play();

            if (fadeIn)
            {
                crossfading = true;
                crossfadeTimer = 0f;
                outgoingSource.volume = 0f;
            }
        }

        private void CrossfadeToTrack(AudioClip clip)
        {
            // Swap sources
            var temp = activeSource;
            activeSource = outgoingSource;
            outgoingSource = temp;

            activeSource.clip = clip;
            activeSource.volume = 0f;
            activeSource.Play();

            crossfading = true;
            crossfadeTimer = 0f;
        }

        private void UpdateCrossfade()
        {
            if (!crossfading) return;

            crossfadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(crossfadeTimer / crossfadeDuration);

            activeSource.volume = t * EffectiveVolume;
            outgoingSource.volume = (1f - t) * EffectiveVolume;

            if (t >= 1f)
            {
                crossfading = false;
                outgoingSource.Stop();
                outgoingSource.clip = null;
            }
        }

        public void PlayMenuMusic(Civilization civ)
        {
            if (gameStarted) return;

            if (civPeacetimeTracks.TryGetValue(civ, out var clips) && clips.Length > 0)
            {
                var clip = clips[Random.Range(0, clips.Length)];

                // Don't restart if already playing this clip
                if (activeSource.isPlaying && activeSource.clip == clip) return;

                if (playingMenuMusic && activeSource.isPlaying)
                {
                    CrossfadeToTrack(clip);
                    activeSource.loop = true;
                }
                else
                {
                    StartTrack(clip, fadeIn: true);
                    activeSource.loop = true;
                }
                playingMenuMusic = true;
            }
        }

        public void Stop()
        {
            sourceA.Stop();
            sourceB.Stop();
            sourceA.clip = null;
            sourceB.clip = null;
            activeSource = sourceA;
            outgoingSource = sourceB;
            gameStarted = false;
            playingMenuMusic = false;
            playedCivIntro = false;
            crossfading = false;
            crossfadeTimer = 0f;
            isWartime = false;
            currentIndex = 0;
            lastCombatTime = -100f;
            selectionManager = null;
        }

        private void ShufflePlaylist(List<int> order, int count)
        {
            order.Clear();
            for (int i = 0; i < count; i++)
                order.Add(i);

            // Fisher-Yates shuffle
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }
        }
    }
}
