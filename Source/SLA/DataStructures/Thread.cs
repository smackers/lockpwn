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
using System.Diagnostics.Contracts;

using Microsoft.Boogie;

namespace Lockpwn
{
  internal class Thread
  {
    internal readonly IdentifierExpr Id;
    internal readonly string Name;

    internal readonly Implementation Function;
    internal readonly Expr Arg;

    internal readonly Implementation Creator;

    internal readonly bool IsMain;
    internal readonly bool CreatedAtRoot;

    internal Thread(AnalysisContext ac)
    {
      this.Name = ac.EntryPoint.Name;
      this.Function = ac.EntryPoint;

      this.IsMain = true;
      this.CreatedAtRoot = true;
    }

    internal Thread(AnalysisContext ac, Expr id, Expr func, Expr arg, Implementation creator)
    {
      this.Id = id as IdentifierExpr;
      this.Name = (func as IdentifierExpr).Name;
      this.Function = ac.GetImplementation(this.Name);
      this.Arg = arg;
      this.Creator = creator;

      this.IsMain = false;
      if (ac.EntryPoint.Equals(creator))
        this.CreatedAtRoot = true;
      else
        this.CreatedAtRoot = false;
    }
  }
}
