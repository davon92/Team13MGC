using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneFlow
{
    public static async Task LoadVNAsync(bool newGame = false)
    {
        // if (newGame) set any session flags you need
        await SceneManager.LoadSceneAsync("VNGamePlayScene");
    }
}