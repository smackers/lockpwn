//===-----------------------------------------------------------------------==//
//
//                Lockpwn - a Static Lockset Analyser for Boogie
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
using System.Runtime.InteropServices;

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
        Console.WriteLine("... AccessCheckingInstrumentation");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.InstrumentMain();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Console.WriteLine("..... [{0}]", this.Timer.Result());
      }
    }

    #region access checking instrumentation

    private void InstrumentMain()
    {
      Block exitBlock = null;
      foreach (var b in this.AC.EntryPoint.Blocks)
      {
        if (b.TransferCmd is ReturnCmd)
        {
          exitBlock = b;
          break;
        }
      }

      foreach (var mr in this.AC.SharedMemoryRegions)
      {
        var asserts = this.CreateRaceCheckingAssertions(mr);
        if (asserts.Count == 0)
          continue;

        foreach (var assert in asserts)
        {
          exitBlock.Cmds.Add(this.CreateCaptureStateAssume(mr));
          exitBlock.Cmds.Add(assert);
        }

        if (ToolCommandLineOptions.Get().SuperVerboseMode)
        {
          if (asserts.Count > 0)
            Console.WriteLine("..... Instrumented assertions for '{0}'", mr.Name);
          else
            Console.WriteLine("..... Instrumented assertion for '{0}'", mr.Name);
        }
      }
    }

    private List<AssertCmd> CreateRaceCheckingAssertions(Variable mr)
    {
      var asserts = new List<AssertCmd>();
      var threads = new List<Thread>();

      foreach (var pair in this.AC.ThreadMemoryRegions)
      {
        if (!pair.Value.Contains(mr))
          continue;
        threads.Add(pair.Key);
      }

      if (threads.Count < 2)
        return asserts;

      for (int i = 0; i < threads.Count; i++)
      {
        for (int j = i + 1; j < threads.Count; j++)
        {
          asserts.Add(this.CreateRaceCheckingAssertion(threads[i], threads[j], mr));
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
