// PauseBus.cs
using System;

public static class PauseBus {
    public static bool IsPaused { get; private set; }
    public static event Action<bool> Changed;

    public static void Set(bool paused) {
        if (IsPaused == paused) return;
        IsPaused = paused;
        Changed?.Invoke(paused);
    }
}