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
using System.Linq;

using Microsoft.Boogie;

namespace Lockpwn
{
  internal class Thread
  {
    #region fields

    internal IdentifierExpr Id;
    internal string Name;
    internal Expr Arg;

    internal Implementation Function;
    internal Implementation SpawnFunction;

    internal Tuple<Implementation, Block, CallCmd> Joiner;

    internal bool IsMain;
    internal bool CreatedAtRoot;

    #endregion

    #region constructors

    /// <summary>
    /// Constructor.
    /// </summary>
    private Thread() { }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="ac">AnalysisContext</param>
    private Thread(AnalysisContext ac)
    {
      this.Name = ac.EntryPoint.Name;
      this.Function = ac.EntryPoint;
      this.SpawnFunction = null;

      this.Joiner = null;

      this.IsMain = true;
      this.CreatedAtRoot = true;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="ac">AnalysisContext</param>
    /// <param name="id">Identifier</param>
    /// <param name="func">Func</param>
    /// <param name="arg">Argument</param>
    /// <param name="creator">Creator</param>
    private Thread(AnalysisContext ac, Expr id, Expr arg, Expr func, Implementation creator)
    {
      this.Id = id as IdentifierExpr;
      this.Name = (func as IdentifierExpr).Name;
      this.Arg = arg;

      this.Function = ac.GetImplementation(this.Name);
      this.SpawnFunction = creator;

      this.Joiner = null;

      this.IsMain = false;
      if (ac.EntryPoint.Equals(creator))
      {
        this.CreatedAtRoot = true;
      }
      else
      {
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

    internal static Thread Create(AnalysisContext ac, Expr id, Expr arg, Expr func, Implementation creator)
    {
      var thread = new Thread(ac, id, arg, func, creator);
      ac.Threads.Add(thread);
      return thread;
    }

    #endregion

    #region methods

    /// <summary>
    /// Clones the thread in the given analysis context.
    /// </summary>
    /// <param name="ac">AnalysisContext</param>
    /// <returns>Cloned thread</returns>
    internal Thread Clone(AnalysisContext ac)
    {
      var thread = new Thread();

      thread.Id = this.Id;
      thread.Name = this.Name;
      thread.Arg = this.Arg;

      thread.IsMain = this.IsMain;
      thread.CreatedAtRoot = this.CreatedAtRoot;

      thread.Function = ac.GetImplementation(this.Function.Name);

      if (this.SpawnFunction != null)
      {
        thread.SpawnFunction = ac.GetImplementation(this.SpawnFunction.Name);
      }

      if (this.Joiner != null)
      {
        var jImpl = ac.GetImplementation(this.Joiner.Item1.Name);
        var jBlock = jImpl.Blocks.FirstOrDefault(val => val.Label.Equals(this.Joiner.Item2.Label));
        var jCall = jBlock.Cmds.OfType<CallCmd>().FirstOrDefault(val =>
          val.ToString().Equals(this.Joiner.Item3.ToString()));

        thread.Joiner = Tuple.Create(jImpl, jBlock, jCall);
      }

      return thread;
    }

    #endregion
  }
}
