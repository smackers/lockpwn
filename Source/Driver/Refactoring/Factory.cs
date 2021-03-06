﻿//===-----------------------------------------------------------------------==//
//
// Lockpwn - blazing fast symbolic analysis for concurrent Boogie programs
//
// Copyright (c) 2015 Pantazis Deligiannis (pdeligia@me.com)
//
// This file is distributed under the MIT License. See LICENSE.TXT for details.
//
//===----------------------------------------------------------------------===//

using System;
using Microsoft.Boogie;

namespace Lockpwn.Refactoring
{
  internal static class Factory
  {
    internal static IPass CreateProgramSimplifier(AnalysisContext ac)
    {
      return new ProgramSimplifier(ac);
    }

    internal static IPass CreateThreadRefactoring(AnalysisContext ac)
    {
      return new ThreadRefactoring(ac);
    }
  }
}
