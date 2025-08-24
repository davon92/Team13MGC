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
    [SerializeField] Button continueButton;
    [SerializeField] Button returnButton;

    public string     ScreenId      => MenuIds.VnPause;
    public GameObject Root          => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;

    void Awake()
    {
        if (saveButton)     saveButton.onClick.AddListener(OnSave);
        if (continueButton) continueButton.onClick.AddListener(OnContinue);
        if (returnButton)   returnButton.onClick.AddListener(OnReturnToMenu);
    }

    public void OnShow(object args)
    {
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

    void OnContinue() => screens?.Pop();

    async void OnReturnToMenu()
    {
        bool yes = await ModalHub.I.Confirm.ShowAsync("Return to the main menu? Unsaved progress will be lost." );
        if (!yes) return;

        await SceneFlow.LoadFrontEndAsync();
    }
}