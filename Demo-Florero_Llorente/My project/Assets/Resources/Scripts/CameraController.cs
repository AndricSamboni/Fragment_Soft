using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
    
    // The target (player) that the camera will follow
    public Transform objetive;
    // Controls how fast and smooth the camera catches up to the target
    public float CameraSpeed= 0.025f;
    // Offset distance between the target and the camera so it doesn't clip directly on top
    public Vector3 displacement;

    // LateUpdate runs after all Update functions, ideal for cameras to avoid stuttering/jitter
    private void LateUpdate()
    {
        // Calculate where the camera wants to be based on target position and offset
        Vector3 desiredPosition= objetive.position+displacement;
        // Smoothly interpolate (glide) from current camera position to the desired position
        Vector3 SmoothPosition= Vector3.Lerp(transform.position, desiredPosition, CameraSpeed);
        // Apply the smoothed position to the camera
        transform.position= SmoothPosition;

    }

}