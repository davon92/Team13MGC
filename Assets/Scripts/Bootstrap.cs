using UnityEngine;
using UnityEngine.Audio;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer; // drag your mixer asset here

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SettingsService.Mixer = mixer;
        SettingsService.Load();  // applies audio on boot
    }
}