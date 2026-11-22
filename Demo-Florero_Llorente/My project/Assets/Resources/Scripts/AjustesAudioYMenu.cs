using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ajustes de audio y subpaneles integrados en un menu de pausa existente.
/// - Mantiene los nombres de los campos/metodos del script original.
/// - Vincula automaticamente Slider/Toggle; no requiere eventos manuales OnValueChanged.
/// - Admite sliders con intervalo 0..1 o 0..100.
/// - Tiene herramientas de diagnostico en el menu contextual del componente.
/// No cambia Time.timeScale salvo al reiniciar la escena.
/// </summary>
public class AjustesAudioYMenu : MonoBehaviour
{
    [Header("Subpaneles del Canvas")]
    [SerializeField] private GameObject panelAjustesAudio;
    [SerializeField] private GameObject panelControles;
    [SerializeField] private TMP_Text textoControles;

    [Header("Audio (arrastrar AudioSource de MUSICA, no AudioClip)")]
    [SerializeField] private AudioSource[] fuentesMusica;
    [SerializeField] private Slider sliderVolumenGeneral;
    [SerializeField] private Slider sliderVolumenMusica;
    [SerializeField] private Toggle toggleSilenciarTodo;
    [SerializeField] private Toggle toggleSilenciarMusica;

    [Header("Boton de musica en el HUD")]
    [SerializeField] private Image imagenBotonMusica;
    [SerializeField] private Sprite iconoMusicaActiva;
    [SerializeField] private Sprite iconoMusicaSilenciada;
    [SerializeField] private TMP_Text etiquetaBotonMusica;

    [Header("Diagnostico")]
    [Tooltip("Muestra en Console cada cambio recibido de sliders y toggles.")]
    [SerializeField] private bool mostrarLogs = true;
    [Tooltip("Solo si otro script sobrescribe constantemente el audio. No activar por defecto.")]
    [SerializeField] private bool mantenerValoresEnLateUpdate = false;

    private float[] volumenBaseFuentes;
    private float volumenGeneral = 1f;
    private float volumenMusica = 1f;
    private bool silenciarTodo;
    private bool silenciarMusica;

    private const string KGeneral = "Florero_Audio_General";
    private const string KMusica = "Florero_Audio_Musica";
    private const string KMuteGeneral = "Florero_Audio_MuteGeneral";
    private const string KMuteMusica = "Florero_Audio_MuteMusica";

    // Each music source keeps its Inspector volume as a baseline. The music slider scales that
    // baseline.
    private void Awake()
    {
        volumenGeneral = Mathf.Clamp01(PlayerPrefs.GetFloat(KGeneral, 1f));
        volumenMusica = Mathf.Clamp01(PlayerPrefs.GetFloat(KMusica, 1f));
        silenciarTodo = PlayerPrefs.GetInt(KMuteGeneral, 0) != 0;
        silenciarMusica = PlayerPrefs.GetInt(KMuteMusica, 0) != 0;

        if (fuentesMusica == null) fuentesMusica = new AudioSource[0];
        volumenBaseFuentes = new float[fuentesMusica.Length];
        for (int i = 0; i < fuentesMusica.Length; i++)
            volumenBaseFuentes[i] = fuentesMusica[i] != null ? fuentesMusica[i].volume : 1f;

        AplicarAudio();
    }

    // Assign the sliders and toggles in the Inspector. This component registers their change
    // callbacks when enabled.
    private void OnEnable()
    {
        // El codigo conecta los eventos; evita tener que seleccionar Dynamic float/bool.
        if (sliderVolumenGeneral != null)
            sliderVolumenGeneral.onValueChanged.AddListener(CambiarVolumenGeneral);
        if (sliderVolumenMusica != null)
            sliderVolumenMusica.onValueChanged.AddListener(CambiarVolumenMusica);
        if (toggleSilenciarTodo != null)
            toggleSilenciarTodo.onValueChanged.AddListener(SilenciarTodo);
        if (toggleSilenciarMusica != null)
            toggleSilenciarMusica.onValueChanged.AddListener(SilenciarMusica);
    }

