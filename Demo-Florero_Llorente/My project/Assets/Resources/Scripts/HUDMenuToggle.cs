using UnityEngine;

// Controla visibilidad del panel de inventario sin cambiar el script del ZIP.
// Place this component on an object that stays active when the inventory panel is hidden, so the
// key can reopen it.
public class HUDMenuToggle : MonoBehaviour
{
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private KeyCode inventoryKey = KeyCode.I;
    [SerializeField] private bool startWithInventoryVisible = true;
    private void Start()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(startWithInventoryVisible);
    }
    private void Update()
    {
        if (Input.GetKeyDown(inventoryKey)) ToggleInventory();
    }
    // A UI button can call the same toggle method as the configured inventory key.
    public void ToggleInventory()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(!inventoryPanel.activeSelf);
    }
}