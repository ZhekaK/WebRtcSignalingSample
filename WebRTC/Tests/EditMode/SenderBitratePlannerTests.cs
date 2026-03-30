using NUnit.Framework;

public class SenderBitratePlannerTests
{
    [Test]
    public void BuildPerStreamPlan_SplitsBudgetAcrossStreams()
    {
        SenderBitratePlanner.BuildPerStreamPlan(
            totalMaxMbps: 280,
            totalMinMbps: 120,
            streamCount: 4,
            out var perStreamMax,
            out var perStreamMin);

        Assert.AreEqual(70_000_000UL, perStreamMax);
        Assert.AreEqual(30_000_000UL, perStreamMin);
    }

    [Test]
    public void BuildPerStreamPlan_DoesNotHardCapConfiguredBudget()
    {
        SenderBitratePlanner.BuildPerStreamPlan(
            totalMaxMbps: 500,
            totalMinMbps: 400,
            streamCount: 2,
            out var perStreamMax,
            out var perStreamMin);

        Assert.AreEqual(250_000_000UL, perStreamMax);
        Assert.AreEqual(200_000_000UL, perStreamMin);
    }

    [Test]
    public void BuildPerStreamPlan_UsesAtLeastOneStream()
    {
        SenderBitratePlanner.BuildPerStreamPlan(
            totalMaxMbps: 100,
            totalMinMbps: 50,
            streamCount: 0,
            out var perStreamMax,
            out var perStreamMin);

        Assert.AreEqual(100_000_000UL, perStreamMax);
        Assert.AreEqual(50_000_000UL, perStreamMin);
    }

    [Test]
    public void BuildPerStreamPlan_ClampsMinBudgetToMaxBudget()
    {
        SenderBitratePlanner.BuildPerStreamPlan(
            totalMaxMbps: 60,
            totalMinMbps: 120,
            streamCount: 3,
            out var perStreamMax,
            out var perStreamMin);

        Assert.AreEqual(20_000_000UL, perStreamMax);
        Assert.AreEqual(20_000_000UL, perStreamMin);
    }
}
