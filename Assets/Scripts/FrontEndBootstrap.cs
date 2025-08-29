using UnityEngine;

public class FrontEndBootstrap : MonoBehaviour
{
    [SerializeField] ScreenController controller;

    void Awake()
    {
        if (!controller) controller = FindFirstObjectByType<ScreenController>();

        var id = SceneFlow.FrontEndStartupScreenId;
        // clear so it doesn't "stick" next time
        SceneFlow.FrontEndStartupScreenId = null;

        if (!controller) return;

        if (!string.IsNullOrEmpty(id))
            controller.Show(id);                // jump to requested screen (e.g., "FreePlay")
        //else if (controller.AutoShowOnStart)
           // controller.Show(controller.InitialScreenId);
    }
}