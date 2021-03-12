﻿// Copyright (c) 2019-2021 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Microsoft.CodeAnalysis;

namespace ReactiveMarbles.PropertyChanged.SourceGenerator
{
    internal static class StringBuilderSourceCreatorHelper
    {
        private static readonly string AutoGeneratedString = @"// <auto-generated>
// This code is auto generated do not modify.
// <auto-generated />";

        public static string GetAutoGeneratedString() => AutoGeneratedString;

        public static string GetMultiExpressionMethodParameters(string inputType, string outputType, List<string> tempReturnTypes)
        {
            var sb = new StringBuilder();
            var counter = tempReturnTypes.Count;

            for (var i = 0; i < counter; i++)
            {
                sb.Append("        Expression<Func<").Append(inputType).Append(", ").Append(tempReturnTypes[i]).Append(">> propertyExpression").Append(i + 1).AppendLine(",");
            }

            sb.Append("        Func<");
            for (var i = 0; i < counter; i++)
            {
                sb.Append(tempReturnTypes[i]).Append(", ");
            }

            sb.Append(outputType).AppendLine("> conversionFunc,")
                .Append(@"        [CallerMemberName]string callerMemberName = null,
        [CallerFilePath]string callerFilePath = null,
        [CallerLineNumber]int callerLineNumber = 0)");

            return sb.ToString();
        }

        public static string GetMultiExpressionMethodBody(int counter)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < counter; i++)
            {
                sb.Append("        var obs").Append(i + 1).Append(" = objectToMonitor.WhenChanged(propertyExpression").Append(i + 1).AppendLine(", callerMemberName, callerFilePath, callerLineNumber);");
            }

            sb.Append("        return obs1.CombineLatest(");
            for (var i = 1; i < counter; i++)
            {
                sb.Append("obs").Append(i + 1).Append(", ");
            }

            sb.Append("conversionFunc);");

