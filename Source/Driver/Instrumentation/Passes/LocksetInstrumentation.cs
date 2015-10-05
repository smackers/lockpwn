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

namespace Lockpwn.Instrumentation
{
  internal class LocksetInstrumentation : IPass
  {
    private AnalysisContext AC;
    private Thread Thread;
    private ExecutionTimer Timer;

    private int LockCounter;
    private int UnlockCounter;

    internal LocksetInstrumentation(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.LockCounter = 0;
      this.UnlockCounter = 0;
    }

    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... LocksetInstrumentation");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.InstrumentMainFunction();
      this.AddNonCheckedFunc();

      foreach (var thread in this.AC.ThreadTemplates)
      {
        this.InstrumentThread(thread);
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void InstrumentThread(Thread thread)
    {
      this.Thread = thread;

      this.AddUpdateLocksetFunc(Microsoft.Boogie.Type.Int);

      foreach (var impl in this.AC.GetThreadSpecificFunctions(thread))
      {
        this.InstrumentImplementation(impl);
        this.InstrumentProcedure(impl);
      }

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        var p1 = this.LockCounter == 1 ? "" : "s";
        var p2 = this.UnlockCounter == 1 ? "" : "s";
        Output.PrintLine("..... Instrumented '{0}' lock" + p1 + " in '{1}'",
          this.LockCounter, thread.Name);
        Output.PrintLine("..... Instrumented '{0}' unlock" + p2 + " in '{1}'",
          this.UnlockCounter, thread.Name);
      }

      this.LockCounter = 0;
      this.UnlockCounter = 0;
    }

    #region lockset analysis variables and methods

