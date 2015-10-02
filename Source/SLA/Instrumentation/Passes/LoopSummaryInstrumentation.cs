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

namespace Lockpwn.Instrumentation
{
  internal class LoopSummaryInstrumentation : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    private HashSet<Constant> ExistentialBooleans;
    private Dictionary<Thread, int> CandidateCounter;

    public LoopSummaryInstrumentation(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.ExistentialBooleans = new HashSet<Constant>();
      this.CandidateCounter = new Dictionary<Thread, int>();
    }

    public void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Console.WriteLine("... LoopSummaryInstrumentation");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var thread in this.AC.Threads)
      {
        this.CandidateCounter.Add(thread, 0);

        this.InstrumentThread(thread);

        if (ToolCommandLineOptions.Get().SuperVerboseMode)
        {
          var suffix = this.CandidateCounter[thread] == 1 ? "" : "s";
          Console.WriteLine("..... Instrumented '{0}' loop invariant candidate" + suffix +
            " in '{1}'", this.CandidateCounter[thread], thread.Name);
        }
      }

      this.InstrumentExistentialBooleans();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Console.WriteLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void InstrumentThread(Thread thread)
    {
      foreach (var impl in this.AC.GetThreadSpecificFunctions(thread))
      {
        this.IdentifyAndInstrumentLoopsInImplementation(thread, impl);
      }
    }

    private void IdentifyAndInstrumentLoopsInImplementation(Thread thread, Implementation impl)
    {
      var loopHeaders = this.ComputeLoopHeaders(impl);
      if (loopHeaders.Count == 0)
        return;

      var curLockSets = this.AC.CurrentLocksets.Where(val => val.Thread.Equals(thread));
      var memLockSets = this.AC.MemoryLocksets.Where(val => val.Thread.Equals(thread));
      var writeSets = this.AC.GetWriteAccessCheckingVariables().
        Where(val => val.Name.EndsWith(thread.Name));
      var readSets = this.AC.GetReadAccessCheckingVariables().
        Where(val => val.Name.EndsWith(thread.Name));

      foreach (var header in loopHeaders)
      {
        foreach (var read in readSets)
        {
          this.InstrumentAssertCandidate(thread, header, read, false);
        }

        foreach (var write in writeSets)
        {
          this.InstrumentAssertCandidate(thread, header, write, false);
        }

        foreach (var mls in memLockSets)
        {
          this.InstrumentAssertCandidate(thread, header, mls.Id, true);
        }

        foreach (var cls in curLockSets)
        {
          this.InstrumentAssertCandidate(thread, header, cls.Id, true);
        }
      }
    }

    private List<Block> ComputeLoopHeaders(Implementation impl)
    {
      var cfg = this.AC.GetImplementationGraph(impl);
      cfg.ComputeLoops();
      return cfg.Headers.ToList();
    }

    private void InstrumentExistentialBooleans()
    {
      foreach (var b in this.ExistentialBooleans)
      {
        b.Attributes = new QKeyValue(Token.NoToken, "existential", new List<object>() { Expr.True }, null);
        this.AC.TopLevelDeclarations.Add(b);
      }
    }

    private void InstrumentAssertCandidate(Thread thread, Block block, Variable variable, bool value)
    {
      var cons = this.CreateConstant(thread);
      Expr expr = this.CreateImplExpr(cons, variable, value);
      block.Cmds.Insert(0, new AssertCmd(Token.NoToken, expr));
    }

    private void InstrumentAssert(Block block, Variable variable, bool value)
    {
      Expr expr = this.CreateExpr(variable, value);
      block.Cmds.Insert(0, new AssertCmd(Token.NoToken, expr));
    }

    private Expr CreateImplExpr(Constant cons, Variable v, bool value)
    {
      return Expr.Imp(new IdentifierExpr(cons.tok, cons), this.CreateExpr(v, value));
    }

    private Expr CreateExpr(Variable v, bool value)
    {
      Expr expr = null;
      if (value) expr = new IdentifierExpr(v.tok, v);
      else expr = Expr.Not(new IdentifierExpr(v.tok, v));
      return expr;
    }

    private Constant CreateConstant(Thread thread)
    {
      Constant cons = new Constant(Token.NoToken, new TypedIdent(Token.NoToken, "_b$" +
        thread.Name + "$" + this.CandidateCounter[thread], Microsoft.Boogie.Type.Bool), false);
      this.ExistentialBooleans.Add(cons);
      this.CandidateCounter[thread]++;
      return cons;
    }
  }
}
