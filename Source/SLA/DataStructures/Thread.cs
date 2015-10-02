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
  internal class Thread
  {
    #region fields

    internal readonly IdentifierExpr Id;
    internal readonly string Name;

    internal readonly Implementation Function;
    internal Implementation SpawnFunction;
    internal readonly Expr Arg;

    internal Thread Parent;
    internal Tuple<Implementation, Block, CallCmd> Joiner;

    internal readonly bool IsMain;
    internal readonly bool CreatedAtRoot;

    #endregion

    #region constructors

    private Thread(AnalysisContext ac)
    {
      this.Name = ac.EntryPoint.Name;
      this.Function = ac.EntryPoint;
      this.SpawnFunction = null;
      this.Parent = null;
      this.Joiner = null;

      this.IsMain = true;
      this.CreatedAtRoot = true;
    }

    private Thread(AnalysisContext ac, Expr id, Expr func, Expr arg, Implementation creator)
    {
      this.Id = id as IdentifierExpr;
      this.Name = (func as IdentifierExpr).Name;
      this.Function = ac.GetImplementation(this.Name);
      this.Arg = arg;
      this.SpawnFunction = creator;
      this.Joiner = null;

      this.IsMain = false;
      if (ac.EntryPoint.Equals(creator))
      {
        this.Parent = ac.MainThread;
        this.CreatedAtRoot = true;
      }
      else
      {
        this.Parent = null;
        this.CreatedAtRoot = false;
      }
    }

    #endregion

    #region factory methods

    internal static Thread CreateMain(AnalysisContext ac)
    {
      var thread = new Thread(ac);
      ac.MainThread = thread;
      ac.Threads.Add(thread);
      return thread;
    }

    internal static Thread Create(AnalysisContext ac, Expr id, Expr func, Expr arg, Implementation creator)
    {
      var thread = new Thread(ac, id, func, arg, creator);
      ac.Threads.Add(thread);
      return thread;
    }

    #endregion
  }
}
