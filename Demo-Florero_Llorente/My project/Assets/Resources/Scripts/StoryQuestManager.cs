using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Complementa HUDController del ZIP. Desactivar el MissionManager original.
public class StoryQuestManager : MonoBehaviour
{
    public static StoryQuestManager Instance { get; private set; }

    [Serializable] public class Step
    {
        public string targetId;                  // ID del NPC o del objeto
        public string objective;                 // Texto visible
        public string requiredItem;              // Si existe, se exige en inventario
        public string giveItem;                  // Se agrega tras interactuar
        public bool consumeRequiredItem = true;
    }

    [Serializable] public class Quest
    {
        public string id;
        public string title;
        public bool mainQuest = true;
        public string unlockAfterQuestId;       // Vacío: disponible desde el inicio
        public List<Step> steps = new List<Step>();
        [TextArea(2, 5)] public string learning;
        public string factId;
        [Tooltip("Si se desmarca, el dato queda en el diario (J) y no interrumpe la escena.")]
        public bool showFactAutomatically = false;
        public string factTitle;
        [TextArea(2, 6)] public string factBody;
    }

    [Header("Referencias")]
    [SerializeField] private HUDController hud;
    [SerializeField] private HistoricalJournal journal;
    [Header("Misiones")]
    [SerializeField] private List<Quest> quests = new List<Quest>();
    [Header("Inicio")]
    [SerializeField] private bool createExamplesIfEmpty = true;
    [SerializeField] private string firstMainQuestId = "M01";

