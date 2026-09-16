using System;
using System.Collections.Generic;
using System.Linq;
using HotChocolate.Language;

namespace GraphRoots.GraphApiTests;

static class SchemaContract
{
    public static string Canonical(string sdl)
    {
        var document = Utf8GraphQLParser.Parse(sdl);
        var lines = new List<string>();
        foreach (var definition in document.Definitions
            .Where(d => d is not SchemaDefinitionNode and not SchemaExtensionNode and not DirectiveDefinitionNode)
            .OrderBy(NameOf, StringComparer.Ordinal))
        {
            switch (definition)
            {
                case ObjectTypeDefinitionNode obj:
                    lines.Add($"type {obj.Name.Value}{Implements(obj.Interfaces)}{Desc(obj.Description)}");
                    foreach (var field in obj.Fields.OrderBy(f => f.Name.Value, StringComparer.Ordinal))
                        lines.Add("  " + Field(field));
                    break;
                case InterfaceTypeDefinitionNode iface:
                    lines.Add($"interface {iface.Name.Value}{Desc(iface.Description)}");
                    foreach (var field in iface.Fields.OrderBy(f => f.Name.Value, StringComparer.Ordinal))
                        lines.Add("  " + Field(field));
                    break;
                case InputObjectTypeDefinitionNode input:
                    lines.Add($"input {input.Name.Value}{Desc(input.Description)}");
                    foreach (var field in input.Fields.OrderBy(f => f.Name.Value, StringComparer.Ordinal))
                        lines.Add("  " + InputField(field));
                    break;
                case EnumTypeDefinitionNode enm:
                    lines.Add($"enum {enm.Name.Value}{Desc(enm.Description)}");
                    foreach (var value in enm.Values.OrderBy(v => v.Name.Value, StringComparer.Ordinal))
                        lines.Add($"  {value.Name.Value}{Desc(value.Description)}");
                    break;
                case ScalarTypeDefinitionNode scalar:
                    lines.Add($"scalar {scalar.Name.Value}{Desc(scalar.Description)}");
                    break;
                case UnionTypeDefinitionNode union:
                    lines.Add($"union {union.Name.Value} = {string.Join(" | ", union.Types.Select(t => t.Name.Value).OrderBy(n => n, StringComparer.Ordinal))}");
                    break;
            }
        }

        return string.Join("\n", lines);
    }

    static string NameOf(IDefinitionNode node) => node switch
    {
        ObjectTypeDefinitionNode n => n.Name.Value,
        InterfaceTypeDefinitionNode n => n.Name.Value,
        InputObjectTypeDefinitionNode n => n.Name.Value,
        EnumTypeDefinitionNode n => n.Name.Value,
        ScalarTypeDefinitionNode n => n.Name.Value,
        UnionTypeDefinitionNode n => n.Name.Value,
        _ => node.Kind.ToString(),
    };

    static string Implements(IReadOnlyList<NamedTypeNode> interfaces) =>
        interfaces.Count == 0
            ? ""
            : " implements " + string.Join(" & ", interfaces.Select(i => i.Name.Value).OrderBy(n => n, StringComparer.Ordinal));

    static string Field(FieldDefinitionNode field)
    {
        var args = field.Arguments.Count == 0
            ? ""
            : "(" + string.Join(", ", field.Arguments.OrderBy(a => a.Name.Value, StringComparer.Ordinal).Select(Arg)) + ")";
        return $"{field.Name.Value}{args}: {field.Type}{Desc(field.Description)}";
    }

    static string InputField(InputValueDefinitionNode field) =>
        $"{field.Name.Value}: {field.Type}{Default(field.DefaultValue)}{Desc(field.Description)}";

    static string Arg(InputValueDefinitionNode arg) =>
        $"{arg.Name.Value}: {arg.Type}{Default(arg.DefaultValue)}";

    static string Default(IValueNode? value) => value == null || value.Kind == SyntaxKind.NullValue ? "" : $" = {value}";

    static string Desc(StringValueNode? description)
    {
        if (description == null || string.IsNullOrWhiteSpace(description.Value))
            return "";
        return " | " + description.Value.Replace("\r", "", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
    }
}