    private void OnDisable()
    {
        if (sliderVolumenGeneral != null)
            sliderVolumenGeneral.onValueChanged.RemoveListener(CambiarVolumenGeneral);
        if (sliderVolumenMusica != null)
            sliderVolumenMusica.onValueChanged.RemoveListener(CambiarVolumenMusica);
        if (toggleSilenciarTodo != null)
            toggleSilenciarTodo.onValueChanged.RemoveListener(SilenciarTodo);
        if (toggleSilenciarMusica != null)
            toggleSilenciarMusica.onValueChanged.RemoveListener(SilenciarMusica);
        PlayerPrefs.Save();
    }

    private void Start()
    {
        SincronizarUI();
        if (textoControles != null)
            textoControles.text = "W  - Mover arriba\nS  - Mover abajo\nA  - Mover izquierda\nD  - Mover derecha\n" +
                                 "E  - Interactuar / avanzar dialogos\nI  - Inventario\nJ  - ¿Sabias que...?";
        if (panelAjustesAudio != null) panelAjustesAudio.SetActive(false);
        if (panelControles != null) panelControles.SetActive(false);
        if (fuentesMusica.Length == 0)
            Debug.LogWarning("[AjustesAudio] Fuentes Musica esta vacio. El control GENERAL si funciona, pero el control de MUSICA no.", this);
        if (sliderVolumenGeneral == null || sliderVolumenMusica == null || toggleSilenciarTodo == null || toggleSilenciarMusica == null)
            Debug.LogWarning("[AjustesAudio] Falta asignar uno o mas Sliders/Toggles en el Inspector.", this);
        ActualizarIconos();
    }

    // Restore saved values without firing UI events that would write those values back to
    // PlayerPrefs.
    private void SincronizarUI()
    {
        if (sliderVolumenGeneral != null)
            sliderVolumenGeneral.SetValueWithoutNotify(Mathf.Lerp(sliderVolumenGeneral.minValue, sliderVolumenGeneral.maxValue, volumenGeneral));
        if (sliderVolumenMusica != null)
            sliderVolumenMusica.SetValueWithoutNotify(Mathf.Lerp(sliderVolumenMusica.minValue, sliderVolumenMusica.maxValue, volumenMusica));
        if (toggleSilenciarTodo != null) toggleSilenciarTodo.SetIsOnWithoutNotify(silenciarTodo);
        if (toggleSilenciarMusica != null) toggleSilenciarMusica.SetIsOnWithoutNotify(silenciarMusica);
    }

    // Map the slider's own range to an audio gain between zero and one.
    private static float Normalizar(Slider slider, float valor)
    {
        if (slider == null) return Mathf.Clamp01(valor);
        if (Mathf.Approximately(slider.minValue, slider.maxValue)) return 0f;
        return Mathf.Clamp01(Mathf.InverseLerp(slider.minValue, slider.maxValue, valor));
    }

    public void CambiarVolumenGeneral(float valor)
    {
        volumenGeneral = Normalizar(sliderVolumenGeneral, valor);
        PlayerPrefs.SetFloat(KGeneral, volumenGeneral);
        AplicarAudio();
        if (mostrarLogs) Debug.Log($"[AjustesAudio] Slider general={valor:0.##}; volumen aplicado={volumenGeneral:0.00}; Listener={AudioListener.volume:0.00}", this);
    }

    public void CambiarVolumenMusica(float valor)
    {
        volumenMusica = Normalizar(sliderVolumenMusica, valor);
        PlayerPrefs.SetFloat(KMusica, volumenMusica);
        AplicarAudio();
        if (mostrarLogs) Debug.Log($"[AjustesAudio] Slider musica={valor:0.##}; factor aplicado={volumenMusica:0.00}; fuentes asignadas={fuentesMusica.Length}", this);
    }

    public void SilenciarTodo(bool estado)
    {
        silenciarTodo = estado;
        PlayerPrefs.SetInt(KMuteGeneral, silenciarTodo ? 1 : 0);
        AplicarAudio();
        PlayerPrefs.Save();
        if (mostrarLogs) Debug.Log($"[AjustesAudio] Silenciar TODO={estado}; Listener={AudioListener.volume:0.00}", this);
    }

