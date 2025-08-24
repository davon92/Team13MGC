using UnityEngine;

public class HitBurst : MonoBehaviour
{
    [SerializeField] ParticleSystem burst;   // place at the hit line

    public void Play(Judgement j)
    {
        if (!burst || j == Judgement.Miss) return;
        burst.Play();
    }
}