using TMPro;
using UnityEngine;

/// <summary>Vincula un NPC/objeto con el objetivo actual. En NPC con Dialogue_NPC,
/// el diálogo llama a ExecuteInteraction() al finalizar. En objetos sueltos se permite E.</summary>
public class QuestTarget : MonoBehaviour
{
    [Tooltip("ID idéntico al targetId del objetivo")]
    public string targetId;
    [Tooltip("TextMeshPro de mundo (NO TextMeshProUGUI del Canvas principal). Debe ser hijo del NPC.")]
    public TMP_Text indicator;
    [Tooltip("Desactivado para NPC con Dialogue_NPC; activado solo para objetos sin diálogo.")]
    public bool handleInputAutomatically = false;
    public KeyCode interactKey = KeyCode.E;
    public bool hideAfterSuccessfulInteraction = false;

    private bool nearby;
    private StoryQuestManager manager;

    // Use the same targetId here and in the quest step. Progress events update the indicator as
    // the available steps change.
    private void Start()
    {
        manager = StoryQuestManager.Instance;
        if (manager != null) manager.OnProgressChanged += UpdateIndicator;
        UpdateIndicator();
    }
    private void OnDestroy()
    {
        if (manager != null) manager.OnProgressChanged -= UpdateIndicator;
    }
    // Disable automatic input when dialogue should submit the interaction on completion instead
    // of on the initial keypress.
    private void Update()
    {
        if (TutorialControles2D.BloqueaInteracciones) return;
        if (handleInputAutomatically && nearby && Input.GetKeyDown(interactKey))
            ExecuteInteraction();
    }
    // The return value reports whether the manager accepted the interaction. Optional hiding
    // occurs only after success.
    public bool ExecuteInteraction()
    {
        if (manager == null) manager = StoryQuestManager.Instance;
        if (manager == null || string.IsNullOrWhiteSpace(targetId)) return false;
        bool succeeded = manager.Interact(targetId);
        if (succeeded && hideAfterSuccessfulInteraction) gameObject.SetActive(false);
        return succeeded;
    }
    public void UpdateIndicator()
    {
        if (indicator == null) return;
        if (manager == null) manager = StoryQuestManager.Instance;
        string symbol = manager == null ? "" : manager.IndicatorFor(targetId);
        indicator.text = symbol;
        indicator.gameObject.SetActive(!string.IsNullOrEmpty(symbol));
    }
    private void OnTriggerEnter2D(Collider2D other) { if (other.CompareTag("Player")) nearby = true; }
    private void OnTriggerExit2D(Collider2D other) { if (other.CompareTag("Player")) nearby = false; }
    private void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) nearby = true; }
    private void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) nearby = false; }
}
