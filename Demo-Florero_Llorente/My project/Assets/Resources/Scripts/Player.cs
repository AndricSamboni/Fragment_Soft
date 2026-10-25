using UnityEngine;

public class Player : MonoBehaviour
{
   
   public float speed= 4f;
   private Rigidbody2D rb2D;
   private Vector2 movementInput;
   private Animator animator;

    void Start()
    {
   rb2D= GetComponent<Rigidbody2D>();   
   animator=GetComponent<Animator>();  
    }

    
    void Update()
    {
        movementInput.x= Input.GetAxisRaw("Horizontal");
        movementInput.y= Input.GetAxisRaw("Vertical");

movementInput= movementInput.normalized;

animator.SetFloat("Horizontal", movementInput.x);
animator.SetFloat("Vertical", movementInput.y);
animator.SetFloat("speed", movementInput.magnitude);

    }

    private void FixedUpdate()
    {
        rb2D.linearVelocity= movementInput*speed;
    }
}
