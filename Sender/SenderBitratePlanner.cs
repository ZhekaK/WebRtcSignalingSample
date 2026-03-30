using UnityEngine;

/// <summary>
/// Computes per-stream bitrate limits from a total bandwidth budget.
/// </summary>
public static class SenderBitratePlanner
{
    public static void BuildPerStreamPlan(
        int totalMaxMbps,
        int totalMinMbps,
        int streamCount,
        out ulong perStreamMaxBitrate,
        out ulong perStreamMinBitrate)
    {
        int safeStreamCount = Mathf.Max(1, streamCount);
        // Do not hard-cap sender budget here: use the value configured in SenderManager.
        int safeMaxMbps = Mathf.Max(1, totalMaxMbps);
        int safeMinMbps = Mathf.Clamp(totalMinMbps, 1, safeMaxMbps);

        perStreamMaxBitrate = (ulong)((long)safeMaxMbps * 1_000_000L / safeStreamCount);
        perStreamMinBitrate = (ulong)((long)safeMinMbps * 1_000_000L / safeStreamCount);
    }
}
