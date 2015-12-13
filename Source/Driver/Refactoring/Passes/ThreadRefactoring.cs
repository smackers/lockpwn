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
using Microsoft.Boogie.GraphUtil;

using Lockpwn.Analysis;
using Lockpwn.IO;

namespace Lockpwn.Refactoring
{
  internal class ThreadRefactoring : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    private Dictionary<Thread, HashSet<string>> AlreadyAnalysedFunctions;
    private HashSet<string> DuplicatedFunctions;

    public ThreadRefactoring(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;

      this.AlreadyAnalysedFunctions = new Dictionary<Thread, HashSet<string>>();
      this.DuplicatedFunctions = new HashSet<string>();
    }

    public void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... ThreadRefactoring");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var thread in this.AC.Threads)
      {
        this.ParseAndRenameNestedFunctions(thread, thread.Function, null);
        if (ToolCommandLineOptions.Get().SuperVerboseMode)
          Output.PrintLine("..... Separated call graph of {0}", thread);
      }

      this.CleanUp();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void ParseAndRenameNestedFunctions(Thread thread, Implementation impl, IdentifierExpr tid)
    {
      if (!this.AlreadyAnalysedFunctions.ContainsKey(thread))
        this.AlreadyAnalysedFunctions.Add(thread, new HashSet<string>());
      if (this.AlreadyAnalysedFunctions[thread].Contains(impl.Name))
        return;
      this.AlreadyAnalysedFunctions[thread].Add(impl.Name);

      if (!this.AC.MainThread.Name.Equals(impl.Name) &&
        ((!Utilities.ShouldNotAccessFunction(impl.Name) &&
        !Utilities.ShouldSkipFromAnalysis(impl.Name)) ||
          impl.Name.StartsWith("__call_wrapper") ||
          impl.Name.StartsWith("pthread_create")))
      {
        impl = this.DuplicateFunction(thread, impl, tid);
      }

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          if (cmd is CallCmd)
          {
            this.ParseAndRenameFunctionsInCall(thread, impl, block, cmd as CallCmd, tid);
          }
          else if (cmd is AssignCmd)
          {
            this.ParseAndRenameFunctionsInAssign(thread, impl, block, cmd as AssignCmd, tid);
          }
          else if (cmd is AssumeCmd)
          {
            this.ParseAndRenameFunctionsInAssume(thread, impl, block, cmd as AssumeCmd, tid);
          }
        }
      }
    }

    private void ParseAndRenameFunctionsInCall(Thread thread, Implementation impl, Block block,
      CallCmd cmd, IdentifierExpr tid)
    {
      var callee = this.AC.GetImplementation(cmd.callee);

      if (callee == null || this.AC.IsAToolFunc(callee.Name))
        return;

      if (Utilities.ShouldNotAccessFunction(callee.Name))
        return;
      if (Utilities.ShouldSkipFromAnalysis(callee.Name) &&
        !callee.Name.StartsWith("__call_wrapper") &&
        !callee.Name.StartsWith("pthread_create"))
        return;

      if (cmd.callee.Equals(thread.Name))
      {
        cmd.callee = thread.Name + "$" + thread.Id;
        return;
      }
      else if (cmd.callee.StartsWith("pthread_create"))
      {
        tid = cmd.Ins[0] as IdentifierExpr;
        thread = this.AC.Threads.First(val => !val.IsMain && val.Id.IsEqual(tid));
        cmd.callee = cmd.callee + "$" + thread.Name + "$" + tid.Name;
        cmd.Ins[2] = new IdentifierExpr(Token.NoToken, thread.Name + "$" + thread.Id, Microsoft.Boogie.Type.Int);
      }
      else if (thread.IsMain)
      {
        cmd.callee = cmd.callee + "$" + thread.Name;
      }
      else
      {
        cmd.callee = cmd.callee + "$" + thread.Name + "$" + thread.Id.Name;
      }

      this.ParseAndRenameNestedFunctions(thread, callee, tid);

      foreach (var expr in cmd.Ins)
      {
        if (!(expr is IdentifierExpr)) continue;
        callee = this.AC.GetImplementation((expr as IdentifierExpr).Name);

        if (callee != null && !Utilities.ShouldNotAccessFunction(callee.Name))
        {
          this.ParseAndRenameNestedFunctions(thread, callee, tid);

          var suffix = "";
          if (thread.IsMain)
          {
            suffix = "$" + thread.Name;
          }
          else
          {
            suffix = "$" + thread.Name + "$" + thread.Id.Name;
          }

          (expr as IdentifierExpr).Name = (expr as IdentifierExpr).Name + suffix;
        }
      }
    }

    private void ParseAndRenameFunctionsInAssign(Thread thread, Implementation impl, Block block,
      AssignCmd cmd, IdentifierExpr tid)
    {
      foreach (var rhs in cmd.Rhss)
      {
        if (rhs is NAryExpr)
        {
          this.ParseAndRenameNAryExpr(thread, impl, block, rhs as NAryExpr, tid);
        }
        else if (rhs is IdentifierExpr)
        {
          this.ParseAndRenameIdentifier(thread, impl, block, rhs as IdentifierExpr, tid);
        }
      }
    }

    private void ParseAndRenameFunctionsInAssume(Thread thread, Implementation impl, Block block,
      AssumeCmd cmd, IdentifierExpr tid)
    {
      if (cmd.Expr is NAryExpr)
      {
        this.ParseAndRenameNAryExpr(thread, impl, block, cmd.Expr as NAryExpr, tid);
      }
      else if (cmd.Expr is IdentifierExpr)
      {
        this.ParseAndRenameIdentifier(thread, impl, block, cmd.Expr as IdentifierExpr, tid);
      }
    }

    private void ParseAndRenameNAryExpr(Thread thread, Implementation impl, Block block,
      NAryExpr expr, IdentifierExpr tid)
    {
      foreach (var arg in expr.Args)
      {
        if (arg is NAryExpr)
        {
          this.ParseAndRenameNAryExpr(thread, impl, block, arg as NAryExpr, tid);
        }
        else if (arg is IdentifierExpr)
        {
          this.ParseAndRenameIdentifier(thread, impl, block, arg as IdentifierExpr, tid);
        }
      }
    }

    private void ParseAndRenameIdentifier(Thread thread, Implementation impl, Block block,
      IdentifierExpr identifier, IdentifierExpr tid)
    {
      var callee = this.AC.GetImplementation(identifier.Name);
      if (callee == null)
        return;

      if (this.AC.IsAToolFunc(callee.Name) ||
        Utilities.ShouldNotAccessFunction(callee.Name) ||
        Utilities.ShouldSkipFromAnalysis(callee.Name))
        return;

      if (identifier.Name.Equals(thread.Name))
      {
        var t = this.AC.Threads.First(val => val.Name.Equals(
          identifier.Name) && val.Id.IsEqual(tid));
        identifier.Name = t.Name + "$" + t.Id;
        return;
      }

      this.ParseAndRenameNestedFunctions(thread, callee, tid);

      var suffix = "";
      if (thread.IsMain)
      {
        suffix = "$" + thread.Name;
      }
      else
      {
        suffix = "$" + thread.Name + "$" + thread.Id.Name;
      }

      identifier.Name = identifier.Name + suffix;
    }

    private Implementation DuplicateFunction(Thread thread, Implementation func, IdentifierExpr tid)
    {
      string suffix = "";
      if (func.Name.Equals(thread.Name))
      {
        suffix = "$" + thread.Id.Name;
      }
      else if (thread.IsMain)
      {
        suffix = "$" + thread.Name;
      }
      else
      {
        suffix = "$" + thread.Name + "$" + thread.Id.Name;
      }

      var cons = this.AC.GetConstant(func.Name);
      if (cons != null)
      {
        var consName = cons.Name + suffix;
        var newCons = new Constant(cons.tok,
          new TypedIdent(cons.TypedIdent.tok, consName,
            cons.TypedIdent.Type), cons.Unique);
        this.AC.TopLevelDeclarations.Add(newCons);
      }

      var newProc = new Duplicator().Visit(func.Proc.Clone()) as Procedure;
      var newImpl = new Duplicator().Visit(func.Clone()) as Implementation;

      newProc.Name = func.Proc.Name + suffix;
      newImpl.Name = func.Name + suffix;
      newImpl.Proc = newProc;

      if (func.Name.Equals(thread.Function.Name))
      {
        newImpl.Proc.Attributes = new QKeyValue(Token.NoToken,
          "thread", new List<object>(), null);
        newImpl.Attributes = new QKeyValue(Token.NoToken,
          "thread", new List<object>(), null);
      }
      else
      {
        newImpl.Attributes = new QKeyValue(Token.NoToken, "tag",
          new List<object>() { thread.Name }, func.Attributes);
        newProc.Attributes = new QKeyValue(Token.NoToken, "tag",
          new List<object>() { thread.Name }, func.Proc.Attributes);
      }

      if (func.Name.Equals(thread.Name))
      {
        thread.Function = newImpl;
      }

      this.AC.TopLevelDeclarations.Add(newProc);
      this.AC.TopLevelDeclarations.Add(newImpl);

      this.DuplicatedFunctions.Add(func.Name);

      return newImpl;
    }

    private void CleanUp()
    {
      foreach (var func in this.DuplicatedFunctions)
      {
        ModelCleaner.Remove(this.AC, func);
      }
    }
  }
}
