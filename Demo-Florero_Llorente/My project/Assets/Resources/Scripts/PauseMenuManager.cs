using UnityEngine;

// Assign both UI objects, then connect the pause, resume and quit buttons to the matching public
// methods.
public class PauseMenuManager : MonoBehaviour
{
   public GameObject PauseMenu; // Reference to the pause menu UI panel
   public GameObject PauseButton; // Reference to the button that opens the pause menu

   // Pauses the game time and swaps UI visibility
   public void PauseGame()
    {
        Time.timeScale=0; // Freezes game time
        PauseButton.SetActive(false); // Hides pause button
        PauseMenu.SetActive(true); // Shows pause menu
    }

    // Resumes the game time and restores UI visibility
    // Resuming sets timeScale to one, including when the game used a different speed before the
    // pause.
    public void ResumeGame()
    {
         Time.timeScale=1; // Restores normal game time speed
        PauseButton.SetActive(true); // Shows pause button again
        PauseMenu.SetActive(false); // Hides pause menu
    }

    // Closes the application or stops play mode in the editor
    public void QuitGame()
    {
        Debug.Log("Closed Game");
        Application. Quit();
    }

}
