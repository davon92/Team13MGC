// ModalHub.cs
using UnityEngine;

public class ModalHub : MonoBehaviour
{
    public static ModalHub I { get; private set; }
    [SerializeField] private ConfirmModal confirm;
    public ConfirmModal Confirm => confirm;

    void Awake() => I = this; // per-scene singleton (not DontDestroyOnLoad)
}