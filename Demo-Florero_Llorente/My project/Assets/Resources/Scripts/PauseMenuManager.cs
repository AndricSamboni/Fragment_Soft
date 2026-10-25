using UnityEngine;

public class PauseMenuManager : MonoBehaviour
{
   public GameObject PauseMenu;
   public GameObject PauseButton;

   public void PauseGame()
    {
        Time.timeScale=0;
        PauseButton.SetActive(false);
        PauseMenu.SetActive(true);
    }

public void ResumeGame()
    {
         Time.timeScale=1;
        PauseButton.SetActive(true);
        PauseMenu.SetActive(false);
    }

public void QuitGame()
    {
        Debug.Log("Closed Game");
        Application. Quit();
    }

}
