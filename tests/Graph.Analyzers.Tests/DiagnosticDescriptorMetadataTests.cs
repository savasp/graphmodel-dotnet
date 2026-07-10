// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Xunit;


/// <summary>
/// Guards the descriptor metadata that the per-rule tests never look at. Those tests assert on
/// diagnostic IDs and spans, so a descriptor whose <see cref="DiagnosticDescriptor.Title"/> and
/// <see cref="DiagnosticDescriptor.MessageFormat"/> resolve to the empty string still passes every
/// one of them - while every rule surfaces blank in an IDE.
/// </summary>
/// <remarks>
/// <para>
/// Not hypothetical: renaming the rule prefix from <c>GM0xx</c> to <c>CG0xx</c> renamed the generated
/// <c>Resources</c> accessors but left the <c>.resx</c> data names on the old prefix, so every lookup
/// returned null and every title, message and description went empty. Nothing failed.
/// </para>
/// <para>
/// Like <c>AnalyzerHelperTypeClassificationTests</c>, this class avoids <c>Xunit.Assert</c>: the
/// analyzer-testing packages drag in xunit v2's assertion assembly alongside v3's, so <c>Assert</c>
/// is ambiguous here. It uses the same minimal-helper approach.
/// </para>
/// </remarks>
public class DiagnosticDescriptorMetadataTests
{
    private const int ExpectedRuleCount = 14;

    private static IReadOnlyList<(string FieldName, DiagnosticDescriptor Descriptor)> AllDescriptors() =>
    [
        .. typeof(DiagnosticDescriptors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => (f.Name, (DiagnosticDescriptor)f.GetValue(null)!))
    ];

    [Fact]
    public void EveryRuleIsDiscovered()
    {
        // Pins the reflection below: if DiagnosticDescriptors stops exposing public static fields,
        // the other tests here would silently inspect nothing.
        var count = AllDescriptors().Count;
        if (count != ExpectedRuleCount)
        {
            throw new InvalidOperationException(
                $"Expected {ExpectedRuleCount} diagnostic descriptors but discovered {count}. " +
                "Update ExpectedRuleCount if a rule was intentionally added or removed.");
        }
    }

    [Fact]
    public void EveryDescriptorHasNonEmptyLocalizedStrings()
    {
        var failures = new List<string>();

        foreach (var (fieldName, descriptor) in AllDescriptors())
        {
            Check(failures, fieldName, descriptor, "Title", descriptor.Title.ToString());
            Check(failures, fieldName, descriptor, "MessageFormat", descriptor.MessageFormat.ToString());
            Check(failures, fieldName, descriptor, "Description", descriptor.Description.ToString());
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Descriptors resolved to empty localized strings - a .resx data name most likely no " +
                "longer matches its generated Resources accessor:" +
                "\n" + string.Join("\n", failures));
        }

        static void Check(List<string> failures, string fieldName, DiagnosticDescriptor descriptor, string part, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                failures.Add($"  {fieldName} ({descriptor.Id}): {part} is empty");
            }
        }
    }

    [Fact]
    public void EveryDescriptorUsesCvoyaGraphCategoryAndCgRuleId()
    {
        var failures = new List<string>();

        foreach (var (fieldName, descriptor) in AllDescriptors())
        {
            if (descriptor.Category != "Cvoya.Graph")
            {
                failures.Add($"  {fieldName} ({descriptor.Id}): category is '{descriptor.Category}', expected 'Cvoya.Graph'");
            }

            if (!Regex.IsMatch(descriptor.Id, @"^CG\d{3}$", RegexOptions.None, TimeSpan.FromSeconds(1)))
            {
                failures.Add($"  {fieldName}: id '{descriptor.Id}' does not match CG###");
            }

            if (!descriptor.IsEnabledByDefault)
            {
                failures.Add($"  {fieldName} ({descriptor.Id}): not enabled by default");
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Descriptor identity drifted:" + "\n" + string.Join("\n", failures));
        }
    }

    [Fact]
    public void RuleIdsAreUnique()
    {
        var duplicates = AllDescriptors()
            .GroupBy(d => d.Descriptor.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"  {g.Key}: {string.Join(", ", g.Select(d => d.FieldName))}")
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                "Duplicate diagnostic IDs:" + "\n" + string.Join("\n", duplicates));
        }
    }
}
