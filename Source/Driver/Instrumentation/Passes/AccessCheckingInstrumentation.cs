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
using System.Runtime.InteropServices;

using Lockpwn.IO;

using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace Lockpwn.Instrumentation
{
  internal class AccessCheckingInstrumentation : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    private int CheckCounter;

    internal AccessCheckingInstrumentation(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.CheckCounter = 0;
    }

    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... AccessCheckingInstrumentation");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.InstrumentMain();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    #region access checking instrumentation

    private void InstrumentMain()
    {
      var currentThread = this.AC.MainThread;

      int enableCounter = 0;
      foreach (var mr in this.AC.SharedMemoryRegions)
      {
        var asserts = this.CreateRaceCheckingAssertions(currentThread, mr);
        if (asserts.Count == 0)
          continue;
        
        foreach (var assert in asserts)
        {
          var enabler = new GlobalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
            "assertion$enabler$" + enableCounter++, Microsoft.Boogie.Type.Bool));
          var enablerId = new IdentifierExpr(Token.NoToken, enabler);
          var enablerAssign = new AssignCmd(Token.NoToken, new List<AssignLhs> {
            new SimpleAssignLhs(Token.NoToken, enablerId)
          }, new List<Expr> { Expr.False });

          this.AC.TopLevelDeclarations.Add(enabler);
          this.AC.EntryPoint.Proc.Requires.Add(new Requires(false, enablerId));

          assert.Item3.Expr = Expr.Imp(enablerId, assert.Item3.Expr);

          var ip1 = this.GetInstrumentationPointForThread(assert.Item1);
          var ip2 = this.GetInstrumentationPointForThread(assert.Item2);

          if (ip1.Item2 >= 0)
          {
            ip1.Item1.Cmds.Insert(ip1.Item2, enablerAssign);
            ip1.Item1.Cmds.Insert(ip1.Item2, assert.Item3);
            ip1.Item1.Cmds.Insert(ip1.Item2, this.CreateCaptureStateAssume(mr));
          }
          else
          {
            ip1.Item1.Cmds.Add(this.CreateCaptureStateAssume(mr));
            ip1.Item1.Cmds.Add(assert.Item3);
            ip1.Item1.Cmds.Add(enablerAssign);
          }

          if (!(ip1.Item1.Equals(ip2.Item1) && ip1.Item2 == ip2.Item2) && ip2.Item2 >= 0)
          {
            ip2.Item1.Cmds.Insert(ip2.Item2, enablerAssign);
            ip2.Item1.Cmds.Insert(ip2.Item2, assert.Item3);
            ip2.Item1.Cmds.Insert(ip2.Item2, this.CreateCaptureStateAssume(mr));
          }
          else if (!(ip1.Item1.Equals(ip2.Item1) && ip1.Item2 == ip2.Item2))
          {
            ip2.Item1.Cmds.Add(this.CreateCaptureStateAssume(mr));
            ip2.Item1.Cmds.Add(assert.Item3);
            ip2.Item1.Cmds.Add(enablerAssign);
          }
        }

        if (ToolCommandLineOptions.Get().SuperVerboseMode)
        {
          var p = asserts.Count == 1 ? "" : "s";
          Output.PrintLine("..... Instrumented '{0}' assertion" + p + " for '{1}'",
            asserts.Count, mr.Name);
        }
      }
    }

    /// <summary>
    /// Creates race-checking assertions for the given thread.
    /// </summary>
    /// <param name="thread">Thread</param>
    /// <param name="mr">MemoryRegion</param>
    /// <returns>Race-checking assertions</returns>
    private List<Tuple<Thread, Thread, AssertCmd>> CreateRaceCheckingAssertions(Thread thread, Variable mr)
    {
      var asserts = new List<Tuple<Thread, Thread, AssertCmd>>();
      var threads = new List<Thread>();

      foreach (var pair in this.AC.ThreadMemoryRegions)
      {
        if (!pair.Value.Contains(mr))
          continue;
        if (thread.Children.Count(val => val.Name.Equals(pair.Key.Name)) > 1)
        {
          threads.Add(pair.Key);
          threads.Add(pair.Key);
        }
        else
        {
          threads.Add(pair.Key);
        }
      }

      if (threads.Count < 2)
        return asserts;

      for (int i = 0; i < threads.Count; i++)
      {
        for (int j = i + 1; j < threads.Count; j++)
        {
          asserts.Add(new Tuple<Thread, Thread, AssertCmd>(threads[i], threads[j],
            this.CreateRaceCheckingAssertion(threads[i], threads[j], mr)));
        }
      }

      return asserts;
    }

    private AssertCmd CreateRaceCheckingAssertion(Thread t1, Thread t2, Variable mr)
    {
      Variable wacs1 = this.AC.GetWriteAccessCheckingVariables().Find(val =>
        val.Name.Contains(this.AC.GetWriteAccessVariableName(t1, mr.Name)));
      Variable wacs2 = this.AC.GetWriteAccessCheckingVariables().Find(val =>
        val.Name.Contains(this.AC.GetWriteAccessVariableName(t2, mr.Name)));
      Variable racs1 = this.AC.GetReadAccessCheckingVariables().Find(val =>
        val.Name.Contains(this.AC.GetReadAccessVariableName(t1, mr.Name)));
      Variable racs2 = this.AC.GetReadAccessCheckingVariables().Find(val =>
        val.Name.Contains(this.AC.GetReadAccessVariableName(t2, mr.Name)));

      if (wacs1 == null || wacs2 == null || racs1 == null || racs2 == null)
        return null;

      IdentifierExpr wacsExpr1 = new IdentifierExpr(wacs1.tok, wacs1);
      IdentifierExpr wacsExpr2 = new IdentifierExpr(wacs2.tok, wacs2);
      IdentifierExpr racsExpr1 = new IdentifierExpr(racs1.tok, racs1);
      IdentifierExpr racsExpr2 = new IdentifierExpr(racs2.tok, racs2);

      Expr accessesExpr = null;
      if (t1.Name.Equals(t2.Name))
      {
        accessesExpr = wacsExpr1;
      }
      else
      {
        accessesExpr = Expr.Or(Expr.Or(Expr.And(wacsExpr1, wacsExpr2),
          Expr.And(wacsExpr1, racsExpr2)), Expr.And(racsExpr1, wacsExpr2));
      }

      Expr checkExpr = null;
      foreach (var l in this.AC.Locks)
      {
        var ls1  = this.AC.MemoryLocksets.Find(val => val.Lock.Name.Equals(l.Name) &&
          val.TargetName.Equals(mr.Name) && val.Thread.Name.Equals(t1.Name));
        var ls2  = this.AC.MemoryLocksets.Find(val => val.Lock.Name.Equals(l.Name) &&
          val.TargetName.Equals(mr.Name) && val.Thread.Name.Equals(t2.Name));

        IdentifierExpr lsExpr1 = new IdentifierExpr(ls1.Id.tok, ls1.Id);
        IdentifierExpr lsExpr2 = new IdentifierExpr(ls2.Id.tok, ls2.Id);

        Expr lsAndExpr = null;
        if (t1.Name.Equals(t2.Name))
        {
          lsAndExpr = lsExpr1;
        }
        else
        {
          lsAndExpr = Expr.And(lsExpr1, lsExpr2);
        }

        if (checkExpr == null)
        {
          checkExpr = lsAndExpr;
        }
        else
        {
          checkExpr = Expr.Or(checkExpr, lsAndExpr);
        }
      }

      if (this.AC.Locks.Count == 0)
      {
        checkExpr = Expr.False;
      }

      Expr acsImpExpr = Expr.Imp(accessesExpr, checkExpr);

      AssertCmd assert = new AssertCmd(Token.NoToken, acsImpExpr);
      assert.Attributes = new QKeyValue(Token.NoToken, "resource",
        new List<object>() { mr.Name }, assert.Attributes);
      assert.Attributes = new QKeyValue(Token.NoToken, "race_checking",
        new List<object>(), assert.Attributes);

      return assert;
    }

    private Tuple<Block, int> GetInstrumentationPointForThread(Thread thread)
    {
      Block block = null;
      var index = -1;

      Implementation targetFunction = null;
      if (thread.IsMain)
      {
        targetFunction = thread.Function;
      }
      else
      {
        targetFunction = thread.Parent.Function;
      }

      if (thread.IsMain || thread.Joiner == null)
      {
        foreach (var b in targetFunction.Blocks)
        {
          if (b.TransferCmd is ReturnCmd)
          {
            block = b;
            break;
          }
        }
      }
      else
      {
        for (int idx = 0; idx < thread.Joiner.Item2.Cmds.Count; idx++)
        {
          if (!(thread.Joiner.Item2.Cmds[idx] is CallCmd))
            continue;

          var call = thread.Joiner.Item2.Cmds[idx] as CallCmd;
          if (!call.Equals(thread.Joiner.Item3))
            continue;

          block = thread.Joiner.Item2;
          if (index + 1 == thread.Joiner.Item2.Cmds.Count)
            index = -1;
          else
            index = idx + 1;
          break;
        }
      }

      // HACK for functions with infinite loops
      if (block == null)
      {
        block = targetFunction.Blocks[targetFunction.Blocks.Count - 1];
      }

      return new Tuple<Block, int>(block, index);
    }

    private AssumeCmd CreateCaptureStateAssume(Variable mr)
    {
      AssumeCmd assume = new AssumeCmd(Token.NoToken, Expr.True);
      assume.Attributes = new QKeyValue(Token.NoToken, "checker",
        new List<object>(), assume.Attributes);
      assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
        new List<object>() { "check_state_" + this.CheckCounter++ }, assume.Attributes);
      assume.Attributes = new QKeyValue(Token.NoToken, "resource",
        new List<object>() { mr.Name }, assume.Attributes);
      return assume;
    }

    #endregion
  }
}
