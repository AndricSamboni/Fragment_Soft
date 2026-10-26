using UnityEngine;
using System.Collections;
using TMPro;

public class Dialogue_NPC : MonoBehaviour
{
   // UI elements and text references for the dialogue system
   [SerializeField] private GameObject DialogueMark; // Icon shown when player can talk to NPC
   [SerializeField] private GameObject Dialoguepanel; // Panel box displaying the dialogue
   [SerializeField] private TMP_Text dialogueText; // Text component where letters appear

   [SerializeField, TextArea(4,6)]private string [] dialogueLines; // Array of dialogue sentences
   private float typingTime=0.05f; // Speed of the typing letter-by-letter effect
   private bool isPlayerInRange; // Checks if the player is close enough to interact
   private bool didDialogueStart; // Tracks if a conversation is currently active
   private int lineIndex; // Tracks which line of dialogue is currently showing

    // Update is called once per frame to check for player input
    void Update()
    {
        // If player is close and presses the 'E' key
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        {
            // If dialogue hasn't started yet, begin it
            if (!didDialogueStart)
            {
               StartDialogue(); 
            }
            // If the current line has finished typing, go to the next line
            else if(dialogueText.text==dialogueLines[lineIndex])
            {
                NextDialogueLine();
            }
            // If player presses 'E' while text is still typing, instantly skip to full line text
            else
            {
                StopAllCoroutines();
                dialogueText.text=dialogueLines[lineIndex];
            }
            
        }
    }

    // Initializes and opens the dialogue interface, pausing the game time
    private void StartDialogue()
    {
        didDialogueStart= true;
        Dialoguepanel.SetActive(true);
        DialogueMark.SetActive(false);
        lineIndex=0;
        Time.timeScale=0f; // Pauses gameplay while talking
        StartCoroutine(ShowLine());
        
    }

    // Advances to the next line or closes dialogue if it's the end
    private void NextDialogueLine()
    {
      lineIndex++;
      if(lineIndex<dialogueLines.Length)
        {
            StartCoroutine(ShowLine());
        }  
        else
        {
            didDialogueStart=false;
            Dialoguepanel.SetActive(false);
            DialogueMark.SetActive(true);
            Time.timeScale=1f; // Resumes gameplay when dialogue ends
        }
    }

    // Coroutine that creates a typewriter effect, printing text character by character
    private IEnumerator ShowLine()
    {
        dialogueText.text=string.Empty;
        foreach(char ch in dialogueLines[lineIndex])
        {
            dialogueText.text+=ch;
            yield return new WaitForSecondsRealtime(typingTime); // Realtime ignores timeScale=0 pause
        }
    }

    // Detects when the player enters the NPC's interaction zone
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("Player"))
        {
            isPlayerInRange=true;
            DialogueMark.SetActive(true);
            Debug.Log("Se puede iniciar dialogo");
        }
    }
         // Detects when the player leaves the NPC's interaction zone
         private void OnTriggerExit2D(Collider2D collision)
    {
         if(collision.gameObject.CompareTag("Player")){
        isPlayerInRange=false;
            DialogueMark.SetActive(false);
             Debug.Log("No se puede iniciar dialogo");
    }
    }
    
}