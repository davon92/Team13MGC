using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class FrontEndBootstrap : MonoBehaviour
{
    [SerializeField] private ScreenController controller;

    void Awake()
    {
        CullForeignEventSystems();
        CullForeignScreenControllers();

        var ctrls = Resources.FindObjectsOfTypeAll<ScreenController>();
        foreach (var sc in ctrls)
        {
            if (!sc) continue;
            var go = sc.gameObject;
            var sceneName = go.scene.IsValid() ? go.scene.name : "<ASSET>";
            if (go.hideFlags == HideFlags.None)
                Debug.Log($"[FrontEndBootstrap] Found ScreenController '{go.name}' in scene '{sceneName}' (root={go.transform.root.name})");
        }
        Time.timeScale = 1f;
        StartCoroutine(KillForeignControllersNextFrame());
    }
    
    System.Collections.IEnumerator KillForeignControllersNextFrame()
    {
        yield return null; // let everything spawn
        var activeScene = gameObject.scene;
        var all = Resources.FindObjectsOfTypeAll<ScreenController>();
        foreach (var sc in all)
        {
            if (!sc) continue;
            var go = sc.gameObject;
            if (!go.scene.IsValid()) continue;              // skip assets
            if (go.hideFlags != HideFlags.None) continue;   // skip editor-hidden
            if (go.scene == activeScene) continue;          // keep ours
            Debug.Log($"[FrontEndBootstrap] Destroying foreign controller '{go.name}' in '{go.scene.name}'.");
            Destroy(go);
        }
    }

    void Start()
    {
        EnsureEventSystem();

        if (!controller) controller = FindFirstObjectByType<ScreenController>();
        if (!controller) return;

        SeedFrontEnd();       // Title base, optional push of startup (e.g., SongSelect)
        UnblockFadeOverlay(); // make sure no invisible overlay blocks raycasts
        UnblockExternalCanvasGroups();  
        StartCoroutine(SelectSomethingNextFrame());
    }
    void UnblockExternalCanvasGroups()
    {
        var all = Resources.FindObjectsOfTypeAll<CanvasGroup>();
        foreach (var cg in all)
        {
            if (!cg) continue;
            var go = cg.gameObject;
            if (!go.scene.IsValid())            continue; // skip assets
            if (go.hideFlags != HideFlags.None) continue;
            if (go.scene == gameObject.scene)   continue;

            if (cg.blocksRaycasts || cg.interactable)
            {
#if UNITY_EDITOR
                Debug.Log($"[FrontEndBootstrap] Disabling external CanvasGroup '{go.name}' from scene '{go.scene.name}'.");
#endif
                cg.blocksRaycasts = false;
                cg.interactable   = false;
            }
        }
    }
    // FrontEndBootstrap.cs
    void SeedFrontEnd()
    {
        var startup = SceneFlow.FrontEndStartupScreenId;
        SceneFlow.FrontEndStartupScreenId = null;

        var baseId = controller.InitialScreenId;

#if UNITY_EDITOR
        Debug.Log($"[FrontEndBootstrap] SeedFrontEnd base={baseId} startup={startup}");
#endif

        // Always put Title (base) on the stack first
        if (!string.IsNullOrEmpty(baseId))
        {
            controller.Show(baseId);            // stack = [Title]
#if UNITY_EDITOR
            Debug.Log("[FrontEndBootstrap] Show(base) done; stack now has Title");
#endif
        }

        // If a different screen was requested, push it on top
        if (!string.IsNullOrEmpty(startup) && startup != baseId)
        {
            controller.Push(startup);           // stack = [Title, SongSelect]
#if UNITY_EDITOR
            Debug.Log($"[FrontEndBootstrap] Push({startup}) done; stack now has Title + {startup}");
#endif
        }
    }


    // ---- cull everything not in THIS scene (even hidden / DontDestroyOnLoad) ----
    void CullForeignScreenControllers()
    {
        var all = Resources.FindObjectsOfTypeAll<ScreenController>();
        foreach (var sc in all)
        {
            if (!sc) continue;
            var go = sc.gameObject;

            // Skip prefab assets or editor-hidden objects
            if (!go.scene.IsValid())            continue;
            if (go.hideFlags != HideFlags.None) continue;
            if (sc.hideFlags != HideFlags.None) continue;

            // Keep controllers that live in THIS scene
            if (go.scene == gameObject.scene)   continue;

#if UNITY_EDITOR
            Debug.Log($"[FrontEndBootstrap] Destroying foreign ScreenController '{go.name}' from scene '{go.scene.name}'.");
#endif
            Destroy(go);
        }
    }

    void CullForeignEventSystems()
    {
        var all = Resources.FindObjectsOfTypeAll<EventSystem>();
        EventSystem keep = null;

        // Prefer the EventSystem in THIS scene
        foreach (var es in all)
        {
            if (!es) continue;
            var go = es.gameObject;
            if (!go.scene.IsValid())            continue;
            if (go.hideFlags != HideFlags.None) continue;
            if (go.scene == gameObject.scene) { keep = es; break; }
        }

        // Fall back to any valid live one
        if (!keep)
        {
            foreach (var es in all)
            {
                if (!es) continue;
                var go = es.gameObject;
                if (!go.scene.IsValid()) continue;
                keep = es; break;
            }
        }

        foreach (var es in all)
        {
            if (!es) continue;
            var go = es.gameObject;
            if (!go.scene.IsValid())            continue;
            if (go.hideFlags != HideFlags.None) continue;
            if (es == keep) continue;

#if UNITY_EDITOR
            Debug.Log($"[FrontEndBootstrap] Destroying foreign EventSystem '{go.name}' from scene '{go.scene.name}'.");
#endif
            Destroy(go);
        }
    }

    void EnsureEventSystem()
    {
        var es = EventSystem.current;
        if (!es)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            es = go.GetComponent<EventSystem>();
        }
        es.enabled = true;
#if ENABLE_INPUT_SYSTEM
        var ui = es.GetComponent<InputSystemUIInputModule>();
        if (ui) ui.enabled = true;
#endif
        es.sendNavigationEvents = true;
    }

    void UnblockFadeOverlay()
    {
        var all = FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cg in all)
        {
            var n = cg.gameObject.name;
            if ((n.Contains("Fade") || n.Contains("Overlay")) && cg.alpha <= 0.001f)
            {
                cg.blocksRaycasts = false;
                cg.interactable   = false;
            }
        }
    }

    System.Collections.IEnumerator SelectSomethingNextFrame()
    {
        yield return null; // let ScreenController finish Show/Push
        if (!EventSystem.current) yield break;
        if (EventSystem.current.currentSelectedGameObject) yield break;

        var root = controller ? controller.CurrentScreenRoot : null;
        if (!root) root = controller ? controller.gameObject : gameObject;

        var sel = root.GetComponentInChildren<UnityEngine.UI.Selectable>(false);
        if (sel) EventSystem.current.SetSelectedGameObject(sel.gameObject);
    }
}
