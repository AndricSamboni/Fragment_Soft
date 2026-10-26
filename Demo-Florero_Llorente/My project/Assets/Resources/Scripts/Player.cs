using UnityEngine;

public class Player : MonoBehaviour
{
   
   public float speed= 4f; // Movement speed multiplier
   private Rigidbody2D rb2D; // Reference to physics component for 2D movement
   private Vector2 movementInput; // Stores horizontal and vertical input axes
   private Animator animator; // Reference to animator component for character sprites/animations

    // Start is called before the first frame update to grab necessary components
    void Start()
    {
    rb2D= GetComponent<Rigidbody2D>();   
    animator=GetComponent<Animator>();  
    }

    
    // Update is called once per frame to read player inputs and update animations
    void Update()
    {
    
        // Capture raw keyboard/controller input (-1, 0, or 1)
        movementInput.x= Input.GetAxisRaw("Horizontal");
        movementInput.y= Input.GetAxisRaw("Vertical");

        // Normalize input vector to prevent faster diagonal movement
        movementInput= movementInput.normalized;

        // Send movement data to the Animator controller to trigger walk/idle animations
        animator.SetFloat("Horizontal", movementInput.x);
        animator.SetFloat("Vertical", movementInput.y);
        animator.SetFloat("speed", movementInput.magnitude);

    }

    // FixedUpdate is used for smooth physics calculations
    private void FixedUpdate()
    {
        // Apply velocity to the Rigidbody2D to move the character
        rb2D.linearVelocity= movementInput*speed;
    }
}
