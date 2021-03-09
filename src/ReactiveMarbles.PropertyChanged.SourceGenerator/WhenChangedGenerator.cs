﻿// Copyright (c) 2019-2021 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ReactiveMarbles.PropertyChanged.SourceGenerator
{
    internal sealed class WhenChangedGenerator
    {
        private const string ExtensionClassFullName = "NotifyPropertyChangedExtensions";

        public static void GenerateWhenChanged(GeneratorExecutionContext context, Compilation compilation, SyntaxReceiver syntaxReceiver)
        {
            WhenChangedInvocationInfo whenChangedInvocationInfo = ExtractWhenChangedInvocationInfo(context, compilation, syntaxReceiver);

            if (!whenChangedInvocationInfo.AllExpressionArgumentsAreValid)
            {
                return;
            }

            var partialClassData = whenChangedInvocationInfo.ExpressionArguments
                .Where(x => x.ContainsPrivateOrProtectedMember)
                .GroupBy(x => x.InputType)
                .Select(x => x
                    .GroupBy(y => y.OutputType)
                    .Select(y => y.ToOuputTypeGroup())
                    .ToInputTypeGroup(x.Key))
                .GroupJoin(
                    whenChangedInvocationInfo.MultiExpressionMethodData,
                    x => x.FullName,
                    x => x.InputTypeFullName,
                    (inputTypeGroup, multiExpressionMethodData) =>
                    {
                        IEnumerable<MethodDatum> allMethodData = inputTypeGroup
                            .OutputTypeGroups
                            .Select(CreateSingleExpressionMethodDatum)
                            .Concat(multiExpressionMethodData.Where(x => x.ContainsPrivateOrProtectedTypeArgument));

                        return new PartialClassDatum(inputTypeGroup.NamespaceName, inputTypeGroup.Name, inputTypeGroup.AccessModifier, inputTypeGroup.AncestorClasses, allMethodData);
                    })
                .ToList();

            var extensionClassData = whenChangedInvocationInfo.ExpressionArguments
                .Where(x => !x.ContainsPrivateOrProtectedMember)
                .GroupBy(x => x.InputType)
                .Select(x => x
                    .GroupBy(y => y.OutputType)
                    .Select(y => y.ToOuputTypeGroup())
                    .ToInputTypeGroup(x.Key))
                .GroupJoin(
                    whenChangedInvocationInfo.MultiExpressionMethodData,
                    x => x.FullName,
                    x => x.InputTypeFullName,
                    (inputTypeGroup, multiExpressionMethodData) =>
                    {
                        IEnumerable<MethodDatum> allMethodData = inputTypeGroup
                            .OutputTypeGroups
                            .Select(CreateSingleExpressionMethodDatum)
                            .Concat(multiExpressionMethodData.Where(x => !x.ContainsPrivateOrProtectedTypeArgument));

                        return new ExtensionClassDatum(inputTypeGroup.Name, allMethodData);
                    })
                .ToList();

            var extensionClassCreator = new StringBuilderExtensionClassCreator();
            for (int i = 0; i < extensionClassData.Count; i++)
            {
                string source = extensionClassCreator.Create(extensionClassData[i]);
                context.AddSource($"WhenChanged.{extensionClassData[i].Name}{i}.g.cs", SourceText.From(source, Encoding.UTF8));
            }

            var partialClassCreator = new StringBuilderPartialClassCreator();
            for (int i = 0; i < partialClassData.Count; i++)
            {
                string source = partialClassCreator.Create(partialClassData[i]);
                context.AddSource($"{partialClassData[i].Name}{i}.WhenChanged.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private static WhenChangedInvocationInfo ExtractWhenChangedInvocationInfo(GeneratorExecutionContext context, Compilation compilation, SyntaxReceiver syntaxReceiver)
        {
            bool allExpressionArgumentsAreValid = true;
            var expressionArguments = new HashSet<ExpressionArgument>();
            var multiExpressionMethodData = new HashSet<MultiExpressionMethodDatum>();

            foreach (InvocationExpressionSyntax invocationExpression in syntaxReceiver.WhenChangedMethods)
            {
                SemanticModel model = compilation.GetSemanticModel(invocationExpression.SyntaxTree);
                ISymbol symbol = model.GetSymbolInfo(invocationExpression).Symbol;

                if (symbol is IMethodSymbol methodSymbol)
                {
                    if (!methodSymbol.ContainingType.ToDisplayString().Equals(ExtensionClassFullName))
                    {
                        continue;
                    }

                    foreach (ArgumentSyntax argument in invocationExpression.ArgumentList.Arguments)
                    {
                        if (model.GetTypeInfo(argument.Expression).ConvertedType.Name.Equals("Expression"))
                        {
                            if (argument.Expression is LambdaExpressionSyntax lambdaExpression)
                            {
                                ITypeSymbol lambdaInputType = methodSymbol.TypeArguments[0];
                                ITypeSymbol lambdaOutputType = model.GetTypeInfo(lambdaExpression.Body).Type;
                                List<(string Name, string InputType, string OutputType)> expressionChain = GetExpressionChain(context, lambdaExpression, model);

                                bool containsPrivateOrProtectedMember =
                                    lambdaInputType.DeclaredAccessibility <= Accessibility.Protected ||
                                    ContainsPrivateOrProtectedMember(compilation, model, lambdaExpression);
                                expressionArguments.Add(new(lambdaExpression.Body.ToString(), expressionChain, lambdaInputType, lambdaOutputType, containsPrivateOrProtectedMember));
                                allExpressionArgumentsAreValid &= expressionChain != null;
                            }
                            else
                            {
                                // The argument is evaluates to an expression but it's not inline (could be a variable, method invocation, etc).
                                context.ReportDiagnostic(
                                    Diagnostic.Create(
                                        descriptor: DiagnosticWarnings.ExpressionMustBeInline,
                                        location: argument.GetLocation()));

                                allExpressionArgumentsAreValid = false;
                            }
                        }
                    }

                    if (methodSymbol.TypeArguments.Length > 2)
                    {
                        Accessibility minAccessibility = methodSymbol.TypeArguments.Min(x => x.DeclaredAccessibility);
                        Accessibility accessModifier = minAccessibility;
                        bool containsPrivateOrProtectedTypeArgument = minAccessibility <= Accessibility.Protected;
                        IEnumerable<string> typeNames = methodSymbol.TypeArguments.Select(x => x.ToDisplayString());
                        multiExpressionMethodData.Add(new(accessModifier, typeNames, containsPrivateOrProtectedTypeArgument));
                    }
                }
            }

            return new WhenChangedInvocationInfo(allExpressionArgumentsAreValid, expressionArguments, multiExpressionMethodData);
        }

        private static MethodDatum CreateSingleExpressionMethodDatum(OutputTypeGroup outputTypeGroup)
        {
            MethodDatum methodDatum = null;

            (string _, List<(string Name, string InputType, string outputType)> expressionChain, ITypeSymbol inputTypeSymbol, ITypeSymbol outputTypeSymbol, bool _) = outputTypeGroup.ExpressionArguments.First();
            (string inputTypeName, string outputTypeName) = (inputTypeSymbol.ToDisplayString(), outputTypeSymbol.ToDisplayString());

            Accessibility accessModifier = inputTypeSymbol.DeclaredAccessibility;
            if (outputTypeSymbol.DeclaredAccessibility < inputTypeSymbol.DeclaredAccessibility)
            {
                accessModifier = outputTypeSymbol.DeclaredAccessibility;
            }

            if (outputTypeGroup.ExpressionArguments.Count == 1)
            {
                methodDatum = new SingleExpressionOptimizedImplMethodDatum(inputTypeName, outputTypeName, accessModifier, expressionChain);
            }
            else if (outputTypeGroup.ExpressionArguments.Count > 1)
            {
                string mapName = $"__generated{inputTypeSymbol.GetVariableName()}{outputTypeSymbol.GetVariableName()}Map";

                var entries = new List<MapEntryDatum>(outputTypeGroup.ExpressionArguments.Count);
                foreach (ExpressionArgument argumentDatum in outputTypeGroup.ExpressionArguments)
                {
                    string mapKey = argumentDatum.LambdaBodyString;
                    var mapEntry = new MapEntryDatum(mapKey, argumentDatum.ExpressionChain);
                    entries.Add(mapEntry);
                }

                var map = new MapDatum(mapName, entries);
                methodDatum = new SingleExpressionDictionaryImplMethodDatum(inputTypeName, outputTypeName, accessModifier, map);
            }

            return methodDatum;
        }

        private static List<(string Name, string InputType, string OutputType)> GetExpressionChain(GeneratorExecutionContext context, LambdaExpressionSyntax lambdaExpression, SemanticModel model)
        {
            var members = new List<(string Name, string InputType, string OutputType)>();
            ExpressionSyntax expression = lambdaExpression.ExpressionBody;
            var expressionChain = expression as MemberAccessExpressionSyntax;

            while (expressionChain != null)
            {
                string name = expressionChain.Name.ToString();
                var inputType = model.GetTypeInfo(expressionChain.ChildNodes().ElementAt(0)).Type as INamedTypeSymbol;
                var outputType = model.GetTypeInfo(expressionChain.ChildNodes().ElementAt(1)).Type as INamedTypeSymbol;

                members.Add((name, inputType.ToDisplayString(), outputType.ToDisplayString()));

                expression = expressionChain.Expression;
                expressionChain = expression as MemberAccessExpressionSyntax;
            }

            if (expression is not IdentifierNameSyntax firstLinkInChain)
            {
                // It stopped before reaching the lambda parameter, so the expression is invalid.
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        descriptor: DiagnosticWarnings.OnlyPropertyAndFieldAccessAllowed,
                        location: expression.GetLocation()));

                return null;
            }

            string lambdaParameterName =
                (lambdaExpression as SimpleLambdaExpressionSyntax)?.Parameter.Identifier.ToString() ??
                (lambdaExpression as ParenthesizedLambdaExpressionSyntax)?.ParameterList.Parameters[0].Identifier.ToString();

            if (string.Equals(lambdaParameterName, firstLinkInChain.Identifier.ToString(), StringComparison.InvariantCulture))
            {
                members.Reverse();
                return members;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    descriptor: DiagnosticWarnings.LambdaParameterMustBeUsed,
                    location: lambdaExpression.Body.GetLocation()));

            return null;
        }

        private static bool ContainsPrivateOrProtectedMember(Compilation compilation, SemanticModel model, LambdaExpressionSyntax lambdaExpression)
        {
            var members = new List<ExpressionSyntax>();
            ExpressionSyntax expression = lambdaExpression.ExpressionBody;
            var expressionChain = expression as MemberAccessExpressionSyntax;

            while (expressionChain != null)
            {
                members.Add(expression);
                expression = expressionChain.Expression;
                expressionChain = expression as MemberAccessExpressionSyntax;
            }

            members.Add(expression);
            members.Reverse();
            ITypeSymbol inputTypeSymbol = model.GetTypeInfo(expression).ConvertedType;

            for (int i = members.Count - 1; i > 0; i--)
            {
                ExpressionSyntax parent = members[i - 1];
                ExpressionSyntax child = members[i];
                ITypeSymbol parentTypeSymbol = model.GetTypeInfo(parent).ConvertedType;

                if (!SymbolEqualityComparer.Default.Equals(parentTypeSymbol, inputTypeSymbol))
                {
                    continue;
                }

                ISymbol propertyInvocationSymbol = model.GetSymbolInfo(child).Symbol;
                SyntaxNode propertyDeclarationSyntax = propertyInvocationSymbol.DeclaringSyntaxReferences.First().GetSyntax();
                SemanticModel propertyDeclarationModel = compilation.GetSemanticModel(propertyDeclarationSyntax.SyntaxTree);
                ISymbol propertyDeclarationSymbol = propertyDeclarationModel.GetDeclaredSymbol(propertyDeclarationSyntax);
                Accessibility propertyAccessibility = propertyDeclarationSymbol.DeclaredAccessibility;

                if (propertyAccessibility <= Accessibility.Protected)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
