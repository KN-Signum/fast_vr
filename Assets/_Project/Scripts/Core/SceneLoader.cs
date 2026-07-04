using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SceneLoader : MonoBehaviour
{
    private static SceneLoader _instance;

    [Header("UI Assets")]
    [SerializeField] private Sprite loadingBarSprite;
    [SerializeField] private Sprite loadingOutlineSprite;
    [SerializeField] private TMP_FontAsset labelFont;

    [Header("Timing")]
    [SerializeField] private float minimumDisplaySeconds = 0.35f;
    [SerializeField] private float postLoadFillSpeed = 2f;

    private GameObject _overlayRoot;
    private Image _fillImage;
    private bool _isLoading;

    public static bool IsLoading => _instance != null && _instance._isLoading;

    public static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("SceneLoader: empty scene name.");
            return;
        }

        Instance.StartLoad(sceneName);
    }

    public static void LoadActiveScene()
    {
        Load(SceneManager.GetActiveScene().name);
    }

    private static SceneLoader Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = FindFirstObjectByType<SceneLoader>();
            if (_instance != null)
                return _instance;

            var go = new GameObject("SceneLoader");
            _instance = go.AddComponent<SceneLoader>();
            DontDestroyOnLoad(go);
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
    }

    private void StartLoad(string sceneName)
    {
        EnsureOverlay();
        StopAllCoroutines();
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        _isLoading = true;
        _overlayRoot.SetActive(true);
        SetFill(0f);

        float shownAt = Time.unscaledTime;

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);
        loadOp.allowSceneActivation = false;

        while (loadOp.progress < 0.9f)
        {
            SetFill(Mathf.Clamp01(loadOp.progress / 0.9f));
            yield return null;
        }

        float minElapsed = Time.unscaledTime - shownAt;
        if (minElapsed < minimumDisplaySeconds)
            yield return new WaitForSecondsRealtime(minimumDisplaySeconds - minElapsed);

        while (_fillImage.fillAmount < 1f)
        {
            SetFill(Mathf.MoveTowards(_fillImage.fillAmount, 1f, postLoadFillSpeed * Time.unscaledDeltaTime));
            yield return null;
        }

        loadOp.allowSceneActivation = true;

        while (!loadOp.isDone)
            yield return null;

        _overlayRoot.SetActive(false);
        _isLoading = false;
    }

    private void SetFill(float amount)
    {
        if (_fillImage != null)
            _fillImage.fillAmount = amount;
    }

    private void EnsureOverlay()
    {
        if (_overlayRoot != null)
            return;

        _overlayRoot = new GameObject("LoadingOverlay");
        _overlayRoot.transform.SetParent(transform, false);

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(_overlayRoot.transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var backdrop = CreateUiObject("Backdrop", canvasGo.transform);
        StretchFullScreen(backdrop.GetComponent<RectTransform>());
        var backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0.07f, 0.07f, 0.07f, 0.95f);
        backdropImage.raycastTarget = true;

        var content = CreateUiObject("Content", canvasGo.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(1280f, 320f);
        contentRect.anchoredPosition = Vector2.zero;

        var labelGo = CreateUiObject("Label", content.transform);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.58f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "Ładowanie gry...";
        label.fontSize = 72;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.478f, 0.106f, 0.443f, 1f);
        if (labelFont != null)
            label.font = labelFont;

        var barRoot = CreateUiObject("ProgressBar", content.transform);
        var barRootRect = barRoot.GetComponent<RectTransform>();
        barRootRect.anchorMin = new Vector2(0.5f, 0f);
        barRootRect.anchorMax = new Vector2(0.5f, 0f);
        barRootRect.pivot = new Vector2(0.5f, 0f);
        barRootRect.sizeDelta = new Vector2(1040f, 84f);
        barRootRect.anchoredPosition = new Vector2(0f, 16f);

        var outlineGo = CreateUiObject("Outline", barRoot.transform);
        StretchFullScreen(outlineGo.GetComponent<RectTransform>());
        var outlineImage = outlineGo.AddComponent<Image>();
        outlineImage.sprite = loadingOutlineSprite;
        outlineImage.type = loadingOutlineSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        outlineImage.preserveAspect = loadingOutlineSprite != null;
        outlineImage.color = loadingOutlineSprite != null
            ? Color.white
            : new Color(0.44f, 0.38f, 0.31f, 1f);

        var fillGo = CreateUiObject("Fill", barRoot.transform);
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.04f, 0.18f);
        fillRect.anchorMax = new Vector2(0.96f, 0.82f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        _fillImage = fillGo.AddComponent<Image>();
        _fillImage.sprite = loadingBarSprite;
        _fillImage.type = Image.Type.Filled;
        _fillImage.fillMethod = Image.FillMethod.Horizontal;
        _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fillImage.fillAmount = 0f;
        _fillImage.preserveAspect = false;
        _fillImage.color = loadingBarSprite != null
            ? Color.white
            : new Color(0.82f, 0.52f, 0.25f, 1f);

        _overlayRoot.SetActive(false);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
