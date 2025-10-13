using UnityEngine;

public class HitBurst : MonoBehaviour
{
    [SerializeField] ParticleSystem burst;   // place at the hit line
    [SerializeField] AudioSource audioPlayer;
    public void Play(Judgement j)
    {
        if (!burst || j == Judgement.Miss) return;
        audioPlayer.Play();
        burst.Play();
    }
}