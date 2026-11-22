using UnityEngine;

// Si ya tienes un sistema de diálogos: conecta OnDialogueFinished al final del diálogo.
// Assign the NPC's QuestTarget and connect the dialogue system's completion event to
// OnDialogueFinished.
public class QuestDialogueBridge : MonoBehaviour
{
    [SerializeField] private QuestTarget target;
    // The assigned target submits its own ID, allowing this callback to be reused on different
    // NPCs.
    public void OnDialogueFinished()
    {
        if (target != null) target.ExecuteInteraction();
    }
}