using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Threading.Tasks;
#if DOTWEEN_PRESENT
using DG.Tweening;
#endif

public class StoryMenuScreen : MonoBehaviour, IUIScreen, ICancelHandler
{
    [Header("Wiring")]
    [SerializeField] private GameObject root;            // Screen_Story panel
    [SerializeField] private GameObject firstSelected;   // New Game button
    [SerializeField] private ScreenController screens;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button backButton;

    [Header("Optional overwrite modal")]
    [SerializeField] private bool confirmOverwriteIfSaveExists = true;
    [SerializeField] private GameObject overwriteModalRoot; // small panel with Yes/No
    [SerializeField] private Button overwriteYesButton;
    [SerializeField] private Button overwriteNoButton;

    public string ScreenId => MenuIds.StoryMenu;
    public GameObject Root => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;

    void Awake()
    {
        if (root == null) root = gameObject;

        // Button hooks (so you don't forget to wire in the Inspector)
        if (newGameButton)  newGameButton.onClick.AddListener(OnNewGame);
        if (loadGameButton) loadGameButton.onClick.AddListener(OnLoadGame);
        if (backButton)     backButton.onClick.AddListener(OnBack);

        if (overwriteModalRoot) HideModalImmediate();
        if (overwriteYesButton) overwriteYesButton.onClick.AddListener(() => { HideModal(); _ = StartNewGameAsync(); });
        if (overwriteNoButton)  overwriteNoButton.onClick.AddListener(HideModal);
    }

    public void OnShow(object args)
    {
        if (FirstSelected)
            EventSystem.current.SetSelectedGameObject(FirstSelected);
    }

    public void OnHide() { }

    // --- Actions ---
    public void OnNewGame()
    {
        if (confirmOverwriteIfSaveExists && SaveSystem.HasAnySave())
        {
            ShowModal();
            return;
        }
        _ = StartNewGameAsync();
    }

    public void OnLoadGame()
    {
        screens?.Show(MenuIds.SaveLoad);   // make MenuIds.SaveLoad (or whatever id you use)
    }

    public void OnBack() => screens?.Pop();

    public void OnCancel(BaseEventData e) => OnBack();

    // --- Flow ---
    private async Task StartNewGameAsync()
    {
        SaveSystem.CreateNewGame();              // stubbed below; replace with your real save init
        await Fade.Instance.Out();
        await SceneFlow.LoadVNAsync(newGame: true); // add the helper in SceneFlow (below)
        await Fade.Instance.In();
    }

    // --- Modal helpers ---
    private void ShowModal()
    {
        if (!overwriteModalRoot) return;
        overwriteModalRoot.SetActive(true);
#if DOTWEEN_PRESENT
        var cg = EnsureCanvasGroup(overwriteModalRoot);
        cg.alpha = 0f; cg.blocksRaycasts = true; cg.interactable = true;
        cg.DOFade(1f, 0.15f).SetUpdate(true);
#else
        var cg = EnsureCanvasGroup(overwriteModalRoot);
        cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;
#endif
        EventSystem.current.SetSelectedGameObject(overwriteYesButton ? overwriteYesButton.gameObject : null);
    }

    private void HideModal()
    {
        if (!overwriteModalRoot) return;
#if DOTWEEN_PRESENT
        var cg = EnsureCanvasGroup(overwriteModalRoot);
        cg.DOKill();
        cg.DOFade(0f, 0.12f).SetUpdate(true).OnComplete(() =>
        {
            cg.blocksRaycasts = false; cg.interactable = false;
            overwriteModalRoot.SetActive(false);
        });
#else
        var cg = EnsureCanvasGroup(overwriteModalRoot);
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
        overwriteModalRoot.SetActive(false);
#endif
    }

    private void HideModalImmediate()
    {
        var cg = EnsureCanvasGroup(overwriteModalRoot);
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
        overwriteModalRoot.SetActive(false);
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }
}