    private readonly Dictionary<string, int> progress = new Dictionary<string, int>();
    private readonly HashSet<string> active = new HashSet<string>();
    private readonly HashSet<string> completed = new HashSet<string>();
    private readonly HashSet<string> items = new HashSet<string>();
    public event Action OnProgressChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (createExamplesIfEmpty && quests.Count == 0) BuildExampleQuests();
    }

    private void Start()
    {
        if (hud == null) hud = FindObjectOfType<HUDController>();
        if (journal == null) journal = FindObjectOfType<HistoricalJournal>();
        StartQuest(firstMainQuestId);
    }

    // Solo permite hablar con NPC que tenga un objetivo realmente vigente.
    // Permite detectar el primer paso de una secundaria desbloqueada aún sin iniciar.
    // Dialogue selection checks active steps first, then the opening step of an eligible side
    // quest.
    public bool TryGetRelevantStep(string targetId, out string questId, out int stepIndex)
    {
        questId = null;
        stepIndex = -1;
        if (string.IsNullOrWhiteSpace(targetId)) return false;
        // La misma prioridad que Interact: principal antes que secundarias.
        foreach (Quest q in quests.OrderByDescending(q => q.mainQuest))
        {
            if (!active.Contains(q.id)) continue;
            if (!progress.TryGetValue(q.id, out int p) || p >= q.steps.Count) continue;
            if (q.steps[p].targetId != targetId) continue;
            questId = q.id;
            stepIndex = p;
            return true;
        }
        foreach (Quest q in quests)
        {
            if (q.mainQuest || active.Contains(q.id) || completed.Contains(q.id) || q.steps.Count == 0) continue;
            if (!string.IsNullOrEmpty(q.unlockAfterQuestId) && !completed.Contains(q.unlockAfterQuestId)) continue;
            if (q.steps[0].targetId != targetId) continue;
            questId = q.id;
            stepIndex = 0;
            return true;
        }
        return false;
    }

    public bool CanInteractTarget(string targetId)
    {
        return TryGetRelevantStep(targetId, out _, out _);
    }

    /// <summary>Objetivo pendiente de la misión PRINCIPAL, para la flecha de navegación.</summary>
    // The guidance arrow uses the current main step's target ID to find the corresponding object
    // in the scene.
    public bool TryGetCurrentMainTarget(out string targetId, out string objective)
    {
        targetId = null;
        objective = null;
        foreach (Quest q in quests)
        {
            if (!q.mainQuest || !active.Contains(q.id)) continue;
            if (!progress.TryGetValue(q.id, out int p) || p < 0 || p >= q.steps.Count) continue;
            Step step = q.steps[p];
            targetId = step.targetId;
            objective = step.objective;
            return !string.IsNullOrWhiteSpace(targetId);
        }
        return false;
    }

    public bool IsActive(string id) => active.Contains(id);
    public bool IsCompleted(string id) => completed.Contains(id);
    public bool HasItem(string id) => items.Contains(id);
    public List<string> GetInventoryItems()
    {
        var result = new List<string>(items);
        result.Sort(StringComparer.CurrentCulture);
        return result;
    }

    // Use unlockAfterQuestId to gate a quest on completion of an earlier quest.
    public bool StartQuest(string id)
    {
        Quest q = quests.Find(x => x.id == id);
        if (q == null || active.Contains(id) || completed.Contains(id)) return false;
        if (!string.IsNullOrEmpty(q.unlockAfterQuestId) && !completed.Contains(q.unlockAfterQuestId)) return false;
        active.Add(id);
        progress[id] = 0;
        Refresh();
        return true;
    }

    // Se llama desde el NPC/objeto que está marcado en cada objetivo.
    // A target interaction can start a side quest and advance its opening step on the same call.
    public bool Interact(string targetId)
    {
        // Permite iniciar misiones secundarias opcionales al hablar con su primer NPC.
        foreach (Quest q in quests)
        {
            if (q.mainQuest || active.Contains(q.id) || completed.Contains(q.id) || q.steps.Count == 0) continue;
            if (q.steps[0].targetId == targetId) StartQuest(q.id);
        }

        // Priorizar historia principal, luego secundarias.
        foreach (Quest q in quests.OrderByDescending(q => q.mainQuest))
        {
            if (!active.Contains(q.id)) continue;
            int p = progress[q.id];
            if (p >= q.steps.Count) continue;
            Step s = q.steps[p];
            if (s.targetId != targetId) continue;
            if (!string.IsNullOrEmpty(s.requiredItem) && !items.Contains(s.requiredItem))
            {
                Debug.Log("Falta el objeto: " + s.requiredItem);
                return false;
            }
            if (!string.IsNullOrEmpty(s.requiredItem) && s.consumeRequiredItem)
            {
                items.Remove(s.requiredItem);
                if (hud != null) hud.RemoveItem(s.requiredItem);
            }
            if (!string.IsNullOrEmpty(s.giveItem) && items.Add(s.giveItem))
                if (hud != null) hud.AddItem(s.giveItem);
            progress[q.id] = p + 1;
            if (progress[q.id] >= q.steps.Count) Finish(q);
            else Refresh();
            return true;
        }
        return false;
    }

    // An active target displays a question mark on its final step and an exclamation mark on
    // earlier steps.
    public string IndicatorFor(string targetId)
    {
        foreach (Quest q in quests.OrderByDescending(q => q.mainQuest))
        {
            if (!active.Contains(q.id)) continue;
            if (progress[q.id] < q.steps.Count && q.steps[progress[q.id]].targetId == targetId)
                return progress[q.id] == q.steps.Count - 1 ? "?" : "!";
        }
        foreach (Quest q in quests)
        {
            if (q.mainQuest || active.Contains(q.id) || completed.Contains(q.id) || q.steps.Count == 0) continue;
            if (!string.IsNullOrEmpty(q.unlockAfterQuestId) && !completed.Contains(q.unlockAfterQuestId)) continue;
            if (q.steps[0].targetId == targetId) return "!";
        }
        return "";
    }

    // Completion records journal entries and can start the next main quest linked to this quest
    // ID.
    private void Finish(Quest q)
    {
        active.Remove(q.id);
        completed.Add(q.id);
        if (journal != null)
        {
            if (!string.IsNullOrWhiteSpace(q.learning))
                journal.Unlock(q.id + "_aprendizaje", "Aprendizaje: " + q.title, q.learning, false);
            if (!string.IsNullOrWhiteSpace(q.factBody))
                journal.Unlock(string.IsNullOrEmpty(q.factId) ? q.id : q.factId,
                    string.IsNullOrEmpty(q.factTitle) ? "¿Sabías que...?" : q.factTitle,
                    q.factBody, q.showFactAutomatically);
        }
        else if (hud != null && q.showFactAutomatically && !string.IsNullOrWhiteSpace(q.factBody))
            hud.ShowFact(q.factTitle, q.factBody);

        // Desbloquea solo la siguiente principal dependiente de esta.
        Quest next = quests.Find(x => x.mainQuest && x.unlockAfterQuestId == q.id && !completed.Contains(x.id));
        if (next != null) StartQuest(next.id);
        Refresh();
    }

    // Publish progress after updating objective text so subscribed indicators and inventory
    // views can refresh.
    public void Refresh()
    {
        if (hud != null)
        {
            List<string> lines = new List<string>();
            foreach (Quest q in quests)
            {
                if (!active.Contains(q.id)) continue;
                int p = progress[q.id];
                if (p < q.steps.Count)
                    lines.Add((q.mainQuest ? "[Principal] " : "[Secundaria] ") + q.title + "\n• " + q.steps[p].objective);
            }
            hud.SetMission(lines.Count > 0 ? string.Join("\n\n", lines) : "Recorrido completado");
        }
        OnProgressChanged?.Invoke();
    }

    private static Step S(string target, string text, string required = "", string give = "")
        => new Step { targetId = target, objective = text, requiredItem = required, giveItem = give };

    private void BuildExampleQuests()
    {
        quests = new List<Quest>
        {
            new Quest { id="M01", title="Un pueblo inconforme", steps=new List<Step>{
                S("comerciante_inicio", "Habla con el comerciante y recoge la carta", "", "Carta del comerciante"),
                S("comerciante_mercado", "Entrega la carta al comerciante del mercado", "Carta del comerciante"),
                S("ciudadano_mercado", "Escucha al ciudadano sobre los criollos") },
                learning="Los criollos buscaban mayor participación política en el gobierno colonial." },
            new Quest { id="M02", title="El famoso florero", unlockAfterQuestId="M01", steps=new List<Step>{
                S("comerciante_inicio", "Recibe el paquete destinado a la tienda", "", "Paquete de documentos"),
                S("comerciante_tienda", "Entrega los documentos cerca de la tienda de Llorente", "Paquete de documentos"),
                S("testigo_florero", "Pregunta para qué se solicitó el florero") },
                learning="El recibimiento de Villavicencio sirvió de pretexto para el incidente político.",
                factId="villavicencio", factTitle="¿Sabías que...? Antonio Villavicencio",
                factBody="El recibimiento de Antonio Villavicencio fue utilizado como motivo para solicitar un adorno a José González Llorente." },
            new Quest { id="M03", title="El conflicto estalla", unlockAfterQuestId="M02", steps=new List<Step>{
                S("comerciante_inicio", "Recoge la carta para la plaza", "", "Carta para la plaza"),
                S("testigo_discusion", "Presencia la discusión y habla con un testigo"),
                S("habitante_plaza", "Sigue a los habitantes y pregunta qué ocurre en la plaza", "Carta para la plaza") },
                learning="El altercado fue un detonante de la protesta popular del 20 de julio de 1810." },
            new Quest { id="M04", title="El pueblo exige respuestas", unlockAfterQuestId="M03", steps=new List<Step>{
                S("ciudadano_cabildo", "Recibe la nota destinada al Cabildo", "", "Nota del Cabildo"),
                S("funcionario_cabildo", "Entrega la nota al funcionario", "Nota del Cabildo"),
                S("habitante_plaza", "Escucha las exigencias de los habitantes") },
                learning="La movilización impulsó la conformación de una Junta de Gobierno." },
            new Quest { id="M05", title="Un cambio para la historia", unlockAfterQuestId="M04", steps=new List<Step>{
                S("comerciante_inicio", "Recoge el último paquete", "", "Paquete final"),
                S("funcionario_cabildo", "Entrega el paquete en el Cabildo", "Paquete final"),
                S("acta_1810", "Examina el acta del 20 de julio") },
                learning="La Junta fue un paso decisivo, pero la independencia no se consiguió definitivamente aquel día.",
                factId="boyaca", factTitle="¿Sabías que...? La independencia",
                factBody="El proceso de independencia continuó durante años. La Batalla de Boyacá del 7 de agosto de 1819 fue decisiva para la Nueva Granada." },
            new Quest { id="S01", title="Noticias que vienen de lejos", mainQuest=false,
                steps=new List<Step>{S("cura_callejon", "Habla con el cura sobre la situación de España"),
                S("documento_1808", "Encuentra el documento antiguo en el callejón")},
                learning="La crisis de la monarquía española de 1808 repercutió en los territorios americanos.",
                factId="espana_1808", factTitle="¿Sabías que...? Crisis de 1808",
                factBody="En 1808, la intervención napoleónica provocó una crisis de autoridad en la monarquía española." },
            new Quest { id="S02", title="Voces de la ciudad", mainQuest=false,
                steps=new List<Step>{S("mujer_mercado", "Ayuda a la vendedora a encontrar su paquete"),
                S("paquete_perdido", "Encuentra el paquete perdido", "", "Paquete perdido"),
                S("mujer_mercado", "Devuelve el paquete a su dueña", "Paquete perdido")},
                learning="Los distintos grupos de la sociedad colonial vivían los problemas políticos de formas diferentes." },
            new Quest { id="S03", title="El secreto del callejón", mainQuest=false,
                steps=new List<Step>{S("anciano_callejon", "Habla con el anciano"),
                S("baul_documentos", "Revisa el baúl", "", "Carta antigua"),
                S("anciano_callejon", "Entrega la carta al anciano", "Carta antigua")},
                learning="Los documentos y testimonios ayudan a reconstruir los acontecimientos del pasado." }
        };
    }
}
