using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TitleScreen : MonoBehaviour, IUIScreen
{
    [Header("Wire in Inspector")]
    [SerializeField] private GameObject root;           // this panel GameObject
    [SerializeField] private GameObject firstSelected;  // Story button (for gamepad focus)
    [SerializeField] private ScreenController screens;  // reference to your ScreenController

    [Space(6)]
    [SerializeField] private Button storyButton;
    [SerializeField] private Button freePlayButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button galleryButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    public string ScreenId => MenuIds.Title;
    public GameObject Root => root;
    public GameObject FirstSelected => firstSelected;

    private void Awake()
    {
        // Safety: let the panel be its own root if not set
        if (root == null) root = gameObject;

        // Hook up button actions
        if (storyButton) storyButton.onClick.AddListener(OnStory);
        if (freePlayButton) freePlayButton.onClick.AddListener(OnFreePlay);
        if (settingsButton) settingsButton.onClick.AddListener(OnSettings);
        if (galleryButton)  galleryButton.onClick.AddListener(OnGallery);
        if (creditsButton)  creditsButton.onClick.AddListener(OnCredits);
        if (quitButton) quitButton.onClick.AddListener(OnQuit);
    }

    public void OnShow(object args)
    {
        // Ensure the first gamepad focus is set
        if (firstSelected)
            EventSystem.current.SetSelectedGameObject(firstSelected);

        // (Optional) Play title BGM or trigger a whoosh here via your AudioManager
    }

    public void OnHide()
    {
        // (Optional) Cleanup, stop title-specific SFX, etc.
    }

    private void OnStory()
    {
        // Go to your VN/Story entry screen (another panel in Menus)
        if (screens) screens.Push(MenuIds.StoryMenu);
    }

    private void OnFreePlay()
    {
        // Go to Song Select panel (another panel in Menus)
        if (screens) screens.Push(MenuIds.SongSelect);
    }

    private void OnSettings()
    {
        // Open Options as a pushed modal (if your controller supports push)
        if (screens) screens.Push(MenuIds.Options);
    }

    private void OnGallery()
    {
        if (screens) screens.Push(MenuIds.Gallery);
    }

    private void OnCredits()
    {
        if (screens) screens.Push(MenuIds.Credits);
    }

    private async void OnQuit()
    {
        bool ok = await ModalHub.I.Confirm.ShowAsync("Are you sure you want to quit?");
        if (ok)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }
    }
}
