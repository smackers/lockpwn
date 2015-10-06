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
using System.Diagnostics.Contracts;

using Microsoft.Boogie;

namespace Lockpwn
{
  internal class ThreadId
  {
    private IdentifierExpr Ptr;
    private int Ixs;

    internal readonly Constant Id;
    internal readonly string Name;

    internal ThreadId(Constant id)
    {
      this.Id = id;
      this.Name = id.Name;
    }

    internal ThreadId(Constant id, Expr tidExpr)
    {
      this.Id = id;
      this.Name = id.Name;

      if (tidExpr is NAryExpr)
      {
        this.Ptr = (tidExpr as NAryExpr).Args[0] as IdentifierExpr;
        this.Ixs = ((tidExpr as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
      }
      else if (tidExpr is IdentifierExpr)
      {
        this.Ptr = tidExpr as IdentifierExpr;
      }
    }

    internal bool IsEqual(AnalysisContext ac, Implementation impl, Expr tidExpr)
    {
      if (this.Ptr == null)
        return false;
      if (tidExpr == null)
        return false;

      if (tidExpr.ToString() == this.Ptr.ToString() &&
        tidExpr.Line == this.Ptr.Line &&
        tidExpr.Col == this.Ptr.Col)
        return true;

      IdentifierExpr ptr = null;
      if (tidExpr is NAryExpr)
      {
        ptr = (tidExpr as NAryExpr).Args[0] as IdentifierExpr;
        int ixs = ((tidExpr as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
        if (this.Ixs != ixs)
          return false;
      }
      else
      {
        ptr = tidExpr as IdentifierExpr;
        if (tidExpr is IdentifierExpr &&
          ac.GetConstant((tidExpr as IdentifierExpr).Name) != null &&
          this.Ptr.Name.Equals((tidExpr as IdentifierExpr).Name))
        {
          return true;
        }
      }

      if (ptr == null)
        return false;

      int index = -1;
      for (int i = 0; i < impl.InParams.Count; i++)
      {
        if (impl.InParams[i].Name.Equals(ptr.Name))
          index = i;
      }

      if (index == -1)
        return false;

      foreach (var b in ac.EntryPoint.Blocks)
      {
        foreach (var c in b.Cmds)
        {
          if (!(c is CallCmd))
            continue;
          if (!(c as CallCmd).callee.Equals(impl.Name))
            continue;

          IdentifierExpr id = (c as CallCmd).Ins[index] as IdentifierExpr;
          if (id.Name.Equals(this.Ptr.Name))
            return true;
        }
      }

      return false;
    }
  }
}
