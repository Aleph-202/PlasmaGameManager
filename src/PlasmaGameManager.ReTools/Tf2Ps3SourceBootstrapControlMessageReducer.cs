using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourceBootstrapControlMessageReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly BootstrapControlSpec[] Specs =
    [
        new(
            "NET_SignonState",
            "008cd0b8",
            "bootstrap-signon-state",
            "Writes the Source signon state transitions embedded in the TF.elf object-stream bootstrap.",
            [
                Field("message-type", "GetType vtable slot +0x20", "WriteUBitLong(5)", "uVar1 = (*(code *)**(undefined4 **)(*param_1 + 0x20))(param_1);"),
                Field("m_nSignonState", "+0x10 / param_1[4]", "WriteByte / 8 bits", "FUN_00870c28(param_2,param_1[4]);"),
                Field("m_nSpawnCount", "+0x14 / param_1[5]", "WriteLong / 32 bits", "FUN_0086caf8(param_2,param_1[5]);")
            ]),
        new(
            "SVC_SetView",
            "008cd018",
            "bootstrap-set-view",
            "Writes the local view entity index between signon state 4 and signon state 5.",
            [
                Field("message-type", "GetType vtable slot +0x20", "WriteUBitLong(5)", "uVar1 = (*(code *)**(undefined4 **)(*param_1 + 0x20))(param_1);"),
                Field("m_nEntityIndex", "+0x10 / param_1[4]", "WriteUBitLong(11)", "_opd_FUN_008d3ef0(param_2,param_1[4],0xb);")
            ])
    ];

    public static async Task<Tf2Ps3SourceBootstrapControlMessageReport> ReduceAsync(
        string cExportPath,
        string vtableCatalogPath,
        string objectStreamBootstrapPath,
        string outputPath)
    {
        var cExport = await File.ReadAllTextAsync(cExportPath);
        using var vtableDocument = JsonDocument.Parse(await File.ReadAllTextAsync(vtableCatalogPath));
        using var objectStreamDocument = JsonDocument.Parse(await File.ReadAllTextAsync(objectStreamBootstrapPath));

        var messages = vtableDocument.RootElement.GetProperty("Messages").EnumerateArray().ToArray();
        var controls = Specs
            .Select(spec => BuildControl(cExport, messages, spec))
            .ToArray();
        var callsites = ExtractCallsites(objectStreamDocument.RootElement);
        var report = new Tf2Ps3SourceBootstrapControlMessageReport(
            "tf2ps3-source-bootstrap-control-message-map",
            "Names the small TF.elf bootstrap control writers that sit inside the Source object-stream bootstrap batches. These are normal Source netmessages, not opaque EA/GameManager payloads.",
            new Tf2Ps3SourceBootstrapControlInputs(cExportPath, vtableCatalogPath, objectStreamBootstrapPath),
            new Tf2Ps3SourceBootstrapControlSummary(
                controls.Length,
                controls.Count(static control => control.VtableMatched),
                controls.Sum(static control => control.Fields.Length),
                callsites.Length,
                callsites.Count(static callsite => callsite.WriterFunction == "008cd0b8"),
                callsites.Count(static callsite => callsite.WriterFunction == "008cd018")),
            controls,
            callsites,
            [
                "008cd0b8 is NET_SignonState::WriteToBuffer: 5-bit type, 8-bit signon state, 32-bit spawn count.",
                "008cd018 is SVC_SetView::WriteToBuffer: 5-bit type, 11-bit entity index.",
                "The bootstrap object-stream batch should include signon state 3 after server-info/send-table/string-table setup, signon state 4 after class-info setup, then SVC_SetView and signon state 5 before the later update-string-table route."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceBootstrapControlMessage BuildControl(
        string cExport,
        JsonElement[] messages,
        BootstrapControlSpec spec)
    {
        var message = messages.Single(message => ReadString(message, "ClassName") == spec.ClassName);
        var table = message.GetProperty("CandidateVtables").EnumerateArray().Single();
        var slots = table.GetProperty("Slots").EnumerateArray().ToArray();
        var writeSlot = slots.Single(slot => ReadString(slot, "Role") == "WriteToBuffer");
        var getTypeSlot = slots.Single(slot => ReadString(slot, "Role") == "GetType");
        var body = ExtractFunctionBody(cExport, spec.WriteFunction);
        return new Tf2Ps3SourceBootstrapControlMessage(
            spec.ClassName,
            ReadString(message, "NetworkNameString"),
            message.GetProperty("SourceMessageId").GetInt32(),
            spec.Role,
            spec.Meaning,
            spec.WriteFunction,
            ReadString(writeSlot, "EntryAddress").EndsWith(spec.WriteFunction, StringComparison.OrdinalIgnoreCase),
            ExtractVptr(table),
            ReadString(getTypeSlot, "EntryAddress"),
            spec.Fields
                .Select(field => new Tf2Ps3SourceBootstrapControlField(
                    field.Name,
                    field.Offset,
                    field.Encoding,
                    field.EvidenceToken,
                    body.Contains(field.EvidenceToken, StringComparison.Ordinal)))
                .ToArray(),
            body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.Contains("008d3ef0", StringComparison.Ordinal)
                    || line.Contains("00870c28", StringComparison.Ordinal)
                    || line.Contains("0086caf8", StringComparison.Ordinal)
                    || line.Contains("+ 0x20", StringComparison.Ordinal)
                    || line.Contains("param_1[4]", StringComparison.Ordinal)
                    || line.Contains("param_1[5]", StringComparison.Ordinal))
                .Take(12)
                .ToArray());
    }

    private static string ExtractVptr(JsonElement table)
    {
        foreach (var evidence in table.GetProperty("Evidence").EnumerateArray())
        {
            var text = evidence.GetString() ?? "";
            var match = VptrEvidenceRegex().Match(text);
            if (match.Success)
            {
                return $"0x{match.Groups["address"].Value}";
            }
        }

        return "";
    }

    private static Tf2Ps3SourceBootstrapControlCallsite[] ExtractCallsites(JsonElement objectStreamRoot)
    {
        var rows = new List<Tf2Ps3SourceBootstrapControlCallsite>();
        foreach (var target in objectStreamRoot.GetProperty("Targets").EnumerateArray())
        {
            var source = ReadString(target, "Address");
            foreach (var call in target.GetProperty("Calls").EnumerateArray())
            {
                var writer = ReadString(call, "TargetFunction");
                if (writer is not ("008cd0b8" or "008cd018"))
                {
                    continue;
                }

                rows.Add(new Tf2Ps3SourceBootstrapControlCallsite(
                    source,
                    writer,
                    ReadString(call, "Role"),
                    call.GetProperty("Line").GetInt32(),
                    ReadString(call, "Statement"),
                    ReadString(call, "Preview")));
            }
        }

        return rows.ToArray();
    }

    private static string ExtractFunctionBody(string source, string function)
    {
        var marker = $"_opd_FUN_{function}";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        var next = FunctionStartRegex().Match(source, start + marker.Length);
        return next.Success
            ? source[start..next.Index]
            : source[start..];
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) ? value.GetString() ?? "" : "";
    }

    private static BootstrapControlFieldSpec Field(
        string name,
        string offset,
        string encoding,
        string evidenceToken)
    {
        return new BootstrapControlFieldSpec(name, offset, encoding, evidenceToken);
    }

    [GeneratedRegex(@"(?m)^\S.*_opd_FUN_[0-9a-f]{8}\(")]
    private static partial Regex FunctionStartRegex();

    [GeneratedRegex(@"candidate object vptr starts at 0x(?<address>[0-9a-f]+)", RegexOptions.IgnoreCase)]
    private static partial Regex VptrEvidenceRegex();
}

