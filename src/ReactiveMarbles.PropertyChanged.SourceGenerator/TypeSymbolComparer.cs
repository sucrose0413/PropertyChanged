﻿// Copyright (c) 2019-2021 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis;

namespace ReactiveMarbles.PropertyChanged.SourceGenerator
{
    internal class TypeSymbolComparer : IComparer<ITypeSymbol>
    {
        public static TypeSymbolComparer Default { get; } = new TypeSymbolComparer();

        public int Compare(ITypeSymbol x, ITypeSymbol y)
        {
            if (ReferenceEquals(x, null) && ReferenceEquals(y, null))
            {
                return 0;
            }

            if (ReferenceEquals(x, null))
            {
                return 1;
            }

            if (ReferenceEquals(y, null))
            {
                return -1;
            }

            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            var xNamed = x as INamedTypeSymbol;
            var yNamed = y as INamedTypeSymbol;

            if (xNamed != null && yNamed != null)
            {
                return xNamed.ToDisplayString().CompareTo(yNamed.ToDisplayString());
            }

            return x.Name.CompareTo(y.Name);
        }
    }
}
