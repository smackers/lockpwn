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
        this.AC.ThreadTemplates.Count == 0)
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
        Output.PrintLine("..... '{0}' is the main thread", thread.Name);
    }

    /// <summary>
    /// Performs an analysis to identify thread usage.
    /// </summary>
    /// <param name="parent">Thread</param>
    private void IdentifyThreadUsageInThread(Thread parent)
    {
      this.IdentifyThreadUsageInImplementation(parent, parent.Function);
    }

    /// <summary>
    /// Performs an analysis to identify thread usage.
    /// </summary>
    /// <param name="parent">Thread</param>
    /// <param name="impl">Implementation</param>
    /// <param name="ins">Optional list of expressions</param>
    private void IdentifyThreadUsageInImplementation(Thread parent, Implementation impl, List<Expr> inPtrs = null)
    {
      if (this.AlreadyAnalyzedImplementations.Contains(impl))
        return;
      this.AlreadyAnalyzedImplementations.Add(impl);

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          if (cmd is CallCmd)
          {
            CallCmd call = cmd as CallCmd;

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

              bool matched = false;
              foreach (var tid in this.AC.ThreadIds)
              {
                if (tid.IsEqual(this.AC, impl, tidExpr))
                {
                  call.Ins[0] = new IdentifierExpr(tid.Id.tok, tid.Id);
                  matched = true;
                  break;
                }
              }

              if (!matched)
              {
                this.AbstractSpawnedThread(parent, tidExpr, call);
              }
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
                    Output.PrintLine("..... '{0}' blocks thread with id '{1}'", parent.Name, tid.Id);
                  }
                  
                  matched = true;
                  break;
                }
              }

              if (!matched)
              {
                this.AbstractBlockedThread(parent, tidExpr, call);
              }
            }

            if (!Utilities.ShouldSkipFromAnalysis(call.callee) ||
              call.callee.Equals("pthread_create"))
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

              this.IdentifyThreadUsageInCall(parent, call, computedRootPointers);
            }
          }
        }
      }
    }

    /// <summary>
    /// Performs an analysis to identify thread usage in the callee.
    /// </summary>
    /// <param name="parent">Thread</param>
    /// <param name="cmd">CallCmd</param>
    /// <param name="ins">List of expressions</param>
    private void IdentifyThreadUsageInCall(Thread parent, CallCmd cmd, List<Expr> inPtrs)
    {
      var impl = this.AC.GetImplementation(cmd.callee);
      if (impl == null || !Utilities.ShouldAccessFunction(impl.Name))
        return;

      this.IdentifyThreadUsageInImplementation(parent, impl, inPtrs);
    }

    /// <summary>
    /// Abstracts the spawned thread.
    /// </summary>
    /// <param name="parent">Parent</param>
    /// <param name="tidExpr">Expr</param>
    /// <param name="spawner">CallCmd</param>
    private void AbstractSpawnedThread(Thread parent, Expr tidExpr, CallCmd spawner)
    {
      ThreadId tid = this.GetAbstractThreadId(tidExpr, spawner);
      var threadName = (spawner.Ins[2] as IdentifierExpr).Name;
      var thread = Thread.Create(this.AC, tid, threadName, spawner.Ins[3], parent);

      parent.AddChild(thread);

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        Output.PrintLine("..... '{0}' spawns thread '{1}' with id '{2}'",
          parent.Name, thread.Name, tid.Id);
      }
    }

    /// <summary>
    /// Abstracts the blocked thread.
    /// </summary>
    /// <param name="parent">Parent</param>
    /// <param name="tidExpr">Expr</param>
    /// <param name="spawner">CallCmd</param>
    private void AbstractBlockedThread(Thread parent, Expr tidExpr, CallCmd spawner)
    {
      ThreadId tid = this.GetAbstractThreadId(tidExpr, spawner);

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        Output.PrintLine("..... '{0}' blocks unidentified thread with id '{1}'",
          parent.Name, tid.Id);
      }
    }

    /// <summary>
    /// Returns an abstract thread id.
    /// </summary>
    /// <param name="tidExpr">Expr</param>
    /// <param name="spawner">CallCmd</param>
    /// <returns>ThreadId</returns>
    private ThreadId GetAbstractThreadId(Expr tidExpr, CallCmd spawner)
    {
      ThreadId tid = new ThreadId(new Constant(Token.NoToken,
        new TypedIdent(Token.NoToken, "tid$" + this.AC.ThreadIds.Count,
          Microsoft.Boogie.Type.Int), true), tidExpr);
      spawner.Ins[0] = new IdentifierExpr(tid.Id.tok, tid.Id);

      tid.Id.AddAttribute("tid", new object[] { });
      this.AC.TopLevelDeclarations.Add(tid.Id);
      this.AC.ThreadIds.Add(tid);

      return tid;
    }
  }
}
