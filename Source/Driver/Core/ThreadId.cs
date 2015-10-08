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
    private Implementation Initializer;

    internal readonly Constant Id;
    internal readonly string Name;

    internal ThreadId(Constant id)
    {
      this.Id = id;
      this.Name = id.Name;
    }

    internal ThreadId(Constant id, Expr tidExpr, Implementation initializer)
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

      this.Initializer = initializer;
    }

    internal bool IsEqual(IdentifierExpr tid)
    {
      if (tid.Name.Equals(this.Id.Name))
        return true;
      return false;
    }

    internal bool IsEqual(AnalysisContext ac, Implementation impl, Expr tid)
    {
      if (this.Ptr == null)
        return false;
      if (tid == null)
        return false;

      if (impl.Equals(this.Initializer) &&
        tid.ToString() == this.Ptr.ToString() &&
        tid.Line == this.Ptr.Line &&
        tid.Col == this.Ptr.Col)
        return true;

      IdentifierExpr ptr = null;
      if (tid is NAryExpr)
      {
        ptr = (tid as NAryExpr).Args[0] as IdentifierExpr;
        int ixs = ((tid as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
        if (this.Ixs != ixs)
          return false;
      }
      else
      {
        ptr = tid as IdentifierExpr;
        if (tid is IdentifierExpr &&
          ac.GetConstant((tid as IdentifierExpr).Name) != null &&
          this.Ptr.Name.Equals((tid as IdentifierExpr).Name))
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

    public override string ToString()
    {
      return this.Name;
    }
  }
}
