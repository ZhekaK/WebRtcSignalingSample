using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Maintains the active mapping from track identity to display index.
/// </summary>
public sealed class ReceiverRouteTable
{
    private readonly Dictionary<string, int> _trackIdToDisplayIndex = new();
    private readonly Dictionary<string, int> _midToDisplayIndex = new();

    public int RequiredDisplaySlots { get; private set; }

    public IReadOnlyDictionary<string, int> TrackMap => _trackIdToDisplayIndex;
    public IReadOnlyDictionary<string, int> MidMap => _midToDisplayIndex;

    public HashSet<int> GetMappedDisplayIndices()
    {
        var result = new HashSet<int>(_trackIdToDisplayIndex.Values);
        foreach (var value in _midToDisplayIndex.Values)
            result.Add(value);

        return result;
    }

    public void Clear()
    {
        _trackIdToDisplayIndex.Clear();
        _midToDisplayIndex.Clear();
        RequiredDisplaySlots = 0;
    }

    public bool Apply(TrackMapMessage map)
    {
        if (map?.tracks == null || map.tracks.Length == 0)
            return false;

        var validEntries = map.tracks
            .Where(entry => entry != null && entry.displayIndex >= 0)
            .ToArray();

        if (validEntries.Length == 0)
            return false;

        _trackIdToDisplayIndex.Clear();
        _midToDisplayIndex.Clear();

        foreach (var entry in validEntries)
        {
            if (!string.IsNullOrEmpty(entry.trackId))
                _trackIdToDisplayIndex[entry.trackId] = entry.displayIndex;

            if (!string.IsNullOrEmpty(entry.transceiverMid))
                _midToDisplayIndex[entry.transceiverMid] = entry.displayIndex;
        }

        RequiredDisplaySlots = validEntries.Max(entry => entry.displayIndex) + 1;
        return true;
    }

    public bool TryResolve(string trackId, string mid, out int displayIndex)
    {
        if (!string.IsNullOrEmpty(mid) && _midToDisplayIndex.TryGetValue(mid, out displayIndex))
            return true;

        if (!string.IsNullOrEmpty(trackId) && _trackIdToDisplayIndex.TryGetValue(trackId, out displayIndex))
            return true;

        displayIndex = -1;
        return false;
    }
}
