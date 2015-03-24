﻿//===-----------------------------------------------------------------------==//
//
//                Lockpwn - a Static Lockset Analyser for Boogie
//
// Copyright (c) 2015 Pantazis Deligiannis (pdeligia@me.com)
//
// This file is distributed under the MIT License. See LICENSE.TXT for details.
//
//===----------------------------------------------------------------------===//

using System;

namespace Lockpwn.Instrumentation
{
  internal static class Factory
  {
    internal static IPass CreateGlobalRaceCheckingInstrumentation(AnalysisContext ac)
    {
      return new GlobalRaceCheckingInstrumentation(ac);
    }

    public static IPass CreateLocksetInstrumentation(AnalysisContext ac)
    {
      return new LocksetInstrumentation(ac);
    }

    public static IPass CreateRaceCheckingInstrumentation(AnalysisContext ac)
    {
      return new RaceCheckingInstrumentation(ac);
    }

    public static IPass CreateErrorReportingInstrumentation(AnalysisContext ac)
    {
      return new ErrorReportingInstrumentation(ac);
    }

    public static IPass CreateAccessCheckingInstrumentation(AnalysisContext ac)
    {
      return new AccessCheckingInstrumentation(ac);
    }

    public static IPass CreateLoopSummaryInstrumentation(AnalysisContext ac)
    {
      return new LoopSummaryInstrumentation(ac);
    }

//    public static IPass CreateYieldInstrumentation(AnalysisContext ac, ErrorReporter errorReporter)
//    {
//      return new YieldInstrumentation(ac, errorReporter);
//    }
  }
}
