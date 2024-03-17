﻿using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace L3.Computed.EFCore.Internal;

public class ComputedConventionSetPlugin(
        Func<IModel, IComputedExpressionAnalyzer> computedExpressionAnalyzerFactory)
    : IConventionSetPlugin
{
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        conventionSet.Add(new ComputedRuntimeConvention(computedExpressionAnalyzerFactory));

        return conventionSet;
    }
}
