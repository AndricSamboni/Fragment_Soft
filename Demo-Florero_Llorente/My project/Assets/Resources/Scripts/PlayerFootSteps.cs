using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Footstep Settings")]
    [Tooltip("Audios de pasos por defecto.")]
    [SerializeField] private List<AudioClip> stepClips = new List<AudioClip>(); // List of audio clips for default footsteps
    [Tooltip("Tiempo de espera entre cada paso.")]
    [SerializeField] private float stepInterval = 0.4f; // Time delay between each footstep sound

    private AudioSource audioSource;
    private float stepTimer;
    private Rigidbody2D rb2D;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 0f; // Set sound to 2D 
        rb2D = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Detects if the character is moving using the Rigidbody velocity threshold
        bool isMoving = rb2D != null && rb2D.linearVelocity.magnitude > 0.1f;

        if (isMoving)
        {
            stepTimer += Time.deltaTime;

            // When timer reaches interval, play a step sound and reset timer
            if (stepTimer >= stepInterval)
            {
                PlayFootstepSound();
                stepTimer = 0f;
            }
        }
        else
        {
            // Reinicia el temporizador para que el primer paso suene inmediatamente al arrancar
            stepTimer = stepInterval; // Reset timer so the first footstep plays immediately when movement starts
        }
    }

    // Plays a random footstep audio clip with slight pitch variation for realism
    private void PlayFootstepSound()
    {
        if (stepClips != null && stepClips.Count > 0)
        {
            AudioClip clip = stepClips[Random.Range(0, stepClips.Count)];
            
            audioSource.pitch = Random.Range(0.9f, 1.1f); // Pequeña variación de tono para que sea natural
            audioSource.PlayOneShot(clip);
        }
    }
}