using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// Loads a GraphQL document the way the storefront bundles it: a <c>.graphql</c> file under
/// <c>client-app/core/api/graphql</c> with its <c>#import "…"</c> fragments inlined. A contract test can then send
/// the storefront's own operation text, so the backend is checked against what the client really asks for.
/// </summary>
internal static class StorefrontDocuments
{
    /// <param name="installedModules">
    /// What the storefront's apply-gates link does before sending: a field marked <c>@gate(module: "X")</c> is
    /// dropped when X is not installed (and so are fragments nothing references any more); otherwise only the
    /// directive goes.
    /// </param>
    public static string Load(string relativePath, IReadOnlyCollection<string> installedModules = null)
    {
        var root = Path.Combine(ViteDevServer.ResolveFrontendDirectory(), "client-app", "core", "api", "graphql");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = new StringBuilder();

        Inline(Path.GetFullPath(Path.Combine(root, relativePath)), seen, text);

        return ApplyGates(text.ToString(), installedModules ?? []);
    }

    private static string ApplyGates(string document, IReadOnlyCollection<string> installedModules)
    {
        var gate = new Regex("@gate\\(module:\\s*\"(?<module>[^\"]+)\"\\)");

        for (var match = gate.Match(document); match.Success; match = gate.Match(document))
        {
            if (installedModules.Contains(match.Groups["module"].Value, StringComparer.OrdinalIgnoreCase))
            {
                document = document.Remove(match.Index, match.Length);
                continue;
            }

            // The field starts after the previous line break; its selection set ends at the matching brace.
            var start = document.LastIndexOf('\n', match.Index) + 1;
            var end = match.Index + match.Length;
            var brace = document.IndexOf('{', end);
            if (brace >= 0 && document[end..brace].Trim().Length == 0)
            {
                end = SelectionSetEnd(document, brace);
            }

            document = document.Remove(start, end - start);
        }

        // Fragments only the dropped field used would now fail the "no unused fragments" rule.
        var fragments = new Regex("fragment\\s+(?<name>\\w+)\\s+on\\s+\\w+\\s*\\{");
        bool removed;
        do
        {
            removed = false;
            foreach (Match fragment in fragments.Matches(document))
            {
                if (Regex.IsMatch(document, "\\.\\.\\." + fragment.Groups["name"].Value + "\\b"))
                {
                    continue;
                }

                var end = SelectionSetEnd(document, fragment.Index + fragment.Length - 1);
                document = document.Remove(fragment.Index, end - fragment.Index);
                removed = true;
                break;
            }
        }
        while (removed);

        return document;
    }

    private static int SelectionSetEnd(string document, int openingBrace)
    {
        var depth = 0;
        for (var i = openingBrace; i < document.Length; i++)
        {
            if (document[i] == '{')
            {
                depth++;
            }
            else if (document[i] == '}' && --depth == 0)
            {
                return i + 1;
            }
        }

        return document.Length;
    }

    private static void Inline(string path, HashSet<string> seen, StringBuilder text)
    {
        if (!seen.Add(path))
        {
            return;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#import", StringComparison.Ordinal))
            {
                var imported = trimmed.Split('"')[1];
                Inline(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, imported)), seen, text);
            }
            else
            {
                text.AppendLine(line);
            }
        }
    }
}
