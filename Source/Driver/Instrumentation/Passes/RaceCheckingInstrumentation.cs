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
  internal class RaceCheckingInstrumentation : IPass
  {
    private AnalysisContext AC;
    private Thread Thread;
    private ExecutionTimer Timer;

    private int ReadCounter;
    private int WriteCounter;

    internal RaceCheckingInstrumentation(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.ReadCounter = 0;
      this.WriteCounter = 0;
    }

    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... RaceCheckingInstrumentation");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var thread in this.AC.Threads)
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

      this.AddAccessFuncs(AccessType.WRITE);
      this.AddAccessFuncs(AccessType.READ);

      foreach (var impl in this.AC.GetThreadSpecificFunctions(thread))
      {
        this.InstrumentImplementation(impl);
        this.InstrumentProcedure(impl);
      }

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        var p1 = this.ReadCounter == 1 ? "" : "es";
        var p2 = this.WriteCounter == 1 ? "" : "es";
        Output.PrintLine("..... Instrumented '{0}' read access" + p1 + " in {1}",
          this.ReadCounter, thread);
        Output.PrintLine("..... Instrumented '{0}' write access" + p2 + " in {1}",
          this.WriteCounter, thread);
      }

      this.ReadCounter = 0;
      this.WriteCounter = 0;
    }

    #region race checking verification variables and methods

    private void AddAccessFuncs(AccessType access)
    {
      foreach (var mr in this.AC.ThreadMemoryRegions[this.Thread])
      {
        List<Variable> inParams = new List<Variable>();

        if (mr.TypedIdent.Type.IsMap)
        {
          inParams.Add(new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr",
            this.AC.MemoryModelType)));
        }

        Procedure proc = new Procedure(Token.NoToken, this.MakeAccessFuncName(access, mr.Name),
          new List<TypeVariable>(), inParams, new List<Variable>(),
          new List<Requires>(), new List<IdentifierExpr>(), new List<Ensures>());
        proc.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        this.AC.TopLevelDeclarations.Add(proc);
        this.AC.ResContext.AddProcedure(proc);

        var cmds = new List<Cmd>();

        foreach (var ls in this.AC.MemoryLocksets.Where(val => val.Thread.Equals(this.Thread)))
        {
          if (!ls.TargetName.Equals(mr.Name))
            continue;

          foreach (var cls in this.AC.CurrentLocksets.Where(val => val.Thread.Equals(this.Thread)))
          {
            if (!cls.Lock.Name.Equals(ls.Lock.Name))
              continue;

            IdentifierExpr lsExpr = new IdentifierExpr(ls.Id.tok, ls.Id);

            cmds.Add(new AssignCmd(Token.NoToken,
              new List<AssignLhs>() {
              new SimpleAssignLhs(Token.NoToken, lsExpr)
            }, new List<Expr> {
              Expr.And(new IdentifierExpr(cls.Id.tok, cls.Id), lsExpr)
            }));

            proc.Modifies.Add(lsExpr);
            break;
          }
        }

        if (access == AccessType.WRITE)
        {
          foreach (var acv in this.AC.GetWriteAccessCheckingVariables().
            Where(val => val.Name.EndsWith(this.Thread.Name + "_$" + this.Thread.Id)))
          {
            if (!acv.Name.Split('_')[1].Equals(mr.Name))
              continue;

            var wacsExpr = new IdentifierExpr(acv.tok, acv);

            cmds.Add(new AssignCmd(Token.NoToken,
              new List<AssignLhs>() {
              new SimpleAssignLhs(Token.NoToken, wacsExpr)
            }, new List<Expr> {
              Expr.True
            }));

            proc.Modifies.Add(wacsExpr);
          }
        }
        else if (access == AccessType.READ)
        {
          foreach (var acv in this.AC.GetReadAccessCheckingVariables().
            Where(val => val.Name.EndsWith(this.Thread.Name + "_$" + this.Thread.Id)))
          {
            if (!acv.Name.Split('_')[1].Equals(mr.Name))
              continue;

            var racsExpr = new IdentifierExpr(acv.tok, acv);

            cmds.Add(new AssignCmd(Token.NoToken,
              new List<AssignLhs>() {
              new SimpleAssignLhs(Token.NoToken, racsExpr)
            }, new List<Expr> {
              Expr.True
            }));

            proc.Modifies.Add(racsExpr);
          }
        }

        List<BigBlock> blocks = null;
        if (mr.TypedIdent.Type.IsMap)
        {
          var ptr = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr",
            this.AC.MemoryModelType));
          var watchdog = this.AC.GetAccessWatchdogConstants().Find(val =>
            val.Name.Contains("WATCHED_ACCESS_" + mr.Name));

          var ptrExpr = new IdentifierExpr(ptr.tok, ptr);
          var watchdogExpr = new IdentifierExpr(watchdog.tok, watchdog);
          var guardExpr = Expr.Eq(watchdogExpr, ptrExpr);

          var ifStmts = new StmtList(new List<BigBlock> {
            new BigBlock(Token.NoToken, null, cmds, null, null) }, Token.NoToken);
          var ifCmd = new IfCmd(Token.NoToken, guardExpr, ifStmts, null, null);

          blocks = new List<BigBlock> {
            new BigBlock(Token.NoToken, "_" + access.ToString(), new List<Cmd>(), ifCmd, null) };
        }
        else
        {
          blocks = new List<BigBlock> {
            new BigBlock(Token.NoToken, "_" + access.ToString(), cmds, null, null) };
        }

        Implementation impl = new Implementation(Token.NoToken, this.MakeAccessFuncName(access, mr.Name),
          new List<TypeVariable>(), inParams, new List<Variable>(), new List<Variable>(),
          new StmtList(blocks, Token.NoToken));

        impl.Proc = proc;
        impl.AddAttribute("inline", new object[] { new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) });

        this.AC.TopLevelDeclarations.Add(impl);
      }
    }

    #endregion

    #region race checking instrumentation

    private void InstrumentImplementation(Implementation impl)
    {
      if (this.AC.IsAToolFunc(impl.Name))
        return;
      if (Utilities.ShouldNotAccessFunction(impl.Name))
        return;
      if (Utilities.ShouldSkipFromAnalysis(impl.Name))
        return;

      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is AssignCmd)) continue;
          var assign = block.Cmds[idx] as AssignCmd;

          var lhss = assign.Lhss.OfType<SimpleAssignLhs>();
          var rhssMap = assign.Rhss.OfType<NAryExpr>();
          var rhss = assign.Rhss.OfType<IdentifierExpr>();

          CallCmd call = null;
          if (lhss.Count() == 1)
          {
            var lhs = lhss.First();
            if (lhs.DeepAssignedIdentifier.Name.StartsWith("$M."))
            {
              if (this.AC.ThreadMemoryRegions[this.Thread].Any(val =>
                val.Name.Equals(lhs.DeepAssignedIdentifier.Name)))
              {
                call = new CallCmd(Token.NoToken,
                  this.MakeAccessFuncName(AccessType.WRITE, lhs.DeepAssignedIdentifier.Name),
                  new List<Expr>(), new List<IdentifierExpr>());

                if (rhssMap.Count() == 0 && assign.Rhss.Count == 1 &&
                  assign.Rhss[0].ToString().StartsWith("$p"))
                {
                  call.Attributes = new QKeyValue(Token.NoToken, "rhs",
                    new List<object>() { assign.Rhss[0]
                    }, call.Attributes);
                }
                else if (rhssMap.Count() == 1 &&
                  rhssMap.First().Fun.FunctionName.StartsWith("$store."))
                {
                  var ptr = rhssMap.First().Args[1];
                  call.Ins.Add(ptr);
                }

                this.WriteCounter++;
              }
              else
              {
                call = new CallCmd(Token.NoToken, "_NO_OP",
                  new List<Expr>(), new List<IdentifierExpr>());
              }
            }
          }

          if (rhssMap.Count() == 1)
          {
            var rhs = rhssMap.First();
            if (rhs.Fun.FunctionName.StartsWith("$load.") && rhs.Args.Count == 2 &&
              (rhs.Args[0] as IdentifierExpr).Name.StartsWith("$M."))
            {
              if (this.AC.ThreadMemoryRegions[this.Thread].Any(val =>
                val.Name.Equals((rhs.Args[0] as IdentifierExpr).Name)))
              {
                call = new CallCmd(Token.NoToken,
                  this.MakeAccessFuncName(AccessType.READ, (rhs.Args[0] as IdentifierExpr).Name),
                  new List<Expr> { rhs.Args[1] }, new List<IdentifierExpr>());

                this.ReadCounter++;
              }
              else
              {
                call = new CallCmd(Token.NoToken, "_NO_OP",
                  new List<Expr>(), new List<IdentifierExpr>());
              }
            }
          }
          else if (rhss.Count() == 1)
          {
            var rhs = rhss.First();
            if (rhs.Name.StartsWith("$M."))
            {
              if (this.AC.ThreadMemoryRegions[this.Thread].Any(val =>
                val.Name.Equals(rhs.Name)))
              {
                call = new CallCmd(Token.NoToken,
                  this.MakeAccessFuncName(AccessType.READ, rhs.Name),
                  new List<Expr>(), new List<IdentifierExpr>());

                this.ReadCounter++;
              }
              else
              {
                call = new CallCmd(Token.NoToken, "_NO_OP",
                  new List<Expr>(), new List<IdentifierExpr>());
              }
            }
          }

          if (call != null)
            block.Cmds.Insert(idx + 1, call);
        }
      }
    }

    private void InstrumentProcedure(Implementation impl)
    {
      if (this.AC.IsAToolFunc(impl.Name))
        return;
      if (Utilities.ShouldNotAccessFunction(impl.Name))
        return;
      if (Utilities.ShouldSkipFromAnalysis(impl.Name))
        return;

      foreach (var acv in this.AC.GetWriteAccessCheckingVariables().
        Where(val => val.Name.EndsWith(this.Thread.Name + "_$" + this.Thread.Id)))
      {
        string targetName = acv.Name.Split('_')[1];
        if (!this.AC.ThreadMemoryRegions[this.Thread].Any(val => val.Name.Equals(targetName)))
          continue;

        if (!impl.Proc.Modifies.Any(mod => mod.Name.Equals(acv.Name)))
          impl.Proc.Modifies.Add(new IdentifierExpr(acv.tok, acv));
      }

      foreach (var acv in this.AC.GetReadAccessCheckingVariables().
        Where(val => val.Name.EndsWith(this.Thread.Name + "_$" + this.Thread.Id)))
      {
        string targetName = acv.Name.Split('_')[1];
        if (!this.AC.ThreadMemoryRegions[this.Thread].Any(val => val.Name.Equals(targetName)))
          continue;

        if (!impl.Proc.Modifies.Any(mod => mod.Name.Equals(acv.Name)))
          impl.Proc.Modifies.Add(new IdentifierExpr(acv.tok, acv));
      }
    }

    #endregion

    #region helper functions

    private string MakeAccessFuncName(AccessType access, string name)
    {
      return "_" + access.ToString() + "_LS_" + name + "_$" +
        this.Thread.Name + "_$" + this.Thread.Id;
    }

    #endregion
  }
}
