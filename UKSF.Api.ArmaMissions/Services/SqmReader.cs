using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;

namespace UKSF.Api.ArmaMissions.Services;

public interface ISqmReader
{
    void Read(MissionPatchContext context);
}

public class SqmReader : ISqmReader
{
    public void Read(MissionPatchContext context)
    {
        var text = SqmTextEditor.ReadFile(context.SqmPath);
        var root = SqmParser.Parse(text);
        var entities = MissionEntities(root);

        context.NextEntityId = NextEntityId(root);
        context.Sqm = new SqmDocument
        {
            Text = text,
            Root = root,
            EntitiesNode = entities,
            Entities = entities?.Items.Select(node => ParseEntity(text, node)).ToList() ?? []
        };
    }

    public static SqmClassNode MissionEntities(SqmClassNode root) =>
        root.Class("Mission")?.Class("Entities") ?? root.Descendants.FirstOrDefault(node => node.Name.Equals("Entities", StringComparison.OrdinalIgnoreCase));

    public static SqmPropertyNode NextIdProperty(SqmClassNode root) =>
        (root.Class("EditorData")?.Class("ItemIDProvider") ?? root.Class("ItemIDProvider"))?.Property("nextID");

    private static int NextEntityId(SqmClassNode root)
    {
        var providerNext = int.TryParse(NextIdProperty(root)?.Text, out var next) ? next : 0;
        var highestId = root.Descendants.Select(node => int.TryParse(node.Value("id"), out var id) ? id : -1).DefaultIfEmpty(-1).Max();
        return Math.Max(providerNext, highestId + 1);
    }

    private static SqmEntity ParseEntity(string text, SqmClassNode node)
    {
        switch (node.Value("dataType"))
        {
            case "Group":
            {
                var children = node.Class("Entities")?.Items.Select(child => ParseEntity(text, child)).ToList() ?? [];
                return new SqmGroup
                {
                    Node = node,
                    Children = children,
                    AllChildrenPlayable = children.Count > 0 && children.All(c => c is SqmObject { IsPlayable: true }),
                    IsIgnored = children.Any(c => c is SqmObject && text[c.Node.Start..c.Node.End].Contains("@ignore", StringComparison.OrdinalIgnoreCase))
                };
            }
            case "Object":
            {
                return new SqmObject
                {
                    Node = node,
                    IsPlayable = node.OwnOrAttributeValue("isPlayable") == "1" || node.OwnOrAttributeValue("isPlayer") == "1",
                    Type = node.Value("type")
                };
            }
            case "Logic": return new SqmLogic { Node = node, Type = node.Value("type") };
            default:      return new SqmPassthrough { Node = node };
        }
    }
}
