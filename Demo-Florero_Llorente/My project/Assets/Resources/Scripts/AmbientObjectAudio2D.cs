using UnityEngine;

/// <summary>
/// Periodically plays one-shot ambient audio clips from a world object at randomized intervals.
/// Supports single or multiple clips without repeating the previous clip consecutively when multiple exist.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class AmbientObjectAudio2D : MonoBehaviour
{
    // Assign clips on the world object and set the desired spatial blend on its AudioSource.
    [Header("Audio Clips")]
    [Tooltip("One or more clips to play. When multiple clips are assigned, one is chosen randomly per trigger.")]
    [SerializeField] private AudioClip[] audioClips;

    [Header("Randomized Interval (Seconds)")]
    [Tooltip("Lower bound in seconds for the next random delay between playback cycles.")]
    [SerializeField, Min(0.1f)] private float minDelay = 5f;

    [Tooltip("Upper bound in seconds for the next random delay (e.g., 15 or 20 seconds).")]
    [SerializeField, Min(0.1f)] private float maxDelay = 20f;

    [Tooltip("When enabled, starts the initial countdown as soon as the object becomes active.")]
    [SerializeField] private bool playOnStart = true;

    [Header("Optional Pitch Variation")]
    [Tooltip("Random pitch range applied per one-shot playback to avoid repetitive sound.")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Header("AudioSource Reference")]
    [Tooltip("Target AudioSource used for spatial/2D one-shot playback; auto-resolved if left empty.")]
    [SerializeField] private AudioSource audioSource;

    // Countdown timer (in seconds) until the next audio trigger
    private float timer;

    // Tracks the last played array index so multi-clip pools avoid immediate back-to-back repeats
    private int lastClipIndex = -1;

    /// <summary>
    /// Caches the required AudioSource component, normalizes delay bounds, and seeds the first timer.
    /// </summary>
    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Prevent invalid inverted delay intervals if configured backwards in the Inspector
        if (maxDelay < minDelay)
            maxDelay = minDelay;

        ResetTimer();
    }

    /// <summary>
    /// Resets the countdown whenever the object is enabled if playOnStart is active.
    /// </summary>
    private void OnEnable()
    {
        if (playOnStart)
            ResetTimer();
    }

    /// <summary>
    /// Advances the countdown each frame and triggers a random audio clip once elapsed.
    /// </summary>
    private void Update()
    {
        // Awake seeds the timer even with playOnStart disabled. That option controls whether
        // enabling the object restarts the delay.
        // Skip processing if there are no clips or no AudioSource available
        if (audioClips == null || audioClips.Length == 0 || audioSource == null)
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            PlayRandomClip();
            ResetTimer();
        }
    }

    /// <summary>
    /// Picks a new randomized wait duration between minDelay and maxDelay.
    /// </summary>
    private void ResetTimer()
    {
        float min = Mathf.Max(0.1f, minDelay);
        float max = Mathf.Max(min, maxDelay);
        timer = Random.Range(min, max);
    }

    /// <summary>
    /// Selects a valid clip, temporarily applies a randomized pitch within pitchRange,
    /// and plays it via PlayOneShot without interrupting overlapping sounds.
    /// </summary>
    private void PlayRandomClip()
    {
        AudioClip clipToPlay = PickClip();
        if (clipToPlay == null)
            return;

        float originalPitch = audioSource.pitch;
        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clipToPlay);
        audioSource.pitch = originalPitch;
    }

    /// <summary>
    /// Safely resolves a non-null AudioClip from audioClips.
    /// Returns the sole valid clip immediately if only one exists, or picks a random clip
    /// while attempting to avoid repeating lastClipIndex when multiple clips are present.
    /// </summary>
    private AudioClip PickClip()
    {
        // Count non-null entries to handle empty Inspector slots safely
        int validCount = 0;
        for (int i = 0; i < audioClips.Length; i++)
        {
            if (audioClips[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        // Fast path for single-clip setups: no randomization or repeat filtering needed
        if (validCount == 1)
        {
            for (int i = 0; i < audioClips.Length; i++)
            {
                if (audioClips[i] != null)
                    return audioClips[i];
            }
        }

        // Multi-clip selection: try up to 20 times to pick a valid clip different from lastClipIndex
        int index;
        int safety = 0;
        do
        {
            index = Random.Range(0, audioClips.Length);
            safety++;
        } while ((audioClips[index] == null || index == lastClipIndex) && safety < 20);

        // Fallback scan in case random sampling hit null slots during the retry window
        if (audioClips[index] == null)
        {
            for (int i = 0; i < audioClips.Length; i++)
            {
                if (audioClips[i] != null)
                {
                    index = i;
                    break;
                }
            }
        }

        lastClipIndex = index;
        return audioClips[index];
    }

    /// <summary>
    /// Public hook for external scripts or events to immediately play a clip and reset the interval timer.
    /// </summary>
    public void TriggerNow()
    {
        PlayRandomClip();
        ResetTimer();
    }
}