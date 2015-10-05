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
using System.ComponentModel.Design.Serialization;

using Microsoft.Boogie;
using Microsoft.Basetypes;

using Lockpwn.Analysis;
using Lockpwn.IO;

namespace Lockpwn.Refactoring
{
  internal class LockRefactoring : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    private HashSet<Implementation> AlreadyRefactoredFunctions;

    internal LockRefactoring(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;

      this.AlreadyRefactoredFunctions = new HashSet<Implementation>();
    }

    /// <summary>
    /// Runs a lock refactoring pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... LockRefactoring");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.AnalyseAndInstrumentLocks(this.AC.MainThread.Function);
      foreach (var thread in this.AC.ThreadTemplates)
      {
        this.AnalyseAndInstrumentLocks(thread.Function);
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    /// <summary>
    /// Performs an analysis to identify and instrument functions with abstract locks.
    /// </summary>
    /// <param name="impl">Implementation</param>
    /// <param name="ins">Optional list of expressions</param>
    private void AnalyseAndInstrumentLocks(Implementation impl, List<Expr> inPtrs = null)
    {
      if (this.AlreadyRefactoredFunctions.Contains(impl))
        return;
      this.AlreadyRefactoredFunctions.Add(impl);

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          if (cmd is CallCmd)
          {
            CallCmd call = cmd as CallCmd;

            if (Utilities.ShouldSkipFromAnalysis(call.callee))
              continue;

            if (call.callee.Equals("pthread_mutex_lock") ||
              call.callee.Equals("pthread_mutex_unlock"))
            {
              var lockExpr = PointerArithmeticAnalyser.ComputeRootPointer(impl, block.Label, call.Ins[0]);
              if (inPtrs != null && (!(lockExpr is LiteralExpr) || (lockExpr is NAryExpr)))
              {
                if (lockExpr is IdentifierExpr)
                {
                  for (int i = 0; i < impl.InParams.Count; i++)
                  {
                    if (lockExpr.ToString().Equals(impl.InParams[i].ToString()))
                    {
                      lockExpr = inPtrs[i];
                    }
                  }
                }
                else if (lockExpr is NAryExpr)
                {
                  for (int i = 0; i < (lockExpr as NAryExpr).Args.Count; i++)
                  {
                    for (int j = 0; j < impl.InParams.Count; j++)
                    {
                      if ((lockExpr as NAryExpr).Args[i].ToString().Equals(impl.InParams[j].ToString()))
                      {
                        (lockExpr as NAryExpr).Args[i] = inPtrs[j];
                      }
                    }
                  }
                }

                lockExpr = PointerArithmeticAnalyser.ComputeLiteralsInExpr(lockExpr);
              }

              bool matched = false;
              foreach (var l in this.AC.Locks)
              {
                if (l.IsEqual(this.AC, impl, lockExpr))
                {
                  call.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
                  matched = true;

                  if (ToolCommandLineOptions.Get().SuperVerboseMode)
                    Output.PrintLine("..... '{0}' uses lock '{1}'", impl.Name, l.Name);

                  break;
                }
              }

              if (!matched && this.AC.Locks.FindAll(val => !val.IsKernelSpecific).Count == 1)
              {
                var l = this.AC.Locks.Find(val => !val.IsKernelSpecific);
                call.Ins[0] = new IdentifierExpr(l.Id.tok, l.Id);
              }
              else if (!matched)
              {
                call.Ins[0] = lockExpr;
              }
            }
            else if (!this.ShouldSkip(call.callee))
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
                  Expr ptrExpr = PointerArithmeticAnalyser.ComputeRootPointer(impl, block.Label, inParam);
                  computedRootPointers.Add(ptrExpr);
                }
              }

              this.AnalyseAndInstrumentLocksInCall(call, computedRootPointers);
            }
          }
          else if (cmd is AssignCmd)
          {
            this.AnalyseAndInstrumentLocksInAssign(cmd as AssignCmd);
          }
        }
      }
    }

    private void AnalyseAndInstrumentLocksInCall(CallCmd cmd, List<Expr> inPtrs)
    {
      var impl = this.AC.GetImplementation(cmd.callee);

      if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
      {
        this.AnalyseAndInstrumentLocks(impl, inPtrs);
      }

      foreach (var expr in cmd.Ins)
      {
        if (!(expr is IdentifierExpr)) continue;
        impl = this.AC.GetImplementation((expr as IdentifierExpr).Name);

        if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
        {
          this.AnalyseAndInstrumentLocks(impl);
        }
      }
    }

    private void AnalyseAndInstrumentLocksInAssign(AssignCmd cmd)
    {
      foreach (var rhs in cmd.Rhss)
      {
        if (!(rhs is IdentifierExpr)) continue;
        var impl = this.AC.GetImplementation((rhs as IdentifierExpr).Name);

        if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
        {
          this.AnalyseAndInstrumentLocks(impl);
        }
      }
    }

    private bool ShouldSkip(string callee)
    {
      if (callee.Contains("#"))
        return true;
      return false;
    }
  }
}
