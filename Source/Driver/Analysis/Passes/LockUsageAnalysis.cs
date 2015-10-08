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
  internal class LockUsageAnalysis : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    private HashSet<Implementation> AlreadyAnalyzedImplementations;

    internal LockUsageAnalysis(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;

      this.AlreadyAnalyzedImplementations = new HashSet<Implementation>();
    }

    /// <summary>
    /// Runs a lock abstraction pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... LockUsageAnalysis");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.IdentifyLockCreationInThread(this.AC.MainThread);

      if (this.AC.Locks.Count > 0)
      {
        this.AlreadyAnalyzedImplementations.Clear();
        this.IdentifyLockUsageInThread(this.AC.MainThread);
      }

      if (ToolCommandLineOptions.Get().SuperVerboseMode &&
        this.AC.Locks.Count == 0)
      {
        Output.PrintLine("..... No locks detected");
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    /// <summary>
    /// Performs an analysis to identify lock creation.
    /// </summary>
    /// <param name="parent">Thread</param>
    private void IdentifyLockCreationInThread(Thread parent)
    {
      this.IdentifyLockCreationInImplementation(parent, parent.Function);
    }

    /// <summary>
    /// Performs an analysis to identify lock usage.
    /// </summary>
    /// <param name="parent">Thread</param>
    private void IdentifyLockUsageInThread(Thread parent)
    {
      this.IdentifyLockUsageInImplementation(parent, parent.Function);
    }

    /// <summary>
    /// Performs an analysis to identify lock creation.
    /// </summary>
    /// <param name="parent">Thread</param>
    /// <param name="child">Thread</param>
    /// <param name="impl">Implementation</param>
    /// <param name="ins">Optional list of expressions</param>
    private void IdentifyLockCreationInImplementation(Thread parent, Implementation impl, List<Expr> inPtrs = null)
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
            if (call.callee.Equals("pthread_mutex_init"))
            {
              var exprs = new HashSet<Expr>();
              var result = new PointerAnalysis(this.AC, impl).GetPointerOrigins(call.Ins[0], out exprs);
              var lockExpr = exprs.FirstOrDefault();

              if (result != PointerAnalysis.ResultType.Allocated &&
                result != PointerAnalysis.ResultType.Shared &&
                inPtrs != null)
              {
                lockExpr = new PointerAnalysis(this.AC, impl).RecomputeExprFromInParams(lockExpr, inPtrs);
              }

              this.AbstractInitializedLock(parent, lockExpr, call);
            }

            if (!Utilities.ShouldSkipFromAnalysis(call.callee) ||
              call.callee.StartsWith("pthread_create$"))
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

              this.IdentifyLockCreationInCall(parent, call, computedRootPointers);
            }
          }
        }
      }
    }

    /// <summary>
    /// Performs an analysis to identify lock usage.
    /// </summary>
    /// <param name="parent">Thread</param>
    /// <param name="impl">Implementation</param>
    /// <param name="ins">Optional list of expressions</param>
    private void IdentifyLockUsageInImplementation(Thread parent, Implementation impl, List<Expr> inPtrs = null)
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

            if (call.callee.Equals("pthread_mutex_lock") ||
              call.callee.Equals("pthread_mutex_unlock"))
            {
              var exprs = new HashSet<Expr>();
              var result = new PointerAnalysis(this.AC, impl).GetPointerOrigins(call.Ins[0], out exprs);
              var lockExpr = exprs.FirstOrDefault();

              if (result != PointerAnalysis.ResultType.Allocated &&
                result != PointerAnalysis.ResultType.Shared &&
                inPtrs != null)
              {
                lockExpr = new PointerAnalysis(this.AC, impl).RecomputeExprFromInParams(lockExpr, inPtrs);
              }

              bool matched = false;
              foreach (var l in this.AC.Locks)
              {
                if (l.IsEqual(this.AC, impl, lockExpr))
                {
                  if (ToolCommandLineOptions.Get().SuperVerboseMode)
                  {
                    Output.PrintLine("..... {0} uses lock '{1}'", parent, l.Name);
                  }

                  if (Output.Debugging)
                  {
                    Output.PrintLine("....... replacing lock '{0}' in call '{1}', line {2}",
                      call.Ins[0], call.callee, call.Line);
                  }

                  call.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
                  matched = true;

                  break;
                }
              }

              if (!matched && this.AC.Locks.Count == 1)
              {
                var l = this.AC.Locks[0];

                if (ToolCommandLineOptions.Get().SuperVerboseMode)
                {
                  Output.PrintLine("..... {0} uses lock '{1}'", parent, l.Name);
                }

                if (Output.Debugging)
                {
                  Output.PrintLine("....... replacing lock '{0}' in call '{1}', line {2}",
                    call.Ins[0], call.callee, call.Line);
                }

                call.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
              }
              else if (!matched)
              {
                this.AbstractUsedLock(parent, lockExpr, call);
              }
            }

            if (!Utilities.ShouldSkipFromAnalysis(call.callee) ||
              call.callee.StartsWith("pthread_create$") ||
              call.callee.StartsWith("__call_wrapper$"))
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

              Thread child = null;
              if (call.callee.StartsWith("pthread_create$"))
              {
                var tid = computedRootPointers[0] as IdentifierExpr;
                child = this.AC.Threads.First(val => !val.IsMain && val.Id.IsEqual(tid));
                parent = child;
              }

              this.IdentifyLockUsageInCall(parent, call, computedRootPointers);
            }
          }
        }
      }
    }

    /// <summary>
    /// Performs an analysis to identify lock creation in the callee.
    /// </summary>
    /// <param name="parent">Thread</param>
    /// <param name="cmd">CallCmd</param>
    /// <param name="ins">List of expressions</param>
    private void IdentifyLockCreationInCall(Thread parent, CallCmd cmd, List<Expr> inPtrs)
    {
      var impl = this.AC.GetImplementation(cmd.callee);
      if (impl == null || !Utilities.ShouldAccessFunction(impl.Name))
        return;

      this.IdentifyLockCreationInImplementation(parent, impl, inPtrs);
    }

    /// <summary>
    /// Performs an analysis to identify lock usage in the callee.
    /// </summary>
    /// <param name="parent">Thread</param>
    /// <param name="cmd">CallCmd</param>
    /// <param name="ins">List of expressions</param>
    private void IdentifyLockUsageInCall(Thread parent, CallCmd cmd, List<Expr> inPtrs)
    {
      var impl = this.AC.GetImplementation(cmd.callee);
      if (impl == null || !Utilities.ShouldAccessFunction(impl.Name))
        return;

      this.IdentifyLockUsageInImplementation(parent, impl, inPtrs);
    }

    /// <summary>
    /// Abstracts the initialized lock.
    /// </summary>
    /// <param name="parent">Parent</param>
    /// <param name="lockExpr">Expr</param>
    /// <param name="locker">CallCmd</param>
    private void AbstractInitializedLock(Thread parent, Expr lockExpr, CallCmd locker)
    {
      Lock l = this.GetAbstractLock(lockExpr);

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        Output.PrintLine("..... {0} initializes lock '{1}'", parent, l.Name);
      }

      if (Output.Debugging)
      {
        Output.PrintLine("....... replacing lock '{0}' in call '{1}', line {2}",
          locker.Ins[0], locker.callee, locker.Line);
      }

      locker.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
    }

    /// <summary>
    /// Abstracts the used lock.
    /// </summary>
    /// <param name="parent">Parent</param>
    /// <param name="lockExpr">Expr</param>
    /// <param name="locker">CallCmd</param>
    private void AbstractUsedLock(Thread parent, Expr lockExpr, CallCmd locker)
    {
      Lock l = this.GetAbstractLock(lockExpr);

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        Output.PrintLine("..... {0} uses unidentified lock '{1}'", parent, l.Name);
      }

      if (Output.Debugging)
      {
        Output.PrintLine("....... replacing lock '{0}' in call '{1}', line {2}",
          locker.Ins[0], locker.callee, locker.Line);
      }

      locker.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
    }

    /// <summary>
    /// Returns an abstract lock.
    /// </summary>
    /// <param name="lockExpr">Expr</param>
    /// <returns>ThreadId</returns>
    private Lock GetAbstractLock(Expr lockExpr)
    {
      Lock l = new Lock(new Constant(Token.NoToken,
        new TypedIdent(Token.NoToken, "lock$" + this.AC.Locks.Count,
          Microsoft.Boogie.Type.Int), true), lockExpr);
      
      l.Id.AddAttribute("lock", new object[] { });
      this.AC.TopLevelDeclarations.Add(l.Id);
      this.AC.Locks.Add(l);

      return l;
    }
  }
}