    public void SilenciarMusica(bool estado)
    {
        silenciarMusica = estado;
        PlayerPrefs.SetInt(KMuteMusica, silenciarMusica ? 1 : 0);
        AplicarAudio();
        PlayerPrefs.Save();
        if (mostrarLogs) Debug.Log($"[AjustesAudio] Silenciar MUSICA={estado}; fuentes asignadas={fuentesMusica.Length}", this);
    }

    public void AlternarSilenciarMusica()
    {
        SilenciarMusica(!silenciarMusica);
        if (toggleSilenciarMusica != null)
            toggleSilenciarMusica.SetIsOnWithoutNotify(silenciarMusica);
    }

    // Master mute acts on the listener. Music mute acts on the listed sources and preserves
    // their volume settings.
    private void AplicarAudio()
    {
        AudioListener.volume = silenciarTodo ? 0f : volumenGeneral;
        for (int i = 0; i < fuentesMusica.Length; i++)
        {
            AudioSource fuente = fuentesMusica[i];
            if (fuente == null) continue;
            fuente.volume = volumenBaseFuentes[i] * volumenMusica;
            fuente.mute = silenciarMusica;
        }
        ActualizarIconos();
    }

    // Enable this option when another component writes to a music source's volume earlier in the
    // frame.
    private void LateUpdate()
    {
        if (mantenerValoresEnLateUpdate) AplicarAudio();
    }

    private void ActualizarIconos()
    {
        if (imagenBotonMusica != null)
        {
            Sprite sprite = silenciarMusica ? iconoMusicaSilenciada : iconoMusicaActiva;
            if (sprite != null) imagenBotonMusica.sprite = sprite;
        }
        if (etiquetaBotonMusica != null)
            etiquetaBotonMusica.text = silenciarMusica ? "Música: apagada" : "Música: encendida";
    }

    public void AbrirAjustesAudio()
    {
        if (panelControles != null) panelControles.SetActive(false);
        if (panelAjustesAudio != null) panelAjustesAudio.SetActive(true);
        SincronizarUI();
    }

    public void CerrarAjustesAudio()
    {
        if (panelAjustesAudio != null) panelAjustesAudio.SetActive(false);
        if (panelControles != null) panelControles.SetActive(false);
        PlayerPrefs.Save();
    }

    public void AbrirControles()
    {
        if (panelAjustesAudio != null) panelAjustesAudio.SetActive(false);
        if (panelControles != null) panelControles.SetActive(true);
    }

    public void VolverDeControles()
    {
        if (panelControles != null) panelControles.SetActive(false);
        if (panelAjustesAudio != null) panelAjustesAudio.SetActive(true);
    }

    public void ReiniciarEscena()
    {
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    [ContextMenu("Diagnostico: mostrar estado de audio")]
    private void MostrarEstadoAudio()
    {
        Debug.Log($"[AjustesAudio] OBJETO={name}; ACTIVO={isActiveAndEnabled}; General={volumenGeneral:0.00}; Musica={volumenMusica:0.00}; MuteTodo={silenciarTodo}; MuteMusica={silenciarMusica}; ListenerVolume={AudioListener.volume:0.00}; ListenerPause={AudioListener.pause}; FuentesMusica={fuentesMusica.Length}", this);
        for (int i = 0; i < fuentesMusica.Length; i++)
        {
            AudioSource fuente = fuentesMusica[i];
            if (fuente == null) { Debug.LogWarning($"[AjustesAudio] Fuente {i}: referencia vacia", this); continue; }
            Debug.Log($"[AjustesAudio] Fuente {i}: objeto={fuente.gameObject.name}, clip={(fuente.clip != null ? fuente.clip.name : "(sin clip asignado)")}, isPlaying={fuente.isPlaying}, volume={fuente.volume:0.00}, mute={fuente.mute}", fuente);
        }
    }

    [ContextMenu("Prueba: SILENCIAR TODO")]
    private void PruebaSilenciarTodo() { SilenciarTodo(true); SincronizarUI(); }

    [ContextMenu("Prueba: ACTIVAR TODO")]
    private void PruebaActivarTodo() { SilenciarTodo(false); SincronizarUI(); }

    private void OnApplicationPause(bool pausa)
    {
        if (pausa) PlayerPrefs.Save();
    }

    private void OnApplicationQuit() { PlayerPrefs.Save(); }
}
