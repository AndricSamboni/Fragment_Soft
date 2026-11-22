using System.Collections.Generic;
using UnityEngine;

// Biblioteca de datos históricos. Reutiliza FactPanel del HUD original.
public class HistoricalJournal : MonoBehaviour
{
    [System.Serializable] public class Fact
    {
        public string id, title;
        [TextArea(2, 8)] public string body;
    }
    [SerializeField] private HUDController hud;
    [SerializeField] private KeyCode openKey = KeyCode.J;
    [SerializeField] private KeyCode previousKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode nextKey = KeyCode.RightArrow;
    private readonly List<Fact> unlocked = new List<Fact>();
    private int position;
    private bool browsing;

    private void Start() { if (hud == null) hud = FindObjectOfType<HUDController>(); }
    private void Update()
    {
        if (TutorialControles2D.BloqueaInteracciones) return;
        if (Input.GetKeyDown(openKey)) OpenJournal();
        if (!browsing || hud == null) return;
        if (!hud.IsFactOpen) { browsing = false; return; }
        if (Input.GetKeyDown(previousKey)) Previous();
        if (Input.GetKeyDown(nextKey)) Next();
    }
    // Use a stable ID for each fact. A new entry is recorded even when an open HUD panel
    // prevents its immediate display.
    public void Unlock(string id, string title, string body, bool displayNow)
    {
        if (string.IsNullOrEmpty(id) || unlocked.Exists(f => f.id == id)) return;
        unlocked.Add(new Fact { id = id, title = title, body = body });
        // Automático solo para los descubrimientos señalados; otros quedan en el diario.
        if (displayNow && hud != null && !hud.IsFactOpen)
            hud.ShowFact(title, body);
    }
    // Open the unlocked collection through the HUD's fact panel. Browsing reuses that panel for
    // each entry.
    public void OpenJournal()
    {
        if (hud == null) return;
        if (unlocked.Count == 0)
        {
            browsing = false;
            hud.ShowFact("¿Sabías que...?", "Todavía no has desbloqueado descubrimientos históricos.");
            return;
        }
        browsing = true;
        position = 0;
        ShowCurrent();
    }
    // Wrap around the collection so the player can browse in either direction from the first or
    // last entry.
    public void Next() { if (unlocked.Count == 0) return; position = (position + 1) % unlocked.Count; ShowCurrent(); }
    public void Previous() { if (unlocked.Count == 0) return; position = (position - 1 + unlocked.Count) % unlocked.Count; ShowCurrent(); }
    private void ShowCurrent()
    {
        if (hud == null || unlocked.Count == 0) return;
        Fact f = unlocked[position];
        hud.ShowFact(f.title + " (" + (position + 1) + "/" + unlocked.Count + ")", f.body + "\n\n← Anterior   |   Siguiente →   |   Enter: cerrar");
    }
}

