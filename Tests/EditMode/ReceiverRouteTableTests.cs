using NUnit.Framework;

public class ReceiverRouteTableTests
{
    [Test]
    public void Apply_ReturnsFalse_WhenMapIsInvalid()
    {
        var table = new ReceiverRouteTable();

        Assert.IsFalse(table.Apply(null));
        Assert.IsFalse(table.Apply(new TrackMapMessage()));
    }

    [Test]
    public void Apply_BuildsRouteAndResolvesByMidAndTrackId()
    {
        var table = new ReceiverRouteTable();
        var map = new TrackMapMessage
        {
            tracks = new[]
            {
                new TrackMapEntry { displayIndex = 0, trackId = "track-a", transceiverMid = "0" },
                new TrackMapEntry { displayIndex = 1, trackId = "track-b", transceiverMid = "1" }
            }
        };

        Assert.IsTrue(table.Apply(map));
        Assert.AreEqual(2, table.RequiredDisplaySlots);

        Assert.IsTrue(table.TryResolve("track-a", string.Empty, out int byTrack));
        Assert.AreEqual(0, byTrack);

        Assert.IsTrue(table.TryResolve(string.Empty, "1", out int byMid));
        Assert.AreEqual(1, byMid);
    }

    [Test]
    public void GetMappedDisplayIndices_ReturnsUniqueIndices()
    {
        var table = new ReceiverRouteTable();
        var map = new TrackMapMessage
        {
            tracks = new[]
            {
                new TrackMapEntry { displayIndex = 2, trackId = "track-c", transceiverMid = "2" },
                new TrackMapEntry { displayIndex = 2, trackId = "track-c-alt", transceiverMid = "2-alt" }
            }
        };

        Assert.IsTrue(table.Apply(map));

        var indices = table.GetMappedDisplayIndices();

        Assert.AreEqual(1, indices.Count);
        Assert.IsTrue(indices.Contains(2));
    }
}
