using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ventana de inventario con tarjetas para objetos del StoryQuestManager.
/// Crea su UI bajo el Canvas indicado, sin modificar el HUDSystem.prefab.
/// </summary>
public class InventoryWindowUI : MonoBehaviour
{
    [Serializable] public class ItemIcon
    {
        public string itemName;
        public Sprite icon;
    }
    [Header("Referencias")]
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private StoryQuestManager questManager;
    [SerializeField] private List<ItemIcon> itemIcons = new List<ItemIcon>();
    [SerializeField] private KeyCode toggleKey = KeyCode.I;
    [SerializeField] private bool openAtStart = false;

    private GameObject window;
    private RectTransform gridRoot;
    private TMP_Text emptyLabel;

    // Assign a Canvas or let the component locate one. The inventory window and its slots are
    // created at runtime.
    private void Start()
    {
        if (questManager == null) questManager = StoryQuestManager.Instance;
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) parentCanvas = FindObjectOfType<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogError("InventoryWindowUI: falta asignar un Canvas.");
            enabled = false;
            return;
        }
        CreateWindow();
        if (questManager != null) questManager.OnProgressChanged += Refresh;
        Refresh();
        window.SetActive(openAtStart);
    }
    private void OnDestroy()
    {
        if (questManager != null) questManager.OnProgressChanged -= Refresh;
    }
    // The tutorial takes priority over the inventory shortcut while teaching the controls.
    private void Update()
    {
        if (TutorialControles2D.BloqueaInteracciones) return;
        if (Input.GetKeyDown(toggleKey)) Toggle();
    }
    public void Toggle()
    {
        if (window == null) return;
        window.SetActive(!window.activeSelf);
        if (window.activeSelf) Refresh();
    }
    public void Close()
    {
        if (window != null) window.SetActive(false);
    }
    private static Image MakeImage(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }
    private static TMP_Text MakeText(Transform parent, string name, string text, int size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TMP_Text tmp = obj.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }
    private void CreateWindow()
    {
        Image background = MakeImage(parentCanvas.transform, "InventarioVentana", new Color(0.07f, 0.08f, 0.11f, 0.95f));
        window = background.gameObject;
        RectTransform rt = (RectTransform)window.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(550, 470);
        rt.anchoredPosition = Vector2.zero;
        rt.SetAsLastSibling();

        TMP_Text title = MakeText(window.transform, "Titulo", "INVENTARIO  (I para cerrar)", 26);
        RectTransform titleRT = (RectTransform)title.transform;
        titleRT.anchorMin = new Vector2(0, 1); titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1); titleRT.anchoredPosition = new Vector2(0, -20);
        titleRT.sizeDelta = new Vector2(-40, 45);
        title.alignment = TextAlignmentOptions.Center;

        Image area = MakeImage(window.transform, "AreaObjetos", new Color(0.12f, 0.14f, 0.18f, 0.88f));
        gridRoot = (RectTransform)area.transform;
        gridRoot.anchorMin = new Vector2(0, 0); gridRoot.anchorMax = new Vector2(1, 1);
        gridRoot.offsetMin = new Vector2(24, 28);
        gridRoot.offsetMax = new Vector2(-24, -85);
        GridLayoutGroup layout = area.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(155, 90);
        layout.spacing = new Vector2(10, 10);
        layout.padding = new RectOffset(7, 7, 7, 7);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        emptyLabel = MakeText(window.transform, "Vacio", "No tienes objetos en este momento.", 20);
        RectTransform emptyRT = (RectTransform)emptyLabel.transform;
        emptyRT.anchorMin = new Vector2(0, 0.5f); emptyRT.anchorMax = new Vector2(1, 0.5f);
        emptyRT.sizeDelta = new Vector2(-50, 60);
        emptyRT.anchoredPosition = Vector2.zero;
        emptyLabel.alignment = TextAlignmentOptions.Center;
    }
    // Rebuild the slots from the quest manager's inventory on refresh so consumed items
    // disappear from the window.
    private void Refresh()
    {
        if (gridRoot == null) return;
        for (int i = gridRoot.childCount - 1; i >= 0; i--)
            Destroy(gridRoot.GetChild(i).gameObject);
        List<string> items = questManager == null ? new List<string>() : questManager.GetInventoryItems();
        emptyLabel.gameObject.SetActive(items.Count == 0);
        foreach (string item in items)
        {
            Image slot = MakeImage(gridRoot, "Objeto_" + item, new Color(0.21f, 0.23f, 0.29f, 1));
            Image iconImage = null;
            Sprite iconSprite = FindIcon(item);
            if (iconSprite != null)
            {
                iconImage = MakeImage(slot.transform, "Icono", Color.white);
                iconImage.sprite = iconSprite;
                iconImage.preserveAspect = true;
                RectTransform ir = (RectTransform)iconImage.transform;
                ir.anchorMin = new Vector2(0.5f, 1); ir.anchorMax = new Vector2(0.5f, 1);
                ir.pivot = new Vector2(0.5f, 1); ir.anchoredPosition = new Vector2(0, -5);
                ir.sizeDelta = new Vector2(42, 42);
            }
            TMP_Text label = MakeText(slot.transform, "Nombre", item, 15);
            RectTransform lr = (RectTransform)label.transform;
            lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 1);
            lr.offsetMin = new Vector2(5, 3);
            lr.offsetMax = new Vector2(-5, iconImage != null ? -49 : -5);
            label.alignment = TextAlignmentOptions.Center;
        }
    }
    private Sprite FindIcon(string name)
    {
        foreach (var entry in itemIcons)
            if (entry != null && entry.itemName == name) return entry.icon;
        return null;
    }
}
