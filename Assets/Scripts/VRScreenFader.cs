using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class VRScreenFader : MonoBehaviour
{
    public static VRScreenFader Instance { get; private set; }

    [Header("Dimming Settings")]
    [SerializeField] private Color fadeColor = new Color(0.05f, 0.05f, 0.05f, 1f);
    [Tooltip("Gray out to very low light rather than 100% black (0.85 = 85% dimming).")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float maxDimAlpha = 0.85f;
    [SerializeField] private float defaultFadeDuration = 1.0f;

    [Header("Text Settings")]
    [SerializeField] private Color textColor = Color.white;

    private Camera targetCamera;
    private GameObject fadeQuad;
    private Material fadeMaterial;
    private TMP_Text messageText;
    private GameObject textObj;
    private Coroutine currentFadeCoroutine;
    private float currentAlpha = 0f;
    private string activeMessage = "";

    public float CurrentAlpha => currentAlpha;
    public float MaxDimAlpha
    {
        get => maxDimAlpha;
        set => maxDimAlpha = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        EnsureFadeObjects();
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        EnsureFadeObjects();
    }

    public void EnsureFadeObjects()
    {
        if (targetCamera == null || !targetCamera.gameObject.activeInHierarchy)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindAnyObjectByType<Camera>();
        }

        if (targetCamera == null) return;

        // Ensure Material
        if (fadeMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");

            fadeMaterial = new Material(shader);
            fadeMaterial.color = fadeColor;

            // Configure transparent blending
            fadeMaterial.SetFloat("_Surface", 1); // 1 = Transparent in URP
            fadeMaterial.SetFloat("_Blend", 0);
            fadeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fadeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fadeMaterial.SetInt("_ZWrite", 0);
            fadeMaterial.renderQueue = 2990;
        }

        // Create or re-parent Fade Quad (placed at z = 0.6m for comfortable VR stereo viewing)
        if (fadeQuad == null)
        {
            fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fadeQuad.name = "VR_Screen_Fade_Quad";
            Destroy(fadeQuad.GetComponent<Collider>());

            var renderer = fadeQuad.GetComponent<MeshRenderer>();
            renderer.material = fadeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            DontDestroyOnLoad(fadeQuad);
        }

        fadeQuad.transform.SetParent(targetCamera.transform, false);
        fadeQuad.transform.localPosition = new Vector3(0f, 0f, 0.6f);
        fadeQuad.transform.localRotation = Quaternion.identity;
        fadeQuad.transform.localScale = new Vector3(25f, 25f, 1f);

        // Create TextMeshPro message element
        if (messageText == null)
        {
            textObj = new GameObject("FadeMessageText");
            textObj.transform.SetParent(targetCamera.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, 0.59f);
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one * 0.012f;

            messageText = textObj.AddComponent<TextMeshPro>();
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.fontSize = 28;
            messageText.lineSpacing = 15f;
            messageText.color = textColor;
            messageText.text = "";
            messageText.extraPadding = true;

            RectTransform rect = messageText.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(200f, 100f);
            }

            if (messageText.fontMaterial != null)
            {
                messageText.fontMaterial.renderQueue = 3100;
            }

            DontDestroyOnLoad(textObj);
        }
        else
        {
            textObj.transform.SetParent(targetCamera.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, 1.4f);
            textObj.transform.localScale = Vector3.one * 0.012f;
            messageText.fontSize = 28;
        }

        SetAlpha(currentAlpha);
    }

    public void SetAlpha(float alpha)
    {
        currentAlpha = Mathf.Clamp01(alpha);

        if (fadeMaterial != null)
        {
            float dimA = currentAlpha * maxDimAlpha;
            Color c = new Color(fadeColor.r, fadeColor.g, fadeColor.b, dimA);
            fadeMaterial.color = c;
            if (fadeMaterial.HasProperty("_BaseColor"))
            {
                fadeMaterial.SetColor("_BaseColor", c);
            }
        }

        if (fadeQuad != null)
        {
            fadeQuad.SetActive(currentAlpha > 0.001f);
        }

        if (messageText != null)
        {
            Color tc = textColor;
            tc.a = currentAlpha;
            messageText.color = tc;
            messageText.gameObject.SetActive(currentAlpha > 0.01f && !string.IsNullOrEmpty(messageText.text));
        }
    }

    public void SetMessage(string message)
    {
        activeMessage = message ?? "";
        if (messageText != null)
        {
            messageText.text = activeMessage;
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(activeMessage) && currentAlpha > 0.01f);
        }
    }

    public void ClearMessage()
    {
        SetMessage("");
    }

    public void FadeOut(float duration = -1f, string message = null, Action onComplete = null)
    {
        EnsureFadeObjects();
        if (duration < 0f) duration = defaultFadeDuration;
        if (message != null) SetMessage(message);

        if (currentFadeCoroutine != null) StopCoroutine(currentFadeCoroutine);
        currentFadeCoroutine = StartCoroutine(FadeRoutine(currentAlpha, 1f, duration, onComplete));
    }

    public void FadeIn(float duration = -1f, Action onComplete = null)
    {
        EnsureFadeObjects();
        if (duration < 0f) duration = defaultFadeDuration;

        if (currentFadeCoroutine != null) StopCoroutine(currentFadeCoroutine);
        currentFadeCoroutine = StartCoroutine(FadeRoutine(currentAlpha, 0f, duration, () =>
        {
            ClearMessage();
            onComplete?.Invoke();
        }));
    }

    private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration, Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetAlpha(toAlpha);
        currentFadeCoroutine = null;
        onComplete?.Invoke();
    }
}
