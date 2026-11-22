using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicalScoring : MonoBehaviour
{
    [Header("Playlist")]
    [Tooltip("Drag your ambient music clips here.")]
    [SerializeField] private List<AudioClip> ambientClips = new List<AudioClip>(); // List to hold background music tracks

    [Header("Fade Settings")]
    [Tooltip("Duration in seconds for the fade-in and fade-out effect.")]
    [SerializeField] private float fadeDuration = 2f; // How long it takes for tracks to fade in or out
    [Tooltip("Maximum volume for the ambient tracks.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 0.8f; // Peak volume level when a song is playing

    private AudioSource audioSource;
    private List<AudioClip> shuffledClips = new List<AudioClip>();
    private int currentIndex = 0;

    // Give the playlist its own AudioSource. This component changes its clip and volume
    // throughout playback.
    private void Awake()
    {
        // Automatically get the AudioSource component attached to this object
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = false; // Disable loop because we manage the playlist order manually
        audioSource.spatialBlend = 0f; // Ensure it plays globally in 2D stereo
    }

    private void Start()
    {
        // Safety check: if no tracks are assigned, log a warning and stop execution
        if (ambientClips == null || ambientClips.Count == 0)
        {
            Debug.LogWarning("SimpleAmbientMusic: No audio clips assigned in the inspector.");
            return;
        }

        // Shuffle tracks randomly and start playing the ambient playlist loop
        ShufflePlaylist();
        StartCoroutine(PlayPlaylistRoutine());
    }

    // Shuffle a working copy so the clip order shown in the Inspector stays intact across
    // playlist cycles.
    private void ShufflePlaylist()
    {
        // Randomize the order of tracks to prevent the same sequence every time
        shuffledClips = new List<AudioClip>(ambientClips);
        for (int i = shuffledClips.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            AudioClip temp = shuffledClips[i];
            shuffledClips[i] = shuffledClips[randomIndex];
            shuffledClips[randomIndex] = temp;
        }
        currentIndex = 0; // Reset index to the beginning of the shuffled list
    }

    private IEnumerator PlayPlaylistRoutine()
    {
        // Infinite loop to keep background ambient music playing continuously
        while (true)
        {
            // If all tracks have been played, reshuffle the playlist for variety
            if (currentIndex >= shuffledClips.Count)
            {
                ShufflePlaylist();
            }

            // Get the next track and advance the index tracker
            AudioClip currentClip = shuffledClips[currentIndex];
            currentIndex++;

            // Assign the clip, set volume to zero for smooth entry, and play
            audioSource.clip = currentClip;
            audioSource.volume = 0f;
            audioSource.Play();

            // Smooth Fade In effect from 0 up to maxVolume
            yield return StartCoroutine(FadeAudio(0f, maxVolume, fadeDuration));

            // Wait until the clip is almost finished playing (minus the fade out time)
            float waitTime = currentClip.length - (fadeDuration * 2f);
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }

            // Smooth Fade Out effect before switching to the next track
            yield return StartCoroutine(FadeAudio(maxVolume, 0f, fadeDuration));

            // Stop the audio source completely before loading the next track
            audioSource.Stop();
        }
    }

    // Fades use scaled frame time, so their progress pauses while the game's time scale is zero.
    private IEnumerator FadeAudio(float startVolume, float targetVolume, float duration)
    {
        float timer = 0f;
        // Gradually transition the volume over time using linear interpolation (Lerp)
        while (timer < duration)
        {
            timer += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, timer / duration);
            yield return null; // Wait for the next frame
        }
        // Ensure volume settles exactly on the target value at the end of the fade
        audioSource.volume = targetVolume;
    }
}