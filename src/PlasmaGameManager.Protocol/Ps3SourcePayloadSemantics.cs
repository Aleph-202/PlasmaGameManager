using System.Text;

namespace PlasmaGameManager.Protocol;

public static class Ps3SourcePayloadSemantics
{
    private static readonly string[] KnownEmbeddedMarkers =
    [
        "COc",
        "PNG",
        "DSC",
        "FrozenState",
        "TID=",
        "LID=",
        "PID=",
        "GID=",
        "MOTD",
        "A2S_",
        "PRD",
        "PED",
        "GSD",
        "RTD",
        "ORD",
        "TFB",
        "RGD",
        "TFX",
        "PAE",
        "WED"
    ];

    public static Ps3SourcePayloadSemanticInfo Analyze(ReadOnlySpan<byte> body)
    {
        var nativeEntityDelta = TryAnalyzeNativeEntityDelta(body);
        if (nativeEntityDelta is not null)
        {
            return nativeEntityDelta;
        }

        var markers = FindEmbeddedMarkers(body);
        var embeddedRecords = markers.Any(static marker => marker.Marker is "COc" or "PNG" or "DSC")
            ? Ps3SourceEmbeddedObjectRecords.Extract(body)
            : [];
        var nativeMessageRole = ClassifyNativeMessageRole(body);
        var printableRatio = PrintableRatio(body);
        var kind = markers.Length switch
        {
            _ when nativeMessageRole == Ps3SourcePayloadSemanticRole.NativeLzssCompressed
                => Ps3SourcePayloadSemanticKind.NativeWrappedPayload,
            _ when nativeMessageRole == Ps3SourcePayloadSemanticRole.NativeObjectStreamBootstrap
                => Ps3SourcePayloadSemanticKind.NativeObjectStreamPayload,
            > 0 when markers.Any(static marker => marker.Marker == "PRD")
                => Ps3SourcePayloadSemanticKind.EntityDeltaPayload,
            > 0 when embeddedRecords.Length > 0
                && embeddedRecords.All(static record => record.Role == Ps3SourceEmbeddedObjectRecordRole.MarkerCollisionNoise)
                => Ps3SourcePayloadSemanticKind.MarkerCollisionNoisePayload,
            > 0 when markers.Any(static marker => marker.Marker is "COc" or "PNG" or "DSC" or "FrozenState")
                => Ps3SourcePayloadSemanticKind.EmbeddedObjectOrRosterPayload,
            > 0 => Ps3SourcePayloadSemanticKind.EmbeddedTextCommandPayload,
            _ when body.Length == 0 => Ps3SourcePayloadSemanticKind.EmptyPayload,
            _ when body.Length < 32 => Ps3SourcePayloadSemanticKind.BinaryShortControlPayload,
            _ when Entropy(body) >= 7.0 => Ps3SourcePayloadSemanticKind.HighEntropyPayload,
            _ when printableRatio >= 0.35 => Ps3SourcePayloadSemanticKind.MixedTextBinaryPayload,
            _ => Ps3SourcePayloadSemanticKind.BinaryGameplayPayload
        };

        return new Ps3SourcePayloadSemanticInfo(
            kind,
            nativeMessageRole ?? ClassifyRole(kind, embeddedRecords),
            markers,
            Math.Round(printableRatio, 4),
            Math.Round(Entropy(body), 4),
            AsciiPreview(body, 96),
            NativeFieldSummary(body, nativeMessageRole, embeddedRecords));
    }

