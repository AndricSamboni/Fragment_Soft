using UnityEngine;
using System;
using System.Collections;
using TMPro;

// Mantiene los nombres de campos originales para no perder referencias del Inspector.
public class Dialogue_NPC : MonoBehaviour
{
    [Header("Interfaz")]
    [SerializeField] private GameObject DialogueMark;
    [SerializeField] private GameObject Dialoguepanel;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField, TextArea(4, 6)] private string[] dialogueLines;
    [SerializeField, Min(0f)] private float typingTime = 0.05f;

    [Header("Misiones")]
    [SerializeField] private QuestTarget questTarget;
    [SerializeField] private bool advanceQuestWhenDialogueEnds = true;
    [Tooltip("Si está activado, no se puede hablar con el NPC cuando no tiene un objetivo disponible.")]
    [SerializeField] private bool onlyTalkDuringQuest = true;

    [Serializable]
    public class QuestDialogue
    {
        public string questId;
        [Tooltip("Indice del objetivo en Steps del StoryQuestManager (comienza en 0).")]
        public int stepIndex;
        [TextArea(3, 6)] public string[] lines;
    }
    [Tooltip("Opcional: diálogos diferentes para cada misión y cada objetivo. Si no hay coincidencia, usa Dialogue Lines.")]
    [SerializeField] private QuestDialogue[] questDialogues;

    private StoryQuestManager manager;
    private bool isPlayerInRange;
    private bool didDialogueStart;
    private int lineIndex;
    private Coroutine typingRoutine;
    private float previousTimeScale = 1f;
    private string[] activeLines;

    private void Awake()
    {
        if (questTarget == null) questTarget = GetComponent<QuestTarget>();
    }

    private void Start()
    {
        manager = StoryQuestManager.Instance;
        if (manager != null) manager.OnProgressChanged += UpdateAvailability;
        if (DialogueMark != null) DialogueMark.SetActive(false);
        // El panel es una referencia compartida: únicamente el diálogo que se abre lo activa.
        // NO desactivar un panel compartido aquí: otros NPC podrían estar hablando.
        UpdateAvailability();
    }

    // Enable onlyTalkDuringQuest to offer dialogue only when this target matches a relevant
    // quest step.
    private bool MissionAvailable()
    {
        if (!onlyTalkDuringQuest) return true;
        if (manager == null) manager = StoryQuestManager.Instance;
        return manager != null && questTarget != null && manager.CanInteractTarget(questTarget.targetId);
    }

    private void UpdateAvailability()
    {
        if (DialogueMark != null && !didDialogueStart)
            DialogueMark.SetActive(isPlayerInRange && MissionAvailable());
    }

    // While a line is typing, E reveals the rest of it. A subsequent press advances the
    // conversation.
    private void Update()
    {
        if (TutorialControles2D.BloqueaInteracciones) return;
        if (!isPlayerInRange || !Input.GetKeyDown(KeyCode.E)) return;
        if (!didDialogueStart)
        {
            if (!MissionAvailable()) return;
            activeLines = SelectLines();
            if (activeLines == null || activeLines.Length == 0) return;
            StartDialogue();
        }
        else if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
            dialogueText.text = activeLines[lineIndex];
        }
        else
        {
            NextDialogueLine();
        }
    }

    // Match both the quest ID and step index so the same NPC can say different things on later
    // visits.
    private string[] SelectLines()
    {
        if (manager != null && questTarget != null &&
            manager.TryGetRelevantStep(questTarget.targetId, out string id, out int step))
        {
            if (questDialogues != null)
                foreach (QuestDialogue dialogue in questDialogues)
                    if (dialogue != null && dialogue.questId == id &&
                        dialogue.stepIndex == step && dialogue.lines != null && dialogue.lines.Length > 0)
                        return dialogue.lines;
        }
        return dialogueLines;
    }

    // Save the current time scale before pausing. Dialogue text uses real time so it can still
    // appear character by character.
    private void StartDialogue()
    {
        if (Dialoguepanel == null || dialogueText == null) return;
        didDialogueStart = true;
        lineIndex = 0;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Dialoguepanel.SetActive(true);
        if (DialogueMark != null) DialogueMark.SetActive(false);
        typingRoutine = StartCoroutine(ShowLine());
    }

    private void NextDialogueLine()
    {
        lineIndex++;
        if (lineIndex < activeLines.Length)
            typingRoutine = StartCoroutine(ShowLine());
        else
            FinishDialogue();
    }

    // Quest progress is submitted here when configured, after the player has finished reading.
    private void FinishDialogue()
    {
        didDialogueStart = false;
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = null;
        if (Dialoguepanel != null) Dialoguepanel.SetActive(false);
        Time.timeScale = previousTimeScale;
        // Solo se contabiliza después de leer TODAS las líneas y cerrar el diálogo.
        if (advanceQuestWhenDialogueEnds && questTarget != null)
            questTarget.ExecuteInteraction();
        UpdateAvailability();
    }

    private IEnumerator ShowLine()
    {
        dialogueText.text = string.Empty;
        foreach (char ch in activeLines[lineIndex])
        {
            dialogueText.text += ch;
            yield return new WaitForSecondsRealtime(typingTime);
        }
        typingRoutine = null;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        isPlayerInRange = true;
        UpdateAvailability();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        isPlayerInRange = false;
        UpdateAvailability();
    }

    private void OnDisable()
    {
        if (didDialogueStart)
        {
            if (Dialoguepanel != null) Dialoguepanel.SetActive(false);
            Time.timeScale = previousTimeScale;
        }
        didDialogueStart = false;
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = null;
        if (DialogueMark != null) DialogueMark.SetActive(false);
    }

    private void OnDestroy()
    {
        if (manager != null) manager.OnProgressChanged -= UpdateAvailability;
    }
}
