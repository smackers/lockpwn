//===-----------------------------------------------------------------------==//
//
//                Lockpwn - a Static Lockset Analyser for Boogie
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
    internal static IPass CreateLockRefactoring(AnalysisContext ac)
    {
      return new LockRefactoring(ac);
    }

    internal static IPass CreateThreadRefactoring(AnalysisContext ac)
    {
      return new ThreadRefactoring(ac);
    }
  }
}
