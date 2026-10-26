using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Collider2D))]
public class ZoneAudioTrigger : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Maximum volume when the player is closest to the center.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 0.8f;
    [Tooltip("Distance at which the audio starts fading in from the edge of the zone.")]
    [SerializeField] private float maxDistance = 5f;

    private AudioSource audioSource;
    private Transform playerTransform;
    private bool isPlayerInside = false;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true; // Background zone sounds usually loop
        audioSource.volume = 0f;
        audioSource.playOnAwake = false;

        // Ensure the collider is set as a Trigger
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void Update()
    {
        // If player is inside the zone, adjust volume dynamically based on distance
        if (isPlayerInside && playerTransform != null)
        {
            // Calculate distance from the player to this object's position
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            // Map the distance to a smooth volume level (closer = louder)
            // When distance is 0, volume is maxVolume. When distance is maxDistance, volume is 0.
            float targetVolume = Mathf.Lerp(maxVolume, 0f, distance / maxDistance);
            
            // Smoothly transition the volume towards the target value
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume, Time.deltaTime * 2f);
        }
    }

    // Triggered when any object enters the zone collider
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the entering object is the player (make sure your player has the "Player" tag)
        if (other.CompareTag("Player"))
        {
            playerTransform = other.transform;
            isPlayerInside = true;

            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
    }

    // Triggered when any object exits the zone collider
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            StartCoroutine(FadeOutAndStop());
        }
    }

    // Coroutine to smoothly fade out audio when leaving the zone area completely
    private System.Collections.IEnumerator FadeOutAndStop()
    {
        // Smoothly fade out when leaving the area completely
        while (audioSource.volume > 0f)
        {
            audioSource.volume -= Time.deltaTime * 2f;
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        playerTransform = null;
    }
}