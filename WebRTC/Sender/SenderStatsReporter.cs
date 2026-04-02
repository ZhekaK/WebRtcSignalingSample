using System;
using System.Collections.Generic;
using System.Linq;
using Unity.WebRTC;
using UnityEngine;

internal sealed class SenderStatsReporter
{
    private struct OutboundStatsSnapshot
    {
        public long TimestampUs;
        public ulong BytesSent;
        public ulong PacketsSent;
        public uint FramesEncoded;
        public ulong QpSum;
        public double TotalEncodeTime;
        public double TotalPacketSendDelay;
        public ulong RetransmittedPacketsSent;
        public uint PliCount;
        public uint NackCount;
    }

    private readonly Dictionary<string, OutboundStatsSnapshot> _outboundStatsSnapshots = new();

    public void Reset()
    {
        _outboundStatsSnapshots.Clear();
    }

    public void Log(RTCStatsReport report, string label = null)
    {
        if (report == null)
            return;

        List<RTCOutboundRTPStreamStats> outboundVideoStats = new();
        List<RTCRemoteInboundRtpStreamStats> remoteInboundVideoStats = new();
        Dictionary<string, RTCCodecStats> codecsById = new(StringComparer.Ordinal);

        foreach (RTCStats stats in report.Stats.Values)
        {
            switch (stats)
            {
                case RTCOutboundRTPStreamStats outbound when IsVideoKind(outbound.kind):
                    outboundVideoStats.Add(outbound);
                    break;
                case RTCRemoteInboundRtpStreamStats remoteInbound when IsVideoKind(remoteInbound.kind):
                    remoteInboundVideoStats.Add(remoteInbound);
                    break;
                case RTCCodecStats codec:
                    if (!string.IsNullOrEmpty(codec.Id) && !codecsById.ContainsKey(codec.Id))
                        codecsById.Add(codec.Id, codec);
                    break;
            }
        }

        if (outboundVideoStats.Count == 0)
            return;

        double totalBitrateBps = 0d;
        double totalTargetBitrateBps = 0d;
        double totalFps = 0d;
        double totalQpDelta = 0d;
        ulong totalFramesEncodedDelta = 0;
        ulong totalPacketsSentDelta = 0;
        uint maxFrameWidth = 0;
        uint maxFrameHeight = 0;
        double totalEncodeTimeDeltaMs = 0d;
        double totalPacketSendDelayDeltaMs = 0d;
        ulong totalRetransmittedPacketsDelta = 0;
        uint totalPliDelta = 0;
        uint totalNackDelta = 0;

        HashSet<string> codecMimeTypes = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> limitationReasons = new(StringComparer.OrdinalIgnoreCase);

        foreach (RTCOutboundRTPStreamStats outbound in outboundVideoStats)
        {
            totalTargetBitrateBps += Math.Max(0d, outbound.targetBitrate);
            totalFps += Math.Max(0d, outbound.framesPerSecond);

            maxFrameWidth = Math.Max(maxFrameWidth, outbound.frameWidth);
            maxFrameHeight = Math.Max(maxFrameHeight, outbound.frameHeight);

            if (!string.IsNullOrWhiteSpace(outbound.qualityLimitationReason))
                limitationReasons.Add(outbound.qualityLimitationReason);

            if (!string.IsNullOrEmpty(outbound.codecId) &&
                codecsById.TryGetValue(outbound.codecId, out RTCCodecStats codecStats) &&
                !string.IsNullOrWhiteSpace(codecStats.mimeType))
            {
                codecMimeTypes.Add(codecStats.mimeType);
            }

            string statKey = string.IsNullOrEmpty(outbound.Id) ? outbound.ssrc.ToString() : outbound.Id;
            if (_outboundStatsSnapshots.TryGetValue(statKey, out OutboundStatsSnapshot previous))
            {
                long durationUs = outbound.Timestamp - previous.TimestampUs;
                if (durationUs > 0 && outbound.bytesSent >= previous.BytesSent)
                {
                    double durationSec = durationUs / 1_000_000d;
                    ulong bytesDelta = outbound.bytesSent - previous.BytesSent;
                    totalBitrateBps += 8d * bytesDelta / durationSec;
                }

                if (outbound.framesEncoded >= previous.FramesEncoded && outbound.qpSum >= previous.QpSum)
                {
                    uint framesDelta = outbound.framesEncoded - previous.FramesEncoded;
                    ulong qpDelta = outbound.qpSum - previous.QpSum;

                    if (framesDelta > 0)
                    {
                        totalFramesEncodedDelta += framesDelta;
                        totalQpDelta += qpDelta;
                    }
                }

                if (outbound.packetsSent >= previous.PacketsSent)
                    totalPacketsSentDelta += outbound.packetsSent - previous.PacketsSent;

                if (outbound.totalEncodeTime >= previous.TotalEncodeTime)
                    totalEncodeTimeDeltaMs += (outbound.totalEncodeTime - previous.TotalEncodeTime) * 1000d;

                if (outbound.totalPacketSendDelay >= previous.TotalPacketSendDelay)
                    totalPacketSendDelayDeltaMs += (outbound.totalPacketSendDelay - previous.TotalPacketSendDelay) * 1000d;

                if (outbound.retransmittedPacketsSent >= previous.RetransmittedPacketsSent)
                    totalRetransmittedPacketsDelta += outbound.retransmittedPacketsSent - previous.RetransmittedPacketsSent;

                if (outbound.pliCount >= previous.PliCount)
                    totalPliDelta += outbound.pliCount - previous.PliCount;

                if (outbound.nackCount >= previous.NackCount)
                    totalNackDelta += outbound.nackCount - previous.NackCount;
            }

            _outboundStatsSnapshots[statKey] = new OutboundStatsSnapshot
            {
                TimestampUs = outbound.Timestamp,
                BytesSent = outbound.bytesSent,
                PacketsSent = outbound.packetsSent,
                FramesEncoded = outbound.framesEncoded,
                QpSum = outbound.qpSum,
                TotalEncodeTime = outbound.totalEncodeTime,
                TotalPacketSendDelay = outbound.totalPacketSendDelay,
                RetransmittedPacketsSent = outbound.retransmittedPacketsSent,
                PliCount = outbound.pliCount,
                NackCount = outbound.nackCount
            };
        }

        double averageRttMs = double.NaN;
        double averageLossPercent = double.NaN;
        if (remoteInboundVideoStats.Count > 0)
        {
            double[] rttSamples = remoteInboundVideoStats
                .Select(stat => stat.roundTripTime)
                .Where(value => value > 0d)
                .Select(value => value * 1000d)
                .ToArray();

            if (rttSamples.Length > 0)
                averageRttMs = rttSamples.Average();

            double[] lossSamples = remoteInboundVideoStats
                .Select(stat => stat.fractionLost)
                .Where(value => value >= 0d)
                .Select(value => value * 100d)
                .ToArray();

            if (lossSamples.Length > 0)
                averageLossPercent = lossSamples.Average();
        }

        string codecText = codecMimeTypes.Count > 0
            ? string.Join(",", codecMimeTypes.OrderBy(value => value))
            : "unknown";
        string qualityLimitText = limitationReasons.Count > 0
            ? string.Join(",", limitationReasons.OrderBy(value => value))
            : "none";
        string resolutionText = maxFrameWidth > 0 && maxFrameHeight > 0
            ? $"{maxFrameWidth}x{maxFrameHeight}"
            : "n/a";
        string avgQpText = totalFramesEncodedDelta > 0
            ? (totalQpDelta / totalFramesEncodedDelta).ToString("F1")
            : "n/a";
        string rttText = double.IsNaN(averageRttMs)
            ? "n/a"
            : $"{averageRttMs:F1} ms";
        string lossText = double.IsNaN(averageLossPercent)
            ? "n/a"
            : $"{averageLossPercent:F2}%";
        string encodeText = totalFramesEncodedDelta > 0
            ? $"{(totalEncodeTimeDeltaMs / totalFramesEncodedDelta):F2} ms"
            : "n/a";
        string sendDelayText = totalPacketsSentDelta > 0
            ? $"{(totalPacketSendDelayDeltaMs / totalPacketsSentDelta):F2} ms"
            : "n/a";
        string prefix = string.IsNullOrWhiteSpace(label)
            ? "[WebRTC Sender Stats]"
            : $"[WebRTC Sender Stats {label}]";

        Debug.Log(
            $"{prefix} tx={(totalBitrateBps / 1_000_000d):F1} Mbps " +
            $"target={(totalTargetBitrateBps / 1_000_000d):F1} Mbps streams={outboundVideoStats.Count} " +
            $"codec={codecText} res={resolutionText} fps={totalFps:F1} avgQp={avgQpText} " +
            $"rtt={rttText} loss={lossText} enc={encodeText} send={sendDelayText} " +
            $"nackDelta={totalNackDelta} pliDelta={totalPliDelta} retransDelta={totalRetransmittedPacketsDelta} " +
            $"limit={qualityLimitText}");
    }

    private static bool IsVideoKind(string kind)
    {
        return string.Equals(kind, "video", StringComparison.OrdinalIgnoreCase);
    }
}
