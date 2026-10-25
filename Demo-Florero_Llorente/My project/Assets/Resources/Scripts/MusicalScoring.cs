using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SimpleAmbientMusic : MonoBehaviour
{
    [Header("Playlist")]
    [Tooltip("Drag your ambient music clips here.")]
    [SerializeField] private List<AudioClip> ambientClips = new List<AudioClip>();

    [Header("Fade Settings")]
    [Tooltip("Duration in seconds for the fade-in and fade-out effect.")]
    [SerializeField] private float fadeDuration = 2f;
    [Tooltip("Maximum volume for the ambient tracks.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 0.8f;

    private AudioSource audioSource;
    private List<AudioClip> shuffledClips = new List<AudioClip>();
    private int currentIndex = 0;

    private void Awake()
    {
        // Automatically get the AudioSource component attached to this object
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // Ensure it plays globally in 2D
    }

    private void Start()
    {
        if (ambientClips == null || ambientClips.Count == 0)
        {
            Debug.LogWarning("SimpleAmbientMusic: No audio clips assigned in the inspector.");
            return;
        }

        ShufflePlaylist();
        StartCoroutine(PlayPlaylistRoutine());
    }

    private void ShufflePlaylist()
    {
        // Randomize the order of tracks to avoid monotony
        shuffledClips = new List<AudioClip>(ambientClips);
        for (int i = shuffledClips.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            AudioClip temp = shuffledClips[i];
            shuffledClips[i] = shuffledClips[randomIndex];
            shuffledClips[randomIndex] = temp;
        }
        currentIndex = 0;
    }

    private IEnumerator PlayPlaylistRoutine()
    {
        while (true)
        {
            if (currentIndex >= shuffledClips.Count)
            {
                ShufflePlaylist();
            }

            AudioClip currentClip = shuffledClips[currentIndex];
            currentIndex++;

            audioSource.clip = currentClip;
            audioSource.volume = 0f;
            audioSource.Play();

            // Smooth Fade In
            yield return StartCoroutine(FadeAudio(0f, maxVolume, fadeDuration));

            // Wait until the clip is almost finished (minus the fade out time)
            float waitTime = currentClip.length - (fadeDuration * 2f);
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }

            // Smooth Fade Out before switching to the next track
            yield return StartCoroutine(FadeAudio(maxVolume, 0f, fadeDuration));

            audioSource.Stop();
        }
    }

    private IEnumerator FadeAudio(float startVolume, float targetVolume, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, timer / duration);
            yield return null;
        }
        audioSource.volume = targetVolume;
    }
}