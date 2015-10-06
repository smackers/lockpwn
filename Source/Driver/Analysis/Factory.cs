//===-----------------------------------------------------------------------==//
//
// Lockpwn - blazing fast symbolic analysis for concurrent Boogie programs
//
// Copyright (c) 2015 Pantazis Deligiannis (pdeligia@me.com)
//
// This file is distributed under the MIT License. See LICENSE.TXT for details.
//
//===----------------------------------------------------------------------===//

using System;

namespace Lockpwn.Analysis
{
  internal static class Factory
  {
    internal static IPass CreateThreadUsageAnalysis(AnalysisContext ac)
    {
      return new ThreadUsageAnalysis(ac);
    }

    internal static IPass CreateLockUsageAnalysis(AnalysisContext ac)
    {
      return new LockUsageAnalysis(ac);
    }

    internal static IPass CreateSharedStateAnalysis(AnalysisContext ac)
    {
      return new SharedStateAnalysis(ac);
    }

    internal static IPass CreateSharedStateAbstraction(AnalysisContext ac)
    {
      return new SharedStateAbstraction(ac);
    }

    internal static IPass CreateRaceCheckAnalysis(AnalysisContext ac)
    {
      return new RaceCheckAnalysis(ac);
    }

    internal static IPass CreateHoudiniInvariantInference(AnalysisContext ac, AnalysisContext postAc)
    {
      return new HoudiniInvariantInference(ac, postAc);
    }
  }
}
