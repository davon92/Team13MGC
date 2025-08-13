using UnityEngine;

public static class SaveSystem
{
    private const string Key = "save_slot_0";

    public static bool HasAnySave() => PlayerPrefs.HasKey(Key);

    public static void CreateNewGame()
    {
        // TODO: your real initialization
        PlayerPrefs.SetInt(Key, 1);
        PlayerPrefs.Save();
    }
}