using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial secuencial de WASD, I, J, E. Se presenta sobre el jugador
/// utilizando el Canvas existente. No pausa Time.timeScale.
/// El panel se construye solo con los colores del FactPanel original.
/// </summary>
public class TutorialControles2D : MonoBehaviour
{
    public static bool IsRunning { get; private set; }
    private static int frameFin = -1;
    public static bool BloqueaInteracciones => IsRunning || Time.frameCount <= frameFin;

    [Header("Referencias")]
    [SerializeField] private Transform jugador;
    [SerializeField] private Canvas canvasExistente;
    [SerializeField] private Camera camaraJuego;

    [Header("Ubicación del panel")]
    [SerializeField] private Vector3 desplazamientoMundo = new Vector3(0f, 1.65f, 0f);
    [SerializeField] private Vector2 tamanoPanel = new Vector2(440f, 145f);
    [SerializeField] private float margenPantalla = 14f;

    [Header("Inicio y repetición")]
    [SerializeField] private bool mostrarAlIniciar = true;
    [Tooltip("Si está activado, el tutorial se omite en futuras partidas del mismo equipo.")]
    [SerializeField] private bool soloPrimeraVez = false;

    [Header("Audio opcional")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoPaso;
    [SerializeField] private AudioClip sonidoFinal;

    private const string PlayerPrefsKey = "Florero_Tutorial_Controles_Completado";
    // Keep the key and instruction arrays in matching order. Each keypress advances the
    // instruction at the same index.
    private static readonly KeyCode[] Teclas =
    {
        KeyCode.D, KeyCode.A, KeyCode.W, KeyCode.S,
        KeyCode.I, KeyCode.J, KeyCode.E
    };
    private static readonly string[] Instrucciones =
    {
        "Para moverte a la derecha, presiona <b>D</b>.",
        "Para moverte a la izquierda, presiona <b>A</b>.",
        "Para moverte hacia arriba, presiona <b>W</b>.",
        "Para moverte hacia abajo, presiona <b>S</b>.",
        "Para consultar tu inventario, presiona <b>I</b>.",
        "Para leer los datos «¿Sabías que...?», presiona <b>J</b>.",
        "Para hablar con personajes y realizar entregas, presiona <b>E</b>."
    };

    private int pasoActual;
    private RectTransform panel;
    private TMP_Text titulo;
    private TMP_Text instrucciones;
    private TMP_Text progreso;

    private void Awake()
    {
        frameFin = -1;
        IsRunning = mostrarAlIniciar && (!soloPrimeraVez || PlayerPrefs.GetInt(PlayerPrefsKey, 0) == 0);
    }

    private void Start()
    {
        if (!IsRunning) return;
        if (jugador == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) jugador = playerObject.transform;
        }
        if (canvasExistente == null) canvasExistente = FindObjectOfType<Canvas>();
        if (camaraJuego == null) camaraJuego = Camera.main;
        if (jugador == null || canvasExistente == null || camaraJuego == null)
        {
            Debug.LogError("TutorialControles2D: asigna Jugador, Canvas y Cámara principal.", this);
            IsRunning = false;
            enabled = false;
            return;
        }
        CrearInterfaz();
        MostrarPaso();
    }

    private void Update()
    {
        if (!IsRunning) return;
        if (Input.GetKeyDown(Teclas[pasoActual]))
        {
            pasoActual++;
            if (pasoActual >= Teclas.Length)
            {
                if (audioSource != null && sonidoFinal != null) audioSource.PlayOneShot(sonidoFinal);
                FinalizarTutorial();
            }
            else
            {
                if (audioSource != null && sonidoPaso != null) audioSource.PlayOneShot(sonidoPaso);
                MostrarPaso();
            }
        }
    }

    // Follow the player's projected position in Canvas coordinates and hide the prompt when the
    // player is behind the camera.
    private void LateUpdate()
    {
        if (!IsRunning || panel == null || jugador == null) return;
        RectTransform contenedor = panel.parent as RectTransform;
        if (contenedor == null) return;
        Vector3 pantalla = camaraJuego.WorldToScreenPoint(jugador.position + desplazamientoMundo);
        if (pantalla.z <= 0f) { panel.gameObject.SetActive(false); return; }
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);

        Camera uiCamera = canvasExistente.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : canvasExistente.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contenedor, pantalla, uiCamera, out Vector2 posicion))
        {
            float mitadX = contenedor.rect.width * 0.5f;
            float mitadY = contenedor.rect.height * 0.5f;
            float margenX = tamanoPanel.x * 0.5f + margenPantalla;
            float margenY = tamanoPanel.y * 0.5f + margenPantalla;
            posicion.x = Mathf.Clamp(posicion.x, -Mathf.Max(0f, mitadX - margenX),
                Mathf.Max(0f, mitadX - margenX));
            posicion.y = Mathf.Clamp(posicion.y, -Mathf.Max(0f, mitadY - margenY),
                Mathf.Max(0f, mitadY - margenY));
            panel.anchoredPosition = posicion;
        }
    }

    private void MostrarPaso()
    {
        titulo.text = "INSTRUCCIONES DEL MENSAJERO";
        instrucciones.text = Instrucciones[pasoActual];
        progreso.text = "Control " + (pasoActual + 1) + " de " + Teclas.Length;
    }

    private void CrearInterfaz()
    {
        var baseUI = new GameObject("TutorialControlesPanel", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        baseUI.transform.SetParent(canvasExistente.transform, false);
        panel = baseUI.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = tamanoPanel;
        panel.SetAsLastSibling();

        Image fondo = baseUI.GetComponent<Image>();
        fondo.color = new Color(0.9882353f, 0.9529412f, 0.8235294f, 0.97f); // FactPanel
        fondo.raycastTarget = false;
        Outline borde = baseUI.GetComponent<Outline>();
        borde.effectColor = new Color(0.41960785f, 0.26666668f, 0.13725491f, 0.85f);
        borde.effectDistance = new Vector2(3f, -3f);

        titulo = CrearTexto("Titulo", 17, new Color(0.36f, 0.21f, 0.11f),
            new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(tamanoPanel.x - 30f, 30f));
        titulo.fontStyle = FontStyles.Bold;
        instrucciones = CrearTexto("Instruccion", 21, new Color(0.20f, 0.15f, 0.12f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(tamanoPanel.x - 26f, 75f));
        progreso = CrearTexto("Progreso", 14, new Color(0.42f, 0.31f, 0.23f),
            new Vector2(0.5f, 0f), new Vector2(0f, 15f), new Vector2(tamanoPanel.x - 30f, 22f));
    }

    private TMP_Text CrearTexto(string nombre, int tamano, Color color,
        Vector2 ancla, Vector2 posicion, Vector2 dimensiones)
    {
        GameObject objeto = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
        objeto.transform.SetParent(panel, false);
        RectTransform rt = objeto.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = ancla;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posicion;
        rt.sizeDelta = dimensiones;
        TMP_Text tmp = objeto.GetComponent<TMP_Text>();
        tmp.fontSize = tamano;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    public void SaltarTutorial() => FinalizarTutorial();

    // Block other interactions through the completion frame so the final E press cannot also
    // open a conversation.
    private void FinalizarTutorial()
    {
        if (!IsRunning) return;
        frameFin = Time.frameCount; // E no puede iniciar un diálogo en el mismo frame.
        IsRunning = false;
        if (panel != null) panel.gameObject.SetActive(false);
        if (soloPrimeraVez)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, 1);
            PlayerPrefs.Save();
        }
    }

    [ContextMenu("Reactivar tutorial guardado (PlayerPrefs)")]
    private void BorrarMarcaDeTutorial()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsKey);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (IsRunning) IsRunning = false;
    }
}
