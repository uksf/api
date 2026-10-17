using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaMissions.Services;

public interface IHeadlessClientPatcher
{
    void Patch(MissionPatchContext context);
}

public class HeadlessClientPatcher(IVariablesService variablesService) : IHeadlessClientPatcher
{
    private const string HeadlessClientType = "HeadlessClient_F";

    public void Patch(MissionPatchContext context)
    {
        if (context.SqmPath is null || !File.Exists(context.SqmPath))
        {
            return;
        }

        var variable = variablesService.GetVariable("SERVER_HEADLESS_NAMES");
        if (variable is null)
        {
            return;
        }

        var serverNames = variable.AsArray().Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
        if (serverNames.Length == 0)
        {
            return;
        }

        var text = SqmTextEditor.ReadFile(context.SqmPath);
        var root = SqmParser.Parse(text);
        var hcSlots = new List<HcSlot>();
        var missionEntities = SqmReader.MissionEntities(root);
        if (missionEntities is not null)
        {
            CollectHcSlots(missionEntities, hcSlots);
        }

        if (hcSlots.Count == 0)
        {
            return;
        }

        var slotsToKeep = hcSlots.Take(serverNames.Length).ToList();
        var slotsToDrop = hcSlots.Skip(serverNames.Length).ToList();

        if (slotsToDrop.Count > 0)
        {
            SqmTextEditor.WriteFile(context.SqmPath, DropSlots(text, slotsToDrop));

            context.Reports.Add(
                new ValidationReport(
                    "Excess headless client slots dropped",
                    $"Mission has {hcSlots.Count} headless client slots but the server only configures {serverNames.Length}.\n" +
                    $"Dropped slots in mission order: {string.Join(", ", slotsToDrop.Select(s => s.OriginalName))}\n" +
                    "Reduce the number of HeadlessClient_F slots in the mission to remove this warning."
                )
            );
        }

        HeadlessClientReferenceRenamer.Rename(context, slotsToKeep.Select(s => s.OriginalName).ToList(), serverNames);
    }

    private static void CollectHcSlots(SqmClassNode scope, List<HcSlot> output)
    {
        foreach (var item in scope.Items)
        {
            var name = item.OwnOrAttributeValue("name");
            if (item.Value("type") == HeadlessClientType && !string.IsNullOrEmpty(name))
            {
                output.Add(
                    new HcSlot
                    {
                        OriginalName = name,
                        Item = item,
                        Scope = scope
                    }
                );
            }
            else if (item.Value("dataType") == "Group" && item.Class("Entities") is { } groupEntities)
            {
                CollectHcSlots(groupEntities, output);
            }
        }
    }

    private static string DropSlots(string text, List<HcSlot> slotsToDrop)
    {
        var editor = new SqmTextEditor(text);
        var dropped = slotsToDrop.Select(slot => slot.Item).ToHashSet();
        foreach (var slot in slotsToDrop)
        {
            editor.DeleteLines(slot.Item.Start, slot.Item.End);
        }

        foreach (var scope in slotsToDrop.Select(slot => slot.Scope).Distinct())
        {
            var remaining = scope.Items.Where(item => !dropped.Contains(item)).ToList();
            for (var i = 0; i < remaining.Count; i++)
            {
                editor.Rename(remaining[i], $"Item{i}");
            }

            if (scope.Property("items") is { } items)
            {
                editor.SetValue(items, remaining.Count.ToString());
            }
        }

        return editor.Apply();
    }

    private sealed class HcSlot
    {
        public string OriginalName { get; init; }
        public SqmClassNode Item { get; init; }
        public SqmClassNode Scope { get; init; }
    }
}