    private static Ps3SourcePayloadSemanticInfo? TryAnalyzeNativeEntityDelta(ReadOnlySpan<byte> body)
    {
        if (!Ps3SourceEntityDeltaFrameBuilder.TryDecodeNativeRecord(body, out var record, out _))
        {
            return null;
        }

        var rawMarkers = FindEmbeddedMarkers(record.RawPayload);
        var rawPreview = AsciiPreview(record.RawPayload, 4096);
        var isPlayerResource = rawMarkers.Any(static marker => marker.Marker == "PRD")
            || rawPreview.Contains("m_bConnected", StringComparison.Ordinal)
            || string.Equals(record.ObjectName, "CPlayerResource", StringComparison.Ordinal);
        var isPlayerEntity = rawMarkers.Any(static marker => marker.Marker == "PED")
            || rawPreview.Contains("DT_TFPlayer", StringComparison.Ordinal)
            || rawPreview.Contains("m_vecOrigin", StringComparison.Ordinal)
            || string.Equals(record.ObjectName, "CTFPlayer", StringComparison.Ordinal);
        var isGameplayState = rawMarkers.Any(static marker => marker.Marker == "GSD")
            || rawPreview.Contains("DT_TFGameRules", StringComparison.Ordinal)
            || rawPreview.Contains("m_iRoundState", StringComparison.Ordinal)
            || string.Equals(record.ObjectName, "CTFGameRulesProxy", StringComparison.Ordinal);
        var isRoundTimer = rawMarkers.Any(static marker => marker.Marker == "RTD")
            || rawPreview.Contains("DT_TeamRoundTimer", StringComparison.Ordinal)
            || string.Equals(record.ObjectName, "CTeamRoundTimer", StringComparison.Ordinal);
        var isObjectiveResource = rawMarkers.Any(static marker => marker.Marker == "ORD")
            || rawPreview.Contains("DT_BaseTeamObjectiveResource", StringComparison.Ordinal)
            || rawPreview.Contains("DT_TFObjectiveResource", StringComparison.Ordinal)
            || string.Equals(record.ObjectName, "CTFObjectiveResource", StringComparison.Ordinal);
        var isFireBullets = rawMarkers.Any(static marker => marker.Marker == "TFB")
            || rawPreview.Contains("DT_TEFireBullets", StringComparison.Ordinal)
            || string.Equals(record.ObjectName, "CTEFireBullets", StringComparison.Ordinal);
        var isRagdoll = rawMarkers.Any(static marker => marker.Marker == "RGD")
            || rawPreview.Contains("DT_TFRagdoll", StringComparison.Ordinal)
            || string.Equals(record.ObjectName, "CTFRagdoll", StringComparison.Ordinal);
        var isTfExplosion = rawMarkers.Any(static marker => marker.Marker == "TFX")
            || rawPreview.Contains("DT_TETFExplosion", StringComparison.Ordinal)
            || string.Equals(record.ObjectName, "CTETFExplosion", StringComparison.Ordinal);
        var isPlayerAnimEvent = rawMarkers.Any(static marker => marker.Marker == "PAE")
            || rawPreview.Contains("DT_TEPlayerAnimEvent", StringComparison.Ordinal)
            || string.Equals(record.ObjectName, "CTEPlayerAnimEvent", StringComparison.Ordinal);
        var isWeaponEntity = rawMarkers.Any(static marker => marker.Marker == "WED")
            || rawPreview.Contains("DT_BaseCombatWeapon", StringComparison.Ordinal)
            || rawPreview.Contains("DT_TFWeaponBase", StringComparison.Ordinal)
            || string.Equals(record.ObjectName, "CTFWeaponBase", StringComparison.Ordinal);
        if (!isPlayerResource && !isPlayerEntity && !isGameplayState && !isRoundTimer && !isObjectiveResource && !isFireBullets && !isRagdoll && !isTfExplosion && !isPlayerAnimEvent && !isWeaponEntity)
        {
            return null;
        }

        var role = Ps3SourcePayloadSemanticRole.NativePlayerResourceDelta;
        if (isRoundTimer)
        {
            role = Ps3SourcePayloadSemanticRole.NativeTeamRoundTimerDelta;
        }
        else if (isObjectiveResource)
        {
            role = Ps3SourcePayloadSemanticRole.NativeObjectiveResourceDelta;
        }
        else if (isFireBullets)
        {
            role = Ps3SourcePayloadSemanticRole.NativeFireBulletsEvent;
        }
        else if (isRagdoll)
        {
            role = Ps3SourcePayloadSemanticRole.NativeTfRagdollDelta;
        }
        else if (isTfExplosion)
        {
            role = Ps3SourcePayloadSemanticRole.NativeTfExplosionEvent;
        }
        else if (isPlayerAnimEvent)
        {
            role = Ps3SourcePayloadSemanticRole.NativePlayerAnimEvent;
        }
        else if (isWeaponEntity)
        {
            role = Ps3SourcePayloadSemanticRole.NativeWeaponEntityDelta;
        }
        else if (isGameplayState)
        {
            role = Ps3SourcePayloadSemanticRole.NativeGameplayStateDelta;
        }
        else if (isPlayerEntity)
        {
            role = Ps3SourcePayloadSemanticRole.NativePlayerEntityDelta;
        }

        return new Ps3SourcePayloadSemanticInfo(
            Ps3SourcePayloadSemanticKind.EntityDeltaPayload,
            role,
            rawMarkers,
            Math.Round(PrintableRatio(record.RawPayload), 4),
            Math.Round(Entropy(record.RawPayload), 4),
            rawPreview);
    }

