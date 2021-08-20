// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Analyzers.MinimalActions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisReleaseTracking", "RS2008:Enable analyzer release tracking")]
    internal static class DiagnosticDescriptors
    {
        internal static readonly DiagnosticDescriptor DoNotUseModelBindingAttributesOnMinimalActionParameters = new(
            "ASP0003",
            "Do not use model binding attributes with Map actions",
            "{0} should not be specified for a {1} delegate parameter",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: "https://aka.ms/aspnet/analyzers");

        internal static readonly DiagnosticDescriptor DoNotReturnActionResultsFromMapActions = new(
            "ASP0004",
            "Do not use action results with Map actions",
            "IActionResult instances should not be returned from a {0} delegate parameter. Consider returning an equivalent result from Microsoft.AspNetCore.Http.Results.",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: "https://aka.ms/aspnet/analyzers");
    }
}
