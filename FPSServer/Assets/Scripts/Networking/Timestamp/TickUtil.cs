using UnityEngine;

public static class TickUtil {
    public static int SecondsToTick(float sec) {
        return Mathf.RoundToInt(sec / NetworkSettings.tickTime);
    }
}