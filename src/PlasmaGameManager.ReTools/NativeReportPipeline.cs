using System.Diagnostics;
using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class NativeReportPipeline
{
    private static readonly string[] ReducerScripts =
    [
        "reduce-bfbc2-evidence.sh",
        "export-bfbc2-log-evidence.sh",
        "reduce-bfbc2-decompiles.sh",
        "reduce-bfbc2-pointer-evidence.sh",
        "reduce-bfbc2-recovered-callsites.sh",
        "reduce-bfbc2-recovered-pointers.sh",
        "reduce-bfbc2-server-send.sh",
        "reduce-bfbc2-transport-map.sh",
        "reduce-bfbc2-gamemanager-phases.sh",
        "reduce-bfbc2-server-gamemanager-listener.sh",
        "reduce-bfbc2-server-gamemanager-listener-complete.sh",
        "reduce-bfbc2-handle-message.sh",
        "reduce-bfbc2-handle-message-callees.sh",
        "reduce-bfbc2-switch-squad-mutation.sh",
        "reduce-bfbc2-join-chain.sh",
        "reduce-bfbc2-dispatcher-table.sh",
        "reduce-tf2ps3-gamemanager.sh",
        "reduce-tf2ps3-data-neighborhood.sh",
        "reduce-tf2ps3-dispatcher-map.sh",
        "reduce-tf2ps3-anchor-table.sh",
        "reduce-tf2ps3-anchor-context.sh",
        "reduce-tf2ps3-reader-functions.sh",
        "reduce-tf2ps3-reader-helpers.sh",
        "reduce-tf2ps3-second-level-helpers.sh",
        "reduce-tf2ps3-helper-callers.sh",
        "reduce-tf2ps3-dispatcher-map.sh",
        "reduce-tf2ps3-unresolved-targets.sh",
        "reduce-tf2ps3-unresolved-function-context.sh",
        "reduce-tf2ps3-source-network-anchors.sh",
        "reduce-tf2ps3-source-payload-builders.sh",
        "reduce-tf2ps3-source-send-callsite-map.sh",
        "reduce-tf2ps3-source-snapshot-path.sh",
        "reduce-tf2ps3-source-peer-channel.sh",
        "reduce-tf2ps3-source-snapshot-delta.sh",
        "reduce-tf2ps3-source-receive-path.sh",
        "reduce-tf2ps3-source-reliable-peer-attach.sh",
        "reduce-tf2ps3-source-pre-payload-receive.sh",
        "reduce-tf2ps3-source-payload-object-dispatch.sh",
        "reduce-tf2ps3-source-associated-object-token-contract.sh",
        "reduce-tf2ps3-source-associated-object-slot90.sh",
        "reduce-tf2ps3-source-associated-slotac-provenance.sh",
        "reduce-tf2ps3-source-associated-lane-role.sh",
        "reduce-tf2ps3-source-owner-control-subobject.sh",
        "reduce-tf2ps3-source-owner-forward-target.sh",
        "reduce-tf2ps3-source-owner-forward-context.sh",
        "reduce-tf2ps3-source-owner-forwarder-bitstream-coverage.sh",
        "reduce-tf2ps3-source-owner-forward-wrapper-variants.sh",
        "reduce-tf2ps3-source-category5-usercmd-handler.sh",
        "reduce-tf2ps3-source-usercmd-queue-record.sh",
        "export-tf2ps3-clc-move-context.sh",
        "reduce-tf2ps3-source-clc-move-contract.sh",
        "reduce-tf2ps3-source-handler-registrations.sh",
        "reduce-tf2ps3-source-player-vtable.sh",
        "reduce-tf2ps3-source-handler-vtable-candidates.sh",
        "export-tf2ps3-source-registration-binary-refs.sh",
        "reduce-tf2ps3-source-registration-binary-refs.sh",
        "export-tf2ps3-source-handler-vtable-candidate-refs.sh",
        "reduce-tf2ps3-source-handler-registration-proof.sh",
        "reduce-tf2ps3-source-handler-candidate-toc-access.sh",
        "reduce-tf2ps3-source-registration-callsites.sh",
        "export-tf2ps3-source-object-lifecycle-refs.sh",
        "reduce-tf2ps3-source-object-lifecycle.sh",
        "reduce-tf2ps3-source-installed-object-vtable.sh",
        "reduce-tf2ps3-source-object-vtable-lifecycle.sh",
        "reduce-tf2ps3-source-owner-callback.sh",
        "reduce-tf2ps3-source-owner-vtables.sh",
        "reduce-tf2ps3-source-helper-slice-contract.sh",
        "reduce-tf2ps3-source-netchan-static-anchor.sh",
        "reduce-tf2ps3-source-netchan-source-crossmap.sh",
        "reduce-tf2ps3-source-message-string-catalog.sh",
        "reduce-tf2ps3-source-message-vtable-catalog.sh",
        "analyze-client-command-worklist.sh",
        "analyze-source-client-input-coverage.sh",
        "analyze-source-client-boundary-probes.sh",
        "analyze-source-usercmd-record-candidates.sh",
        "analyze-source-usercmd-queue-delta-tail.sh",
        "analyze-source-payload-object-first-word.sh",
        "analyze-source-associated-object-tokens.sh",
        "analyze-associated-object-token-transform-probes.sh",
        "analyze-source-native-association-descriptors.sh",
        "analyze-embedded-clc-move-candidates.sh",
        "analyze-opaque-markerless-command-wrapper.sh",
        "reduce-tf2ps3-source-critical-message-io-contract.sh",
        "reduce-tf2ps3-source-critical-bootstrap-route.sh",
        "reduce-tf2ps3-source-object-stream-bootstrap.sh",
        "reduce-tf2ps3-source-bootstrap-control-messages.sh",
        "reduce-tf2ps3-source-packet-entities-placement.sh",
        "reduce-tf2ps3-source-native-message-contract.sh",
        "reduce-tf2ps3-source-netchan-registration-setup.sh",
        "reduce-tf2ps3-source-required-client-read-contract.sh",
        "reduce-tf2ps3-source-required-handler-constructor-probe.sh",
        "export-tf2ps3-source-required-handler-vptr-table-refs.sh",
        "reduce-tf2ps3-source-required-handler-table-neighborhood.sh",
        "reduce-tf2ps3-source-required-handler-table-toc-access.sh",
        "reduce-tf2ps3-source-required-handler-table-toc-functions.sh",
        "export-tf2ps3-source-helper-slice-refs.sh",
        "reduce-tf2ps3-source-virtual-slot44-scan.sh",
        "reduce-tf2ps3-source-receive-dispatch-slots.sh",
        "reduce-tf2ps3-source-slot70-callsite-census.sh",
        "reduce-tf2ps3-source-udp-ingress-correction.sh",
        "analyze-pcaps.sh",
        "analyze-pcap-corpus.sh",
        "analyze-ea-text-pcaps.sh",
        "analyze-handoff-topology.sh",
        "analyze-gamemanager-handoff-boundary.sh",
        "analyze-gamemanager-hello.sh",
        "analyze-source-streams.sh",
        "analyze-source-gameplay-phases.sh",
        "analyze-source-turn-contract.sh",
        "analyze-source-packet-shapes.sh",
        "analyze-source-queued-peer-opaque.sh",
        "analyze-source-embedded-objects.sh",
        "analyze-source-replay-corpus.sh",
        "analyze-source-native-builder-correlation.sh",
        "analyze-source-transport.sh",
        "analyze-source-transport-fields.sh",
        "analyze-source-bridge-contract.sh",
        "analyze-client-visible-source-endpoints.sh",
        "analyze-source-backend-boundary.sh",
        "analyze-source-translation-readiness.sh",
        "reduce-tf2ps3-source-field-contract.sh",
        "reduce-eatf2-serverdll-contract.sh",
        "reduce-eatf2-serverdll-simulation.sh",
        "reduce-eatf2-serverdll-native-obligations.sh",
        "export-eatf2-serverdll-target-functions.sh",
        "reduce-eatf2-serverdll-target-functions.sh",
        "reduce-eatf2-serverdll-runtime-contract.sh",
        "reduce-eatf2-serverdll-tunnel-map.sh",
        "export-eatf2-serverdll-tunnel-evidence.sh",
        "reduce-eatf2-serverdll-tunnel-ghidra.sh",
        "reduce-tf2ps3-source-queued-peer-targets.sh",
        "reduce-tf2ps3-source-native-template-debt.sh",
        "reduce-tf2ps3-source-template-patch-layout.sh",
        "reduce-tf2ps3-source-state-link-grammar.sh",
        "reduce-tf2ps3-source-embedded-object-grammar.sh",
        "reduce-tf2ps3-source-queued-prefix-contract.sh",
        "reduce-tf2ps3-source-generated-prefix-retail-crossmap.sh",
        "reduce-tf2ps3-source-generated-prefix-field-probe.sh",
        "reduce-tf2ps3-source-native-debt-priority.sh",
        "reduce-tf2ps3-source-loading-frame-debt.sh",
        "reduce-tf2ps3-source-loading-replacement-plan.sh",
        "reduce-tf2ps3-native-source-lifecycle.sh",
        "reduce-tf2ps3-source-server-replacement-contract.sh",
        "reduce-eatf2-serverdll-usercmd-layout.sh",
        "reduce-eatf2-serverdll-usercmd-decoder.sh",
        "analyze-markerless-boundary-hypotheses.sh",
        "reduce-tf2ps3-source-connected-wrapper-boundary.sh",
        "reduce-tf2ps3-source-payload-dispatch-boundary.sh",
        "reduce-tf2ps3-source-slot70-param2-builder.sh",
        "reduce-tf2ps3-source-slot70-param2-field-contract.sh",
        "reduce-tf2ps3-source-bitstream-helper-contract.sh",
        "reduce-tf2ps3-source-markerless-dataflow-targets.sh",
        "reduce-tf2ps3-source-owner-callback-dispatch.sh",
        "reduce-tf2ps3-source-helper-slice-receive-siblings.sh",
        "reduce-tf2ps3-source-recv-bitreader-census.sh",
        "reduce-tf2ps3-source-raw-udp-control-probe.sh",
        "reduce-tf2ps3-source-embedded-clc-move-proof.sh",
        "reduce-tf2ps3-source-markerless-param2-builder.sh",
        "reduce-eatf2-serverdll-usercmd-physics-audit.sh",
        "validate-tf2ps3-source-content.sh",
        "reduce-acceptance-gates.sh"
    ];

    public static async Task RunAsync(string repoRoot, string outputPath, bool continueOnFailure)
    {
        var steps = new List<NativeReportPipelineStep>();
        foreach (var script in ReducerScripts)
        {
            var path = Path.Combine(repoRoot, "scripts", script);
            if (!File.Exists(path))
            {
                steps.Add(new NativeReportPipelineStep(script, "missing", 0, "script does not exist"));
                if (!continueOnFailure)
                {
                    break;
                }

                continue;
            }

            var result = await RunScriptAsync(repoRoot, path);
            steps.Add(new NativeReportPipelineStep(script, result.ExitCode == 0 ? "passed" : "failed", result.ExitCode, result.Output));
            if (result.ExitCode != 0 && !continueOnFailure)
            {
                break;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
        {
            Status = "native-report-pipeline",
            ContinueOnFailure = continueOnFailure,
            StartedAt = DateTimeOffset.UtcNow,
            StepCount = steps.Count,
            Passed = steps.Count(static step => step.Status == "passed"),
            Failed = steps.Count(static step => step.Status == "failed"),
            Missing = steps.Count(static step => step.Status == "missing"),
            Steps = steps
        }, new JsonSerializerOptions { WriteIndented = true }));

        if (steps.Any(static step => step.Status is "failed" or "missing") && !continueOnFailure)
        {
            throw new InvalidOperationException($"Native report pipeline stopped at {steps.Last().Script}: {steps.Last().Status}");
        }
    }

    private static async Task<(int ExitCode, string Output)> RunScriptAsync(string repoRoot, string scriptPath)
    {
        var start = new ProcessStartInfo
        {
            FileName = "sh",
            WorkingDirectory = repoRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(scriptPath);

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Failed to start {scriptPath}");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = ((await stdout) + (await stderr)).Trim();
        if (output.Length > 8000)
        {
            output = output[..8000] + "\n... truncated ...";
        }

        return (process.ExitCode, output);
    }
}

public sealed record NativeReportPipelineStep(
    string Script,
    string Status,
    int ExitCode,
    string Output);
