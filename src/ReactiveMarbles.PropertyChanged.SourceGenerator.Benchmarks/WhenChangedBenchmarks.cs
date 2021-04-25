﻿// Copyright (c) 2019-2021 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

using Microsoft.CodeAnalysis;

using ReactiveMarbles.PropertyChanged.SourceGenerator.Builders;

namespace ReactiveMarbles.PropertyChanged.SourceGenerator.Benchmarks
{
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [MemoryDiagnoser]
    [MarkdownExporterAttribute.GitHub]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class WhenChangedBenchmarks
    {
	    public Compilation Compilation { get; set; }

        [ParamsAllValues]
        public InvocationKind InvocationKind {  get; set; }

        [ParamsAllValues]
        public ReceiverKind ReceiverKind { get; set; }

        [Params(Accessibility.Public, Accessibility.Private)]
        public Accessibility Accessibility { get; set; }


        [GlobalSetup(Targets = new[] { nameof(Depth1WhenChanged) })]
        public void Depth1WhenChangedSetup()
        {
            var hostPropertyTypeInfo = new EmptyClassBuilder()
                .WithClassAccess(Accessibility);
            string userSource = new WhenChangedHostBuilder()
                .WithClassAccess(Accessibility)
                .WithPropertyType(hostPropertyTypeInfo)
                .WithInvocation(InvocationKind, ReceiverKind, x => x.Value)
                .BuildSource();

            Compilation = CompilationUtil.CreateCompilation(userSource);
        }

        [Benchmark]
        [BenchmarkCategory("Change Depth 1")]
        public void Depth1WhenChanged()
        {
            var newCompilation = CompilationUtil.RunGenerators(Compilation, out _, new Generator());
        }
        [GlobalSetup(Targets = new[] { nameof(Depth2WhenChanged) })]
        public void Depth2WhenChangedSetup()
        {
            var hostPropertyTypeInfo = new EmptyClassBuilder()
                .WithClassAccess(Accessibility);
            string userSource = new WhenChangedHostBuilder()
                .WithClassAccess(Accessibility)
                .WithPropertyType(hostPropertyTypeInfo)
                .WithInvocation(InvocationKind, ReceiverKind, x => x.Child.Value)
                .BuildSource();

            Compilation = CompilationUtil.CreateCompilation(userSource);
        }

        [Benchmark]
        [BenchmarkCategory("Change Depth 2")]
        public void Depth2WhenChanged()
        {
            var newCompilation = CompilationUtil.RunGenerators(Compilation, out _, new Generator());
        }
        [GlobalSetup(Targets = new[] { nameof(Depth10WhenChanged) })]
        public void Depth10WhenChangedSetup()
        {
            var hostPropertyTypeInfo = new EmptyClassBuilder()
                .WithClassAccess(Accessibility);
            string userSource = new WhenChangedHostBuilder()
                .WithClassAccess(Accessibility)
                .WithPropertyType(hostPropertyTypeInfo)
                .WithInvocation(InvocationKind, ReceiverKind, x => x.Child.Child.Child.Child.Child.Child.Child.Child.Child.Value)
                .BuildSource();

            Compilation = CompilationUtil.CreateCompilation(userSource);
        }

        [Benchmark]
        [BenchmarkCategory("Change Depth 10")]
        public void Depth10WhenChanged()
        {
            var newCompilation = CompilationUtil.RunGenerators(Compilation, out _, new Generator());
        }
        [GlobalSetup(Targets = new[] { nameof(Depth20WhenChanged) })]
        public void Depth20WhenChangedSetup()
        {
            var hostPropertyTypeInfo = new EmptyClassBuilder()
                .WithClassAccess(Accessibility);
            string userSource = new WhenChangedHostBuilder()
                .WithClassAccess(Accessibility)
                .WithPropertyType(hostPropertyTypeInfo)
                .WithInvocation(InvocationKind, ReceiverKind, x => x.Child.Child.Child.Child.Child.Child.Child.Child.Child.Child.Child.Child.Child.Child.Child.Child.Child.Child.Child.Value)
                .BuildSource();

            Compilation = CompilationUtil.CreateCompilation(userSource);
        }

        [Benchmark]
        [BenchmarkCategory("Change Depth 20")]
        public void Depth20WhenChanged()
        {
            var newCompilation = CompilationUtil.RunGenerators(Compilation, out _, new Generator());
        }

    }
}