            return sb.ToString();
        }

        public static string GetMultiExpressionMethodBodyForPartialClass(int counter)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < counter; i++)
            {
                sb.Append("        var obs").Append(i + 1).Append(" = this.WhenChanged(propertyExpression").Append(i + 1).AppendLine(", callerMemberName, callerFilePath, callerLineNumber);");
            }

            sb.Append("        return obs1.CombineLatest(");
            for (var i = 1; i < counter; i++)
            {
                sb.Append("obs").Append(i + 1).Append(", ");
            }

            sb.Append("conversionFunc);");

            return sb.ToString();
        }

        public static string GetMultiExpressionMethod(string inputType, string outputType, Accessibility accessModifier, string expressionParameters, string body)
        {
            return $@"
    {accessModifier.ToFriendlyString()} static IObservable<{outputType}> WhenChanged(
        this {inputType} objectToMonitor,
{expressionParameters}
    {{
{body}
    }}
";
        }

        public static string GetMultiExpressionMethodForPartialClass(string inputType, string outputType, Accessibility accessModifier, string expressionParameters, string body)
        {
            return $@"
    {accessModifier.ToFriendlyString()} IObservable<{outputType}> WhenChanged(
{expressionParameters}
    {{
{body}
    }}
";
        }

        public static string GetWhenChangedMethodForMap(string inputType, string outputType, Accessibility accessModifier, string mapName)
        {
            return $@"
    {accessModifier.ToFriendlyString()} static IObservable<{outputType}> WhenChanged(
        this {inputType} source,
        Expression<Func<{inputType}, {outputType}>> propertyExpression,
        [CallerMemberName]string callerMemberName = null,
        [CallerFilePath]string callerFilePath = null,
        [CallerLineNumber]int callerLineNumber = 0)
    {{
        return {mapName}[propertyExpression.Body.ToString()].Invoke(source);
    }}
";
        }

        public static string GetObservableCreation(string inputType, string inputName, string outputType, string memberName)
        {
            return $@"Observable.Create<{outputType}>(
                observer =>
                {{
                    if ({inputName} == null)
                    {{
                        return Disposable.Empty;
                    }}

                    observer.OnNext({inputName}.{memberName});

                    PropertyChangedEventHandler handler = (object sender, PropertyChangedEventArgs e) =>
                    {{
                        var input = ({inputType}){inputName};
                        if (e.PropertyName == ""{memberName}"")
                        {{
                            observer.OnNext(input.{memberName});
                        }}
                    }};

                    {inputName}.PropertyChanged += handler;

                    return Disposable.Create((parent: {inputName}, handler), x => x.parent.PropertyChanged -= x.handler);
                }})";
        }

        public static string GetWhenChangedMethodForDirectReturn(string inputType, string outputType, Accessibility accessModifier, string valueChain)
        {
            return $@"
    /// <summary>
    /// Generates a IObservable which signals with updated property value changes.
    /// </summary>
    /// <param name=""source"">The source of the property changes.</param>
    /// <param name=""propertyExpression"">The property.</param>
    /// <returns>The observable which signals with updates.</returns>
    {accessModifier.ToFriendlyString()} static IObservable<{outputType}> WhenChanged(
        this {inputType} source,
        Expression<Func<{inputType}, {outputType}>> propertyExpression,
        [CallerMemberName]string callerMemberName = null,
        [CallerFilePath]string callerFilePath = null,
        [CallerLineNumber]int callerLineNumber = 0)
    {{
        return Observable.Return(source){valueChain};
    }}
";
        }

        public static string GetMapEntryChain(string inputType, string outputType, string memberName)
        {
            return $@"
                .Select(x => {GetObservableCreation(inputType, "x", outputType, memberName)})
                .Switch()";
        }

        public static string GetMapEntryObservableReturn(string key, string valueChain)
        {
            return GetMapEntry(key, $"Observable.Return(source){valueChain}");
        }

        public static string GetMapEntry(string key, string valueChain)
        {
            return $@"
        {{
            ""{key}"",
            source => {valueChain}
        }},";
        }

        public static string GetMap(string inputType, string outputType, string mapName, string entries)
        {
            return $@"
    private static readonly Dictionary<string, Func<{inputType}, IObservable<{outputType}>>> {mapName} = new Dictionary<string, Func<{inputType}, IObservable<{outputType}>>>()
    {{
{entries}
    }};
";
        }

        public static string GetClass(string body)
        {
            return $@"
{GetUsingStatements()}

public static partial class NotifyPropertyChangedExtensions
{{
    {body}
}}
";
        }

        public static string GetUsingStatements()
        {
            return @"
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
";
        }

        public static string GetPartialClass(string namespaceName, string className, Accessibility accessModifier, IEnumerable<AncestorClassInfo> ancestorClasses, string body)
        {
            var source = $@"
{accessModifier.ToFriendlyString()} partial class {className}
{{
    {body}
}}
";

            foreach (var ancestorClass in ancestorClasses)
            {
                source = $@"
{ancestorClass.AccessModifier.ToFriendlyString()} partial class {ancestorClass.Name}
{{
{source}
}}
";
            }

            if (!string.IsNullOrEmpty(namespaceName))
            {
                source = $@"
namespace {namespaceName}
{{
{source}
}}
";
            }

            return source.Insert(0, GetUsingStatements());
        }

        public static string GetWhenChangedMapMethod(string inputType, string outputType, bool isExtension, Accessibility accessModifier, string mapName)
        {
            var staticExpression = isExtension ? "static" : string.Empty;

            var sb = new StringBuilder($"   {accessModifier.ToFriendlyString()} {staticExpression} IObservable<{outputType}> WhenChanged(").AppendLine();

            string invokeName;
            if (isExtension)
            {
                invokeName = "source";
                sb.AppendLine($"        this {inputType} source,");
            }
            else
            {
                invokeName = "this";
            }

            sb.AppendLine($@"        Expression<Func<{inputType}, {outputType}>> propertyExpression, 
        [CallerMemberName]string callerMemberName = null,
        [CallerFilePath]string callerFilePath = null,
        [CallerLineNumber]int callerLineNumber = 0)
    {{
        return {mapName}[propertyExpression.Body.ToString()].Invoke({invokeName});
    }}");

            return sb.ToString();
        }

        public static string GetPartialClassWhenChangedMethodForDirectReturn(string inputType, string outputType, Accessibility accessModifier, List<(string Name, string InputType, string OutputType)> members)
        {
            var observableChainStringBuilder = new StringBuilder(GetObservableCreation(members[0].InputType, "this", members[0].OutputType, members[0].Name));

            foreach (var member in members.Skip(1))
            {
                observableChainStringBuilder.Append(GetMapEntryChain(member.InputType, member.OutputType, member.Name));
            }

            // Making the access modifier public so multi-expression extensions will able to access it, if needed.
            return $@"
    {accessModifier.ToFriendlyString()} IObservable<{outputType}> WhenChanged(
        Expression<Func<{inputType}, {outputType}>> propertyExpression,
        [CallerMemberName]string callerMemberName = null,
        [CallerFilePath]string callerFilePath = null,
        [CallerLineNumber]int callerLineNumber = 0)
    {{
        return {observableChainStringBuilder};
    }}
";
        }

        public static string GetWhenChangedStubClass()
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "ReactiveMarbles.PropertyChanged.SourceGenerator.NotifyPropertyChangedExtensions.cs";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static string GetBindingStubClass()
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "ReactiveMarbles.PropertyChanged.SourceGenerator.BindExtensions.cs";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
