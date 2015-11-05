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
using Microsoft.Basetypes;

using Lockpwn.IO;

namespace Lockpwn.Analysis
{
  internal class ThreadUsageAnalysis : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    private HashSet<Implementation> AlreadyAnalyzedImplementations;

    internal ThreadUsageAnalysis(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;

      this.AlreadyAnalyzedImplementations = new HashSet<Implementation>();
    }

    /// <summary>
    /// Runs a thread usage analysis pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... ThreadUsageAnalysis");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.CreateMainThread();

      this.IdentifyThreadUsageInThread(this.AC.MainThread);

      if (ToolCommandLineOptions.Get().SuperVerboseMode &&
        this.AC.Threads.Count == 0)
      {
        Output.PrintLine("..... No child threads detected");
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    /// <summary>
    /// Creates main thread.
    /// </summary>
    private void CreateMainThread()
    {
      var thread = Thread.CreateMain(this.AC);

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
        Output.PrintLine("..... {0} is the main thread", thread);
    }

    /// <summary>
    /// Performs an analysis to identify thread usage.
    /// </summary>
    /// <param name="parent">Thread</param>
    private void IdentifyThreadUsageInThread(Thread parent)
    {
      this.IdentifyThreadUsageInImplementation(parent, null, parent.Function);
    }

    /// <summary>
    /// Performs an analysis to identify thread usage.
    /// </summary>
    /// <param name="parent">Thread</param>
    /// <param name="parent">child</param>
    /// <param name="impl">Implementation</param>
    /// <param name="ins">Optional list of expressions</param>
    private void IdentifyThreadUsageInImplementation(Thread parent, Thread child,
      Implementation impl, List<Expr> inPtrs = null)
    {
      if (this.AlreadyAnalyzedImplementations.Contains(impl))
        return;
      this.AlreadyAnalyzedImplementations.Add(impl);

      if (child != null && impl.Name.Equals(child.Name))
      {
        parent = child;
      }

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          if (cmd is CallCmd)
          {
            CallCmd call = cmd as CallCmd;
            Thread spawned = null;

            if (call.callee.Equals("pthread_create"))
            {
              var exprs = new HashSet<Expr>();
              var result = new PointerAnalysis(this.AC, impl).GetPointerOrigins(call.Ins[0], out exprs);
              var tidExpr = exprs.FirstOrDefault();

              if (result != PointerAnalysis.ResultType.Allocated &&
                result != PointerAnalysis.ResultType.Shared &&
                inPtrs != null)
              {
                tidExpr = new PointerAnalysis(this.AC, impl).RecomputeExprFromInParams(tidExpr, inPtrs);
              }

              spawned = this.GetAbstractSpawnedThread(parent, impl, tidExpr, call);
            }
            else if (call.callee.Equals("pthread_join"))
            {
              var exprs = new HashSet<Expr>();
              var result = new PointerAnalysis(this.AC, impl).GetPointerOrigins(call.Ins[0], out exprs);
              var tidExpr = exprs.FirstOrDefault();

              if (result != PointerAnalysis.ResultType.Allocated &&
                result != PointerAnalysis.ResultType.Shared &&
                inPtrs != null)
              {
                tidExpr = new PointerAnalysis(this.AC, impl).RecomputeExprFromInParams(tidExpr, inPtrs);
              }

              bool matched = false;
              foreach (var tid in this.AC.ThreadIds)
              {
                if (tid.IsEqual(this.AC, impl, tidExpr))
                {
                  var thread = parent.Children.FirstOrDefault(val => val.Id.Equals(tid));
                  if (thread == null)
                    continue;

                  call.Ins[0] = new IdentifierExpr(tid.Id.tok, tid.Id);
                  thread.Joiner = new Tuple<Implementation, Block, CallCmd>(impl, block, call);

                  if (ToolCommandLineOptions.Get().SuperVerboseMode)
                  {
                    Output.PrintLine("..... {0} blocks {1}", parent, thread);
                  }
                  
                  matched = true;
                  break;
                }
              }

              if (!matched)
              {
                this.AbstractBlockedThread(parent, impl, tidExpr, call);
              }
            }

            if (!Utilities.ShouldSkipFromAnalysis(call.callee) ||
              call.callee.Equals("pthread_create") ||
              call.callee.Equals("__call_wrapper"))
            {
              List<Expr> computedRootPointers = new List<Expr>();
              foreach (var inParam in call.Ins)
              {
                if (inParam is NAryExpr)
                {
                  computedRootPointers.Add(inParam);
                }
                else
                {
                  var exprs = new HashSet<Expr>();
                  new PointerAnalysis(this.AC, impl).GetPointerOrigins(inParam, out exprs);
                  var ptrExpr = exprs.FirstOrDefault();

                  computedRootPointers.Add(ptrExpr);
                }
              }

              this.IdentifyThreadUsageInCall(parent, spawned, call, computedRootPointers);
            }
          }
        }
      }
    }

    /// <summary>
    /// Performs an analysis to identify thread usage in the callee.
    /// </summary>
    /// <param name="parent">Thread</param>
    /// <param name="child">Thread</param>
    /// <param name="cmd">CallCmd</param>
    /// <param name="ins">List of expressions</param>
    private void IdentifyThreadUsageInCall(Thread parent, Thread child, CallCmd cmd, List<Expr> inPtrs)
    {
      var impl = this.AC.GetImplementation(cmd.callee);
      if (impl == null || !Utilities.ShouldAccessFunction(impl.Name))
        return;

      this.IdentifyThreadUsageInImplementation(parent, child, impl, inPtrs);
    }

    /// <summary>
    /// Abstracts and returns the spawned thread.
    /// </summary>
    /// <param name="parent">Parent</param>
    /// <param name="impl">Implementation</param>
    /// <param name="tidExpr">Expr</param>
    /// <param name="spawner">CallCmd</param>
    private Thread GetAbstractSpawnedThread(Thread parent, Implementation impl, Expr tidExpr, CallCmd spawner)
    {
      ThreadId tid = this.GetAbstractThreadId(tidExpr, impl, spawner);

      string threadName = "";
      if (spawner.Ins[2] is IdentifierExpr)
      {
        threadName = (spawner.Ins[2] as IdentifierExpr).Name;
      }
      else if (spawner.Ins[2] is NAryExpr)
      {
        var threadExpr = spawner.Ins[2] as NAryExpr;
        if (threadExpr.Fun.FunctionName.StartsWith("$bitcast."))
        {
          threadName = (threadExpr.Args[0] as IdentifierExpr).Name;
        }
      }

      var thread = Thread.Create(this.AC, tid, threadName, spawner.Ins[3], parent);

      parent.AddChild(thread);

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        Output.PrintLine("..... {0} spawns {1}",
          parent, thread);
      }

      return thread;
    }

    /// <summary>
    /// Abstracts the blocked thread.
    /// </summary>
    /// <param name="parent">Parent</param>
    /// <param name="impl">Implementation</param>
    /// <param name="tidExpr">Expr</param>
    /// <param name="spawner">CallCmd</param>
    private void AbstractBlockedThread(Thread parent, Implementation impl, Expr tidExpr, CallCmd spawner)
    {
      ThreadId tid = this.GetAbstractThreadId(tidExpr, impl, spawner);

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        Output.PrintLine("..... {0} blocks unidentified thread with id '{1}'",
          parent, tid.Id);
      }
    }

    /// <summary>
    /// Returns an abstract thread id.
    /// </summary>
    /// <param name="tidExpr">Expr</param>
    /// <param name="impl">Implementation</param>
    /// <param name="spawner">CallCmd</param>
    /// <returns>ThreadId</returns>
    private ThreadId GetAbstractThreadId(Expr tidExpr, Implementation impl, CallCmd spawner)
    {
      ThreadId tid = new ThreadId(new Constant(Token.NoToken,
        new TypedIdent(Token.NoToken, "tid$" + this.AC.ThreadIds.Count,
          Microsoft.Boogie.Type.Int), true), tidExpr, impl);
      spawner.Ins[0] = new IdentifierExpr(tid.Id.tok, tid.Id);

      tid.Id.AddAttribute("tid", new object[] { });
      this.AC.TopLevelDeclarations.Add(tid.Id);
      this.AC.ThreadIds.Add(tid);

      return tid;
    }
  }
}
