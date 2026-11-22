using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flecha dentro del Canvas que sigue al jugador y gira hacia el siguiente
/// QuestTarget de la MISIÓN PRINCIPAL. Cambia automáticamente al avanzar.
/// No calcula caminos; indica dirección en línea recta.
/// </summary>
public class FlechaObjetivoPrincipalUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform jugador;
    [SerializeField] private Canvas canvasExistente;
    [SerializeField] private Camera camaraJuego;
    [SerializeField] private StoryQuestManager questManager;
    [SerializeField] private HUDController hud;

    [Header("Apariencia")]
    [SerializeField] private Vector3 desplazamientoMundo = new Vector3(0f, 1.10f, 0f);
    [SerializeField] private float tamanoFlecha = 36f;
    [SerializeField] private float ocultarSiCerca = 1.6f;
    [SerializeField] private Color colorFlecha = new Color(0.85f, 0.54f, 0.14f, 1f);

    private RectTransform flecha;
    private Image imagenFlecha;
    private Sprite spriteCreado;
    private Texture2D texturaCreada;
    private Transform objetivoActual;
    private string targetIdActual;
    private float proximoReintento;

    private void Start()
    {
        if (jugador == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) jugador = playerObject.transform;
        }
        if (canvasExistente == null) canvasExistente = FindObjectOfType<Canvas>();
        if (camaraJuego == null) camaraJuego = Camera.main;
        if (questManager == null) questManager = StoryQuestManager.Instance;
        if (hud == null) hud = FindObjectOfType<HUDController>();
        if (jugador == null || canvasExistente == null || camaraJuego == null || questManager == null)
        {
            Debug.LogWarning("FlechaObjetivoPrincipalUI: faltan Jugador, Canvas, Cámara o StoryQuestManager.", this);
            enabled = false;
            return;
        }
        CrearFlecha();
        questManager.OnProgressChanged += BuscarObjetivo;
        BuscarObjetivo();
    }

    // Resolve the main step's target ID to an active scene object. Quest progress can change
    // which object the arrow tracks.
    private void BuscarObjetivo()
    {
        objetivoActual = null;
        targetIdActual = null;
        if (questManager == null || !questManager.TryGetCurrentMainTarget(
            out string targetId, out string descripcion))
            return;
        targetIdActual = targetId;
        QuestTarget[] disponibles = FindObjectsOfType<QuestTarget>(true);
        foreach (QuestTarget candidato in disponibles)
        {
            if (candidato != null && candidato.gameObject.activeInHierarchy &&
                candidato.targetId == targetId)
            {
                objetivoActual = candidato.transform;
                break;
            }
        }
    }

    // Hide the arrow while a fact or tutorial is on screen and when the player is already close
    // to the target.
    private void LateUpdate()
    {
        if (flecha == null) return;
        if (TutorialControles2D.IsRunning || (hud != null && hud.IsFactOpen))
        {
            flecha.gameObject.SetActive(false);
            return;
        }
        if (objetivoActual == null || !objetivoActual.gameObject.activeInHierarchy)
        {
            if (Time.unscaledTime >= proximoReintento)
            {
                proximoReintento = Time.unscaledTime + 1f;
                BuscarObjetivo();
            }
        }
        if (objetivoActual == null)
        {
            flecha.gameObject.SetActive(false);
            return;
        }
        if (Vector2.Distance(jugador.position, objetivoActual.position) <= ocultarSiCerca)
        {
            flecha.gameObject.SetActive(false);
            return;
        }

        // Project both positions through the gameplay camera so the arrow points in the
        // direction the player sees.
        Vector3 pantallaJugador = camaraJuego.WorldToScreenPoint(jugador.position + desplazamientoMundo);
        Vector3 pantallaDestino = camaraJuego.WorldToScreenPoint(objetivoActual.position);
        if (pantallaJugador.z <= 0f || pantallaDestino.z <= 0f)
        {
            flecha.gameObject.SetActive(false);
            return;
        }
        Vector2 direccionPantalla = (Vector2)(pantallaDestino - pantallaJugador);
        if (direccionPantalla.sqrMagnitude < 0.01f)
        {
            flecha.gameObject.SetActive(false);
            return;
        }

        RectTransform contenedor = flecha.parent as RectTransform;
        Camera camaraUI = canvasExistente.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : canvasExistente.worldCamera;
        if (contenedor != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contenedor, pantallaJugador, camaraUI, out Vector2 posicion))
        {
            float x = Mathf.Max(0f, contenedor.rect.width * 0.5f - tamanoFlecha);
            float y = Mathf.Max(0f, contenedor.rect.height * 0.5f - tamanoFlecha);
            posicion.x = Mathf.Clamp(posicion.x, -x, x);
            posicion.y = Mathf.Clamp(posicion.y, -y, y);
            flecha.anchoredPosition = posicion;
        }
        float angulo = Mathf.Atan2(direccionPantalla.y, direccionPantalla.x) * Mathf.Rad2Deg - 90f;
        flecha.localRotation = Quaternion.Euler(0f, 0f, angulo);
        if (!flecha.gameObject.activeSelf) flecha.gameObject.SetActive(true);
    }

    // Build the indicator under the existing Canvas so the scene does not need a preplaced
    // arrow.
    private void CrearFlecha()
    {
        GameObject obj = new GameObject("FlechaMisionPrincipal", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        obj.transform.SetParent(canvasExistente.transform, false);
        flecha = obj.GetComponent<RectTransform>();
        flecha.anchorMin = flecha.anchorMax = flecha.pivot = new Vector2(0.5f, 0.5f);
        flecha.sizeDelta = new Vector2(tamanoFlecha, tamanoFlecha);
        flecha.SetAsLastSibling();
        imagenFlecha = obj.GetComponent<Image>();
        imagenFlecha.raycastTarget = false;
        imagenFlecha.color = colorFlecha;
        imagenFlecha.sprite = CrearSpriteTriangulo();
        Outline borde = obj.GetComponent<Outline>();
        borde.effectColor = new Color(0.25f, 0.16f, 0.08f, 0.9f);
        borde.effectDistance = new Vector2(2f, -2f);
        flecha.gameObject.SetActive(false);
    }

    // Triángulo sencillo apuntando hacia ARRIBA; no necesita importar PNG.
    private Sprite CrearSpriteTriangulo()
    {
        const int lado = 48;
        texturaCreada = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        texturaCreada.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            float t = (lado - 1f - y) / (lado - 1f); // 0 = punta arriba
            float mitad = 2f + t * 19f;
            for (int x = 0; x < lado; x++)
                pixels[y * lado + x] = Mathf.Abs(x - 23.5f) <= mitad
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
        }
        texturaCreada.SetPixels32(pixels);
        texturaCreada.Apply();
        spriteCreado = Sprite.Create(texturaCreada, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f));
        return spriteCreado;
    }

    private void OnDestroy()
    {
        if (questManager != null) questManager.OnProgressChanged -= BuscarObjetivo;
        if (spriteCreado != null) Destroy(spriteCreado);
        if (texturaCreada != null) Destroy(texturaCreada);
    }
}
