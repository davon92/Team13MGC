using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class VNPauseScreen : MonoBehaviour, IUIScreen
{
    [Header("Wiring")]
    [SerializeField] GameObject root;
    [SerializeField] GameObject firstSelected;
    [SerializeField] ScreenController screens;
    [SerializeField] Button saveButton;
    [SerializeField] Button loadButton;
    [SerializeField] Button continueButton;
    [SerializeField] Button returnButton;
    GameObject _selectedBeforePause;
    int _selectedOptionIndexBeforePause = -1;   // NEW
    public static int? LastOptionIndexBeforePause;
    public string     ScreenId      => MenuIds.VnPause;
    public GameObject Root          => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;

    void Awake()
    {
        if (saveButton)     saveButton.onClick.AddListener(OnSave);
        if (loadButton) loadButton.onClick.AddListener(OnLoad);
        if (continueButton) continueButton.onClick.AddListener(OnContinue);
        if (returnButton)   returnButton.onClick.AddListener(OnReturnToMenu);
    }

    private void OnLoad()
    {
        screens?.Push(MenuIds.SaveLoad, new SaveSlotsScreen.Args
        {
            mode = SaveSlotsScreen.Mode.Load
        });
    }

    public void OnShow(object args)
    {
        // Remember what was selected before Pause steals focus
        _selectedBeforePause = EventSystem.current 
            ? EventSystem.current.currentSelectedGameObject 
            : null;

        // Try to compute which option index was selected (if options are visible)
        _selectedOptionIndexBeforePause = -1;
        var bootstrap   = FindFirstObjectByType<VNBootstrap>(FindObjectsInactive.Include);
        var optionsRoot = bootstrap ? bootstrap.optionsPresenterGroup : null;
        if (optionsRoot && _selectedBeforePause)
        {
            var options = optionsRoot.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < options.Length; i++)
            {
                var s = options[i];
                if (s && s.IsActive() && s.interactable && s.gameObject == _selectedBeforePause)
                {
                    _selectedOptionIndexBeforePause = i;
                    break;
                }
            }
        }
        // Persist for resumes triggered by Start (handled in VNPauseInput)
        LastOptionIndexBeforePause = _selectedOptionIndexBeforePause >= 0 
            ? _selectedOptionIndexBeforePause 
            : LastOptionIndexBeforePause;

        // Your existing Pause first-selected + scaler sync
        if (EventSystem.current && firstSelected)
            EventSystem.current.SetSelectedGameObject(firstSelected);
        var grp = Root.GetComponentInChildren<UISelectScalerGroup>(true);
        if (grp) grp.SyncNow(instant: true);
    }

    public void OnHide() {}

    void OnSave()
    {
        screens?.Push(MenuIds.SaveLoad, new SaveSlotsScreen.Args
        {
            mode = SaveSlotsScreen.Mode.Save
        });
    }

    void OnContinue() { VNCo.Start(CoResumeFlow()); }
    public void OnUI_Cancel(UnityEngine.InputSystem.InputValue _) { VNCo.Start(CoResumeFlow()); }

    System.Collections.IEnumerator CoResumeFlow()
    {
        // 1) Swallow the leaked press
        VNInputBlocker.BlockAdvance(0.12f);

        // 2) Close Pause
        screens?.Pop();

        // 3) Let UI settle (do three frames just to be safe with fades/layout rebuild)
        yield return null;
        yield return null;
        yield return null;

        // 4) Restore focus when options are actually interactable
        yield return VNOptionsFocusKeeper.RestoreAfterResume(1.0f);
    }

    async void OnReturnToMenu()
    {
        bool yes = await ModalHub.I.Confirm.ShowAsync("Return to the main menu? Unsaved progress will be lost." );
        if (!yes) return;

        await SceneFlow.LoadFrontEndAsync();
    }
}