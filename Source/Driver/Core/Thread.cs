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
using System.Collections.Generic;
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

    internal Thread Parent;
    internal HashSet<Thread> Children;

    internal Implementation Function;
    internal Tuple<Implementation, Block, CallCmd> Joiner;

    internal bool IsMain;
    internal bool CreatedAtRoot;

    #endregion

    #region constructors

    /// <summary>
    /// Constructor.
    /// </summary>
    private Thread()
    {
      this.Parent = null;
      this.Children = new HashSet<Thread>();

      this.Joiner = null;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="ac">AnalysisContext</param>
    private Thread(AnalysisContext ac)
    {
      this.Name = ac.EntryPoint.Name;
      this.Parent = null;
      this.Children = new HashSet<Thread>();

      this.Function = ac.EntryPoint;
      this.Joiner = null;

      this.IsMain = true;
      this.CreatedAtRoot = true;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="ac">AnalysisContext</param>
    /// <param name="name">String</param>
    /// <param name="id">Identifier</param>
    /// <param name="arg">Argument</param>
    /// <param name="parent">Parent</param>
    private Thread(AnalysisContext ac, string name, Expr id, Expr arg, Thread parent)
    {
      this.Id = id as IdentifierExpr;
      this.Name = name;
      this.Arg = arg;

      this.Parent = parent;
      this.Children = new HashSet<Thread>();

      this.Function = ac.GetImplementation(this.Name);
      this.Joiner = null;

      this.IsMain = false;
      if (ac.MainThread.Name.Equals(parent.Name))
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
      ac.RegisterThreadTemplate(thread);
      return thread;
    }

    internal static Thread Create(AnalysisContext ac, string name, Expr id, Expr arg, Thread parent)
    {
      var thread = new Thread(ac, name, id, arg, parent);
      ac.RegisterThreadTemplate(thread);
      return thread;
    }

    #endregion

    #region methods

    /// <summary>
    /// Adds a child thread.
    /// </summary>
    /// <param name="thread">Thread</param>
    internal void AddChild(Thread thread)
    {
      this.Children.Add(thread);
    }

    /// <summary>
    /// Clones the thread in the given analysis context.
    /// </summary>
    /// <param name="ac">AnalysisContext</param>
    /// <returns>Cloned thread</returns>
    internal Thread Clone(AnalysisContext ac)
    {
      var thread = new Thread(ac);

      thread.Id = this.Id;
      thread.Name = this.Name;
      thread.Arg = this.Arg;

      thread.IsMain = this.IsMain;
      thread.CreatedAtRoot = this.CreatedAtRoot;

      thread.Function = ac.GetImplementation(this.Function.Name);

      if (this.Joiner != null)
      {
        var jImpl = ac.GetImplementation(this.Joiner.Item1.Name);
        var jBlock = jImpl.Blocks.FirstOrDefault(val => val.Label.Equals(this.Joiner.Item2.Label));
        var jCall = jBlock.Cmds.OfType<CallCmd>().FirstOrDefault(val =>
          val.ToString().Equals(this.Joiner.Item3.ToString()));

        thread.Joiner = Tuple.Create(jImpl, jBlock, jCall);
      }

      foreach (var child in this.Children)
      {
        var clonedChild = child.Clone(ac);
        clonedChild.Parent = thread;
        thread.Children.Add(clonedChild);
      }

      ac.RegisterThreadTemplate(thread);

      return thread;
    }

    #endregion
  }
}
