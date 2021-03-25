﻿// Copyright (c) 2019-2021 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ReactiveMarbles.PropertyChanged.SourceGenerator
{
    internal class RoslynWhenChangedPartialClassCreator : ISourceCreator
    {
        public string Create(IEnumerable<IDatum> sourceData)
        {
            var members = new List<MemberDeclarationSyntax>();

            foreach (var group in sourceData
                .Cast<PartialClassDatum>()
                .GroupBy(x => x.NamespaceName))
            {
                var classes = group.Select(x => Create(x));
                if (!string.IsNullOrWhiteSpace(group.Key))
                {
                    var groupNamespace = NamespaceDeclaration(IdentifierName(group.Key))
                        .WithMembers(List<MemberDeclarationSyntax>(classes));
                    members.Add(groupNamespace);
                }
                else
                {
                    members.AddRange(classes);
                }
            }

            if (members.Count > 0)
            {
                var compilation = CompilationUnit()
                    .WithStandardReactiveUsings()
                    .WithMembers(List(members));

                return compilation.NormalizeWhitespace().ToFullString();
            }

            return null;
        }

        private static ClassDeclarationSyntax Create(PartialClassDatum classDatum)
        {
            var visibility = classDatum.AccessModifier.GetAccessibilityTokens().Concat(new[] { Token(SyntaxKind.PartialKeyword) });

            var currentClass = ClassDeclaration(classDatum.Name)
                .WithMembers(List(classDatum.MethodData.SelectMany(x => Create(x))))
                .WithModifiers(TokenList(visibility));

            foreach (var ancestor in classDatum.AncestorClasses)
            {
                visibility = ancestor.AccessModifier.GetAccessibilityTokens().Concat(new[] { Token(SyntaxKind.PartialKeyword) });
                currentClass = ClassDeclaration(ancestor.Name)
                    .WithModifiers(TokenList(visibility))
                    .WithMembers(List<MemberDeclarationSyntax>(new[] { currentClass }));
            }

            return currentClass;
        }

        private static IEnumerable<MemberDeclarationSyntax> Create(MethodDatum method) =>
            method switch
            {
                SingleExpressionDictionaryImplMethodDatum methodDatum => Create(methodDatum),
                SingleExpressionOptimizedImplMethodDatum methodDatum => Create(methodDatum),
                MultiExpressionMethodDatum methodDatum => Create(methodDatum),
                _ => throw new InvalidOperationException("Unknown type of datum."),
            };

        private static IEnumerable<MemberDeclarationSyntax> Create(SingleExpressionDictionaryImplMethodDatum methodDatum)
        {
            var mapEntries = new List<AssignmentExpressionSyntax>();

            foreach (var entry in methodDatum.Map.Entries)
            {
                var observable = RoslynHelpers.GetObservableChain("this", entry.Members);
                mapEntries.Add(RoslynHelpers.MapEntry(entry.Key, observable));
            }

            yield return RoslynHelpers.MapDictionary(methodDatum.InputTypeName, methodDatum.OutputTypeName, methodDatum.Map.MapName, mapEntries);
            yield return RoslynHelpers.WhenChangedWithoutBody(methodDatum.InputTypeName, methodDatum.OutputTypeName, false, methodDatum.AccessModifier)
                .WithExpressionBody(ArrowExpressionClause(RoslynHelpers.MapInvokeExpression("this", methodDatum.Map.MapName, "propertyExpression")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        private static IEnumerable<MemberDeclarationSyntax> Create(SingleExpressionOptimizedImplMethodDatum methodDatum)
        {
            yield return RoslynHelpers.WhenChangedWithoutBody(methodDatum.InputTypeName, methodDatum.OutputTypeName, false, methodDatum.AccessModifier)
                .WithExpressionBody(ArrowExpressionClause(RoslynHelpers.GetObservableChain("this", methodDatum.Members)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        private static IEnumerable<MemberDeclarationSyntax> Create(MultiExpressionMethodDatum methodDatum)
        {
            var statements = new List<StatementSyntax>(methodDatum.TempReturnTypes.Count);
            var combineArguments = new List<ArgumentSyntax>(methodDatum.TempReturnTypes.Count);

            for (int i = 0; i < methodDatum.TempReturnTypes.Count; ++i)
            {
                var type = methodDatum.TempReturnTypes[i];
                var obsName = "obs" + (i + 1);
                var whenChangedVariable = LocalDeclarationStatement(VariableDeclaration(IdentifierName($"IObservable<{type}>"))
                    .WithVariables(
                        SingletonSeparatedList(
                            VariableDeclarator(
                                Identifier(obsName))
                            .WithInitializer(
                                EqualsValueClause(RoslynHelpers.InvokeWhenChanged("propertyExpression" + (i + 1), "this"))))));
                statements.Add(whenChangedVariable);
                combineArguments.Add(Argument(IdentifierName(obsName)));
            }

            combineArguments.Add(Argument(IdentifierName("conversionFunc")));

            statements.Add(ReturnStatement(
                InvocationExpression(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Observable"),
                    IdentifierName("CombineLatest")))
                        .WithArgumentList(
                            ArgumentList(SeparatedList(combineArguments)))));

            yield return RoslynHelpers.WhenChangedConversionWithoutBody(methodDatum.InputType.ToDisplayString(), methodDatum.OutputType.ToDisplayString(), methodDatum.TempReturnTypes, false, methodDatum.AccessModifier)
                .WithBody(Block(statements));
        }
    }
}