    private static Ps3SourcePayloadSemanticRole? ClassifyNativeMessageRole(ReadOnlySpan<byte> body)
    {
        if (Ps3SourceLzss.IsWrapped(body))
        {
            return Ps3SourcePayloadSemanticRole.NativeLzssCompressed;
        }

        if (Ps3SourceObjectStream.TryDecode(body, out var objectStream)
            && objectStream.Payload.Length > 0
            && Ps3SourceNetMessages.TryReadMessageType(objectStream.Payload, out _))
        {
            return Ps3SourcePayloadSemanticRole.NativeObjectStreamBootstrap;
        }

        if (body.Length < 5
            || body[0] != 0xff
            || body[1] != 0xff
            || body[2] != 0xff
            || body[3] != 0xff)
        {
            return null;
        }

        return body[4] switch
        {
            0x41 => Ps3SourcePayloadSemanticRole.NativeHudPlayerObjectUpdate41,
            0x44 => Ps3SourcePayloadSemanticRole.NativePlayerSummary44,
            0x45 => Ps3SourcePayloadSemanticRole.NativeResourceStringTable45,
            0x49 => Ps3SourcePayloadSemanticRole.NativeServerInfo49,
            0x6b => Ps3SourcePayloadSemanticRole.NativeGameplayStatTimesUsed6b,
            _ => null
        };
    }

    private static string NativeFieldSummary(
        ReadOnlySpan<byte> body,
        Ps3SourcePayloadSemanticRole? role,
        IReadOnlyList<Ps3SourceEmbeddedObjectRecord> embeddedRecords)
    {
        var nativeSummary = role switch
        {
            Ps3SourcePayloadSemanticRole.NativeHudPlayerObjectUpdate41
                when Ps3SourceNativeMessages.TryDecodeHudPlayerObjectUpdate(body, out var hudUpdate)
                    => hudUpdate.Update.SecondaryValue is { } secondaryValue
                        ? $"hud-object primary={hudUpdate.Update.PrimaryValue} secondary={secondaryValue} label={hudUpdate.Update.Label ?? ""}"
                        : $"hud-object primary={hudUpdate.Update.PrimaryValue}",
            Ps3SourcePayloadSemanticRole.NativePlayerSummary44
                when Ps3SourceNativeMessages.TryDecodePlayerSummary(body, out var summary)
                    => summary.Entries.Length == 0
                        ? $"players={summary.SummaryHeaderValue}"
                        : $"players={summary.SummaryHeaderValue} first={summary.Entries[0].DisplayName} score={summary.Entries[0].ScoreOrStat}",
            Ps3SourcePayloadSemanticRole.NativeResourceStringTable45
                when Ps3SourceNativeMessages.TryDecodeResourceStringTable(body, out var table)
                    => table.Entries.Length == 0
                        ? "resources=0"
                        : $"resources={table.Entries.Length} first={table.Entries[0].ResourceName}:{table.Entries[0].Classification}",
            Ps3SourcePayloadSemanticRole.NativeServerInfo49
                when Ps3SourceNativeMessages.TryDecodeServerInfo(body, out var serverInfo)
                    => $"server={serverInfo.Info.ServerName} map={serverInfo.Info.MapName} players={serverInfo.Info.CurrentPlayers}/{serverInfo.Info.MaxPlayers} address={serverInfo.Info.ConnectionAddress}",
            Ps3SourcePayloadSemanticRole.NativeGameplayStatTimesUsed6b
                when Ps3SourceNativeMessages.TryDecodeGameplayStatTimesUsed(body, out var stat)
                    => $"timesused kind={stat.Update.VersionOrKind} state={stat.Update.State} value={stat.Update.Value} object={stat.Update.ObjectName} class={stat.Update.Classification}",
            Ps3SourcePayloadSemanticRole.NativeObjectStreamBootstrap
                when Ps3SourceObjectStream.TryDecode(body, out var objectStream)
                    => NativeObjectStreamSummary(objectStream),
            _ => ""
        };
        if (!string.IsNullOrWhiteSpace(nativeSummary))
        {
            return nativeSummary;
        }

        var validRecords = embeddedRecords
            .Where(static record => record.Role != Ps3SourceEmbeddedObjectRecordRole.MarkerCollisionNoise)
            .ToArray();
        if (validRecords.Length == 0)
        {
            return "";
        }

        return EmbeddedRecordFieldSummary(validRecords);
    }

    private static string NativeObjectStreamSummary(Ps3SourceDecodedObjectStreamRecord objectStream)
    {
        var firstMessage = Ps3SourceNetMessages.TryReadMessageType(objectStream.Payload, out var messageType)
            ? Ps3SourceNetMessages.MessageTypeName(messageType)
            : "unknown";
        return $"kind={objectStream.MessageKind} owner={objectStream.OwnerOrCallbackId:x8} seq={objectStream.SequenceA}/{objectStream.SequenceB} payload={objectStream.Payload.Length} first={firstMessage}";
    }

    private static string EmbeddedRecordFieldSummary(IReadOnlyList<Ps3SourceEmbeddedObjectRecord> records)
    {
        var roleCounts = records
            .GroupBy(static record => record.Role)
            .OrderBy(static group => group.Key)
            .Select(static group => $"{group.Key}={group.Count()}");
        var first = records[0];
        var builder = new StringBuilder();
        builder.Append("records=");
        builder.Append(records.Count);
        builder.Append(" roles=");
        builder.Append(string.Join(",", roleCounts));
        builder.Append(" first=");
        builder.Append(first.Marker);
        builder.Append(" object=");
        builder.Append(Hex(first.ObjectId));
        if (first.Role == Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink)
        {
            builder.Append(" linked=");
            builder.Append(Hex(first.LinkedObjectId));
        }
        else
        {
            builder.Append(" owner=");
            builder.Append(Hex(first.FieldB));
            builder.Append(" class=");
            builder.Append(Hex(first.ClassId));
            if (!string.IsNullOrWhiteSpace(first.DisplayName))
            {
                builder.Append(" name=");
                builder.Append(first.DisplayName);
            }
        }

        return builder.ToString();
    }

    private static string Hex(uint? value)
    {
        return value is null ? "????????" : value.Value.ToString("x8");
    }

    public static Ps3SourcePayloadSemanticInfo AnalyzeInitialClientHandoffProbe(ReadOnlySpan<byte> body)
    {
        var semantic = Analyze(body);
        if (body.Length is >= 32 and <= 50
            && semantic.EmbeddedMarkers.Length == 0
            && semantic.Role is Ps3SourcePayloadSemanticRole.MixedTextBinary
                or Ps3SourcePayloadSemanticRole.BinaryGameplay
                or Ps3SourcePayloadSemanticRole.HighEntropyBinary)
        {
            return semantic with { Role = Ps3SourcePayloadSemanticRole.InitialHandoffClientProbe };
        }

        return semantic;
    }

    public static Ps3SourceEmbeddedMarker[] FindEmbeddedMarkers(ReadOnlySpan<byte> body)
    {
        var markers = new List<Ps3SourceEmbeddedMarker>();
        foreach (var marker in KnownEmbeddedMarkers)
        {
            var bytes = Encoding.ASCII.GetBytes(marker);
            var offset = 0;
            while (offset <= body.Length - bytes.Length)
            {
                var found = IndexOf(body[offset..], bytes);
                if (found < 0)
                {
                    break;
                }

                var absolute = offset + found;
                markers.Add(new Ps3SourceEmbeddedMarker(marker, absolute));
                offset = absolute + bytes.Length;
            }
        }

        return markers
            .OrderBy(static marker => marker.Offset)
            .ThenBy(static marker => marker.Marker, StringComparer.Ordinal)
            .ToArray();
    }

    private static Ps3SourcePayloadSemanticRole ClassifyRole(
        Ps3SourcePayloadSemanticKind kind,
        Ps3SourceEmbeddedObjectRecord[] embeddedRecords)
    {
        if (kind == Ps3SourcePayloadSemanticKind.EntityDeltaPayload)
        {
            return Ps3SourcePayloadSemanticRole.NativePlayerResourceDelta;
        }

        if (embeddedRecords.Length > 0)
        {
            var validRecords = embeddedRecords
                .Where(static record => record.Role != Ps3SourceEmbeddedObjectRecordRole.MarkerCollisionNoise)
                .ToArray();
            if (validRecords.Length == 0)
            {
                return Ps3SourcePayloadSemanticRole.MarkerCollisionNoise;
            }

            var validRoles = validRecords
                .Select(static record => record.Role)
                .Distinct()
                .OrderBy(static role => role)
                .ToArray();
            if (validRoles.Length == 1)
            {
                return validRoles[0] switch
                {
                    Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink => Ps3SourcePayloadSemanticRole.PlayerStateLinkBatch,
                    Ps3SourceEmbeddedObjectRecordRole.PlayerObject => Ps3SourcePayloadSemanticRole.PlayerObjectBatch,
                    Ps3SourceEmbeddedObjectRecordRole.PlayerDescriptor => Ps3SourcePayloadSemanticRole.PlayerDescriptorBatch,
                    Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject => Ps3SourcePayloadSemanticRole.FrozenStateBatch,
                    _ => Ps3SourcePayloadSemanticRole.MixedEmbeddedObjectBatch
                };
            }

            if (validRoles.Contains(Ps3SourceEmbeddedObjectRecordRole.PlayerObject)
                || validRoles.Contains(Ps3SourceEmbeddedObjectRecordRole.PlayerDescriptor))
            {
                return Ps3SourcePayloadSemanticRole.PlayerRosterObjectBatch;
            }

            if (validRoles.Contains(Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject))
            {
                return Ps3SourcePayloadSemanticRole.FrozenStateObjectBatch;
            }

            return Ps3SourcePayloadSemanticRole.MixedEmbeddedObjectBatch;
        }

        return kind switch
        {
            Ps3SourcePayloadSemanticKind.EmptyPayload => Ps3SourcePayloadSemanticRole.Empty,
            Ps3SourcePayloadSemanticKind.BinaryShortControlPayload => Ps3SourcePayloadSemanticRole.ShortControl,
            Ps3SourcePayloadSemanticKind.EmbeddedTextCommandPayload => Ps3SourcePayloadSemanticRole.EmbeddedTextCommand,
            Ps3SourcePayloadSemanticKind.EntityDeltaPayload => Ps3SourcePayloadSemanticRole.NativePlayerResourceDelta,
            Ps3SourcePayloadSemanticKind.MarkerCollisionNoisePayload => Ps3SourcePayloadSemanticRole.MarkerCollisionNoise,
            Ps3SourcePayloadSemanticKind.MixedTextBinaryPayload => Ps3SourcePayloadSemanticRole.MixedTextBinary,
            Ps3SourcePayloadSemanticKind.HighEntropyPayload => Ps3SourcePayloadSemanticRole.HighEntropyBinary,
            _ => Ps3SourcePayloadSemanticRole.BinaryGameplay
        };
    }

    private static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static double PrintableRatio(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        var printable = 0;
        foreach (var b in data)
        {
            if (b is >= 0x20 and <= 0x7e or 0x09 or 0x0a or 0x0d)
            {
                printable++;
            }
        }

        return printable / (double)data.Length;
    }

    private static string AsciiPreview(ReadOnlySpan<byte> body, int maxBytes)
    {
        var count = Math.Min(body.Length, maxBytes);
        return new string(body[..count]
            .ToArray()
            .Select(static b => b is >= 32 and <= 126 ? (char)b : '.')
            .ToArray());
    }

    private static double Entropy(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var b in data)
        {
            counts[b]++;
        }

        var entropy = 0.0;
        foreach (var count in counts)
        {
            if (count == 0)
            {
                continue;
            }

            var p = count / (double)data.Length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }
}

public enum Ps3SourcePayloadSemanticKind
{
    EmptyPayload,
    BinaryShortControlPayload,
    EmbeddedObjectOrRosterPayload,
    EmbeddedTextCommandPayload,
    EntityDeltaPayload,
    MarkerCollisionNoisePayload,
    MixedTextBinaryPayload,
    BinaryGameplayPayload,
    HighEntropyPayload,
    NativeWrappedPayload,
    NativeObjectStreamPayload
}

public enum Ps3SourcePayloadSemanticRole
{
    Empty,
    ShortControl,
    PlayerStateLinkBatch,
    PlayerObjectBatch,
    PlayerDescriptorBatch,
    FrozenStateBatch,
    PlayerRosterObjectBatch,
    FrozenStateObjectBatch,
    MixedEmbeddedObjectBatch,
    MarkerCollisionNoise,
    EmbeddedTextCommand,
    MixedTextBinary,
    InitialHandoffClientProbe,
    BinaryGameplay,
    HighEntropyBinary,
    NativeHudPlayerObjectUpdate41,
    NativePlayerSummary44,
    NativeResourceStringTable45,
    NativeServerInfo49,
    NativeGameplayStatTimesUsed6b,
    NativePlayerResourceDelta,
    NativePlayerEntityDelta,
    NativeGameplayStateDelta,
    NativeTeamRoundTimerDelta,
    NativeObjectiveResourceDelta,
    NativeFireBulletsEvent,
    NativeTfRagdollDelta,
    NativeTfExplosionEvent,
    NativePlayerAnimEvent,
    NativeWeaponEntityDelta,
    NativeLzssCompressed,
    NativeObjectStreamBootstrap
}

public sealed record Ps3SourcePayloadSemanticInfo(
    Ps3SourcePayloadSemanticKind Kind,
    Ps3SourcePayloadSemanticRole Role,
    Ps3SourceEmbeddedMarker[] EmbeddedMarkers,
    double PrintableRatio,
    double Entropy,
    string AsciiPreview,
    string NativeFieldSummary = "");

public sealed record Ps3SourceEmbeddedMarker(
    string Marker,
    int Offset);