    private void AddUpdateLocksetFunc(Microsoft.Boogie.Type type = null)
    {
      var str = "_UPDATE_CLS_$";

      var inParams = new List<Variable>();
      var outParams = new List<Variable>();
      var in1 = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
        "lock", this.AC.MemoryModelType));
      var in2 = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
        "isLocked", Microsoft.Boogie.Type.Bool));

      if (type != null)
      {
        str += type.ToString() + "$";
        outParams.Add(new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
          "$r", type)));
      }

      inParams.Add(in1);
      inParams.Add(in2);

      Procedure proc = new Procedure(Token.NoToken, str + this.Thread.Name,
                         new List<TypeVariable>(), inParams, outParams,
                         new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      foreach (var ls in this.AC.CurrentLocksets.Where(val => val.Thread.Equals(this.Thread)))
      {
        proc.Modifies.Add(new IdentifierExpr(ls.Id.tok, ls.Id));
      }

      this.AC.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);

      Block b = new Block(Token.NoToken, "_UPDATE", new List<Cmd>(), new ReturnCmd(Token.NoToken));

      foreach (var ls in this.AC.CurrentLocksets.Where(val => val.Thread.Equals(this.Thread)))
      {
        List<AssignLhs> newLhss = new List<AssignLhs>();
        List<Expr> newRhss = new List<Expr>();

        newLhss.Add(new SimpleAssignLhs(ls.Id.tok, new IdentifierExpr(ls.Id.tok, ls.Id)));
        newRhss.Add(new NAryExpr(Token.NoToken, new IfThenElse(Token.NoToken),
          new List<Expr>(new Expr[] { Expr.Eq(new IdentifierExpr(in1.tok, in1),
              new IdentifierExpr(ls.Lock.tok, ls.Lock)),
            new IdentifierExpr(in2.tok, in2), new IdentifierExpr(ls.Id.tok, ls.Id)
          })));

        var assign = new AssignCmd(Token.NoToken, newLhss, newRhss);
        b.Cmds.Add(assign);
      }

      Implementation impl = new Implementation(Token.NoToken, str + this.Thread.Name,
                              new List<TypeVariable>(), inParams, outParams,
                              new List<Variable>(), new List<Block>());

      if (type == Microsoft.Boogie.Type.Int)
      {
        var iVar = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
          "$i", type));
        var iVarId = new IdentifierExpr(Token.NoToken, iVar);
        impl.LocVars.Add(iVar);
        b.Cmds.Add(new HavocCmd(Token.NoToken, new List<IdentifierExpr> { iVarId }));
        b.Cmds.Add(new AssignCmd(Token.NoToken,
          new List<AssignLhs> { new SimpleAssignLhs(Token.NoToken,
              new IdentifierExpr(Token.NoToken, outParams[0]))
          },
          new List<Expr> { iVarId }));
      }

      impl.Blocks.Add(b);
      impl.Proc = proc;
      impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.TopLevelDeclarations.Add(impl);
    }

    private void AddNonCheckedFunc()
    {
      Procedure proc = new Procedure(Token.NoToken, "_NO_OP",
        new List<TypeVariable>(), new List<Variable>(), new List<Variable>(),
        new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
      proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

      this.AC.TopLevelDeclarations.Add(proc);
      this.AC.ResContext.AddProcedure(proc);
    }

    #endregion

    #region lockset instrumentation

    private void InstrumentImplementation(Implementation impl)
    {
      if (this.AC.IsAToolFunc(impl.Name))
        return;
      if (!Utilities.ShouldAccessFunction(impl.Name))
        return;
      if (Utilities.ShouldSkipFromAnalysis(impl.Name))
        return;

      foreach (var b in impl.Blocks)
      {
        foreach (var c in b.Cmds.OfType<CallCmd>())
        {
          if (c.callee.Equals("pthread_mutex_lock"))
          {
            c.callee = "_UPDATE_CLS_$int$" + this.Thread.Name;
            c.Ins.Add(Expr.True);
            this.LockCounter++;
          }
          else if (c.callee.Equals("pthread_mutex_unlock") ||
            c.callee.Equals("spin_unlock"))
          {
            c.callee = "_UPDATE_CLS_$int$" + this.Thread.Name;
            c.Ins.Add(Expr.False);
            this.UnlockCounter++;
          }
        }
      }
    }

    private void InstrumentProcedure(Implementation impl)
    {
      if (this.AC.IsAToolFunc(impl.Name))
        return;
      if (!Utilities.ShouldAccessFunction(impl.Name))
        return;
      if (Utilities.ShouldSkipFromAnalysis(impl.Name))
        return;

      foreach (var ls in this.AC.CurrentLocksets.Where(val => val.Thread.Equals(this.Thread)))
      {
        impl.Proc.Modifies.Add(new IdentifierExpr(ls.Id.tok, ls.Id));
      }

      foreach (var ls in this.AC.MemoryLocksets.Where(val => val.Thread.Equals(this.Thread)))
      {
        impl.Proc.Modifies.Add(new IdentifierExpr(ls.Id.tok, ls.Id));
      }
    }

    private void InstrumentMainFunction()
    {
      foreach (var ls in this.AC.CurrentLocksets)
      {
        var require = new Requires(false, Expr.Not(new IdentifierExpr(ls.Id.tok, ls.Id)));
        this.AC.EntryPoint.Proc.Requires.Add(require);
      }

      foreach (var ls in this.AC.MemoryLocksets)
      {
        Requires require = new Requires(false, new IdentifierExpr(ls.Id.tok, ls.Id));
        this.AC.EntryPoint.Proc.Requires.Add(require);
      }

      foreach (var acv in this.AC.GetWriteAccessCheckingVariables())
      {
        Requires require = new Requires(false, Expr.Not(new IdentifierExpr(acv.tok, acv)));
        this.AC.EntryPoint.Proc.Requires.Add(require);
      }

      foreach (var acv in this.AC.GetReadAccessCheckingVariables())
      {
        Requires require = new Requires(false, Expr.Not(new IdentifierExpr(acv.tok, acv)));
        this.AC.EntryPoint.Proc.Requires.Add(require);
      }
    }

    #endregion
  }
}