public sealed record Tf2Ps3SourceBootstrapControlMessageReport(
    string Status,
    string Note,
    Tf2Ps3SourceBootstrapControlInputs Inputs,
    Tf2Ps3SourceBootstrapControlSummary Summary,
    Tf2Ps3SourceBootstrapControlMessage[] Messages,
    Tf2Ps3SourceBootstrapControlCallsite[] BootstrapCallsites,
    string[] Findings);

public sealed record Tf2Ps3SourceBootstrapControlInputs(
    string CExportPath,
    string VtableCatalogPath,
    string ObjectStreamBootstrapPath);

public sealed record Tf2Ps3SourceBootstrapControlSummary(
    int MessageCount,
    int VtableMatchedCount,
    int FieldCount,
    int BootstrapCallsiteCount,
    int SignonStateCallsiteCount,
    int SetViewCallsiteCount);

public sealed record Tf2Ps3SourceBootstrapControlMessage(
    string ClassName,
    string NetworkName,
    int MessageId,
    string Role,
    string Meaning,
    string WriteFunction,
    bool VtableMatched,
    string Vptr,
    string GetTypeFunction,
    Tf2Ps3SourceBootstrapControlField[] Fields,
    string[] WriteEvidence);

public sealed record Tf2Ps3SourceBootstrapControlField(
    string Name,
    string Offset,
    string Encoding,
    string EvidenceToken,
    bool PresentInWriteFunction);

public sealed record Tf2Ps3SourceBootstrapControlCallsite(
    string ContainingFunction,
    string WriterFunction,
    string Role,
    int Line,
    string Statement,
    string Preview);

internal sealed record BootstrapControlSpec(
    string ClassName,
    string WriteFunction,
    string Role,
    string Meaning,
    BootstrapControlFieldSpec[] Fields);

internal sealed record BootstrapControlFieldSpec(
    string Name,
    string Offset,
    string Encoding,
    string EvidenceToken);
