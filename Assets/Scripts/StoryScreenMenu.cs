using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class StoryScreenMenu : MonoBehaviour, IUIScreen
{
    [Header("Wiring")]
    [SerializeField] GameObject root;
    [SerializeField] GameObject firstSelected;
    [SerializeField] ScreenController screens;
    [SerializeField] Button newGameButton;
    [SerializeField] Button loadGameButton;
    [SerializeField] Button backButton;
    

    public string     ScreenId      => MenuIds.StoryMenu;
    public GameObject Root          => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;

    void Awake()
    {
        if (newGameButton) newGameButton.onClick.AddListener(OnNewGame);
        if (loadGameButton) loadGameButton.onClick.AddListener(OnLoadGame);
        if (backButton) backButton.onClick.AddListener(OnBack);
    }

    public void OnShow(object args)
    {
        if (EventSystem.current && firstSelected)
            EventSystem.current.SetSelectedGameObject(firstSelected);
        var grp = Root.GetComponentInChildren<UISelectScalerGroup>(true);
        if (grp) grp.SyncNow(instant: true);
    }

    public void OnHide() {}

    async void OnNewGame()
    {
        // If autosave exists, confirm overwrite
        if (SaveSystem.SlotExists(SaveSystem.AutoSlot))
        {
            bool yes = await ModalHub.I.Confirm.ShowAsync(
                "Starting a new game will overwrite your current Auto Save. Continue?"
            );
            if (!yes) return;
        }

        // Create/overwrite autosave and immediately load VN
        SaveSystem.CreateNewGame(firstChapterId: "Prologue");
        await SceneFlow.LoadVNAsync(newGame: true);
    }

    void OnLoadGame()
    {
        screens?.Push(MenuIds.SaveLoad, new SaveSlotsScreen.Args
        {
            mode = SaveSlotsScreen.Mode.Load
        });
    }

    public void OnBack()
    {
        if (screens == null) return;
        if (screens.CanPop) screens.Pop();
        else screens.Show(MenuIds.Title);   // explicit fall-back if Story is the root
    }
    
    public void OnUI_Cancel(InputValue v)
    {
        if (v.isPressed) OnBack();
    }
}
