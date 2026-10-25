using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Footstep Settings")]
    [Tooltip("Audios de pasos por defecto.")]
    [SerializeField] private List<AudioClip> stepClips = new List<AudioClip>();
    [Tooltip("Tiempo de espera entre cada paso.")]
    [SerializeField] private float stepInterval = 0.4f;

    private AudioSource audioSource;
    private float stepTimer;
    private Rigidbody2D rb2D;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 0f; 
        rb2D = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Detecta si el personaje se está moviendo usando la velocidad del Rigidbody
        bool isMoving = rb2D != null && rb2D.linearVelocity.magnitude > 0.1f;

        if (isMoving)
        {
            stepTimer += Time.deltaTime;

            if (stepTimer >= stepInterval)
            {
                PlayFootstepSound();
                stepTimer = 0f;
            }
        }
        else
        {
            // Reinicia el temporizador para que el primer paso suene inmediatamente al arrancar
            stepTimer = stepInterval;
        }
    }

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