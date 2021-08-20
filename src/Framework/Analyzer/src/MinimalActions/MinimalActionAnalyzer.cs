// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AspNetCore.Analyzers.MinimalActions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public partial class MinimalActionAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
    {
        DiagnosticDescriptors.DoNotUseModelBindingAttributesOnMinimalActionParameters,
        DiagnosticDescriptors.DoNotReturnActionResultsFromMapActions,
    });

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationStartAnalysisContext =>
        {
            var compilation = compilationStartAnalysisContext.Compilation;
            if (!WellKnownTypes.TryCreate(compilation, out var wellKnownTypes))
            {
                Debug.Fail("One or more types could not be found. This usually means you are bad at spelling C# type names.");
                return;
            }

            compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
            {
                var invocation = (IInvocationOperation)operationAnalysisContext.Operation;
                var targetMethod = invocation.TargetMethod;
                if (IsMapActionInvocation(wellKnownTypes, invocation, targetMethod))
                {
                    return;
                }

                var delegateCreation = invocation.Arguments[2].Descendants().OfType<IDelegateCreationOperation>().FirstOrDefault();
                if (delegateCreation is null)
                {
                    return;
                }

                if (delegateCreation.Target.Kind == OperationKind.AnonymousFunction)
                {
                    var lambda = ((IAnonymousFunctionOperation)delegateCreation.Target);
                    DisallowMvcBindArgumentsOnParameters(in operationAnalysisContext, wellKnownTypes, invocation, lambda.Symbol);
                    DisallowReturningActionResultFromMapMethods(in operationAnalysisContext, wellKnownTypes, invocation, lambda);
                }
                else if (delegateCreation.Target.Kind == OperationKind.MethodReference)
                {
                    var methodReference = (IMethodReferenceOperation)delegateCreation.Target;
                    DisallowMvcBindArgumentsOnParameters(in operationAnalysisContext, wellKnownTypes, invocation, methodReference.Method);

                    var foundMethodReferenceBody = false;
                    if (!methodReference.Method.DeclaringSyntaxReferences.IsEmpty)
                    {
                        var syntaxReference = methodReference.Method.DeclaringSyntaxReferences[0];
                        var methodOperation = invocation.SemanticModel.GetOperation(syntaxReference.GetSyntax(operationAnalysisContext.CancellationToken));
                        if (methodOperation is ILocalFunctionOperation { Body: not null } localFunction)
                        {
                            foundMethodReferenceBody = true;
                            DisallowReturningActionResultFromMapMethods(
                                in operationAnalysisContext,
                                wellKnownTypes,
                                invocation,
                                methodReference.Method,
                                localFunction.Body);
                        }
                        else if (methodOperation is IMethodBodyOperation methodBody)
                        {
                            foundMethodReferenceBody = true;
                            DisallowReturningActionResultFromMapMethods(
                                in operationAnalysisContext,
                                wellKnownTypes,
                                invocation,
                                methodReference.Method,
                                methodBody.BlockBody ?? methodBody.ExpressionBody);
                        }
                    }

                    if (!foundMethodReferenceBody)
                    {
                        // it's possible we couldn't find the operation for the method reference. In this case,
                        // try and provide less detailed diagnostics to the extent we can
                        DisallowReturningActionResultFromMapMethods(
                                in operationAnalysisContext,
                                wellKnownTypes,
                                invocation,
                                methodReference.Method,
                                methodBody: null);

                    }

                }
            }, OperationKind.Invocation);
        });
    }

    private static bool IsMapActionInvocation(
        WellKnownTypes wellKnownTypes,
        IInvocationOperation invocation,
        IMethodSymbol targetMethod)
    {
        return !targetMethod.Name.StartsWith("Map", StringComparison.Ordinal) ||
            !SymbolEqualityComparer.Default.Equals(wellKnownTypes.MinimalActionEndpointRouteBuilderExtensions, targetMethod.ContainingType) ||
            invocation.Arguments.Length != 3;
    }
}
