using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
   
    public Transform objetive;
    public float CameraSpeed= 0.025f;
    public Vector3 displacement;

    private void LateUpdate()
    {
        Vector3 desiredPosition= objetive.position+displacement;
        Vector3 SmoothPosition= Vector3.Lerp(transform.position, desiredPosition, CameraSpeed);
transform.position= SmoothPosition;

    }

}
