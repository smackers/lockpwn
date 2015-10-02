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

using Lockpwn.IO;

namespace Lockpwn.Refactoring
{
  internal class ThreadRefactoring : IPass
  {
    private AnalysisContext AC;
    private Thread Thread;
    private ExecutionTimer Timer;

    private HashSet<Implementation> AlreadyAnalysedFunctions;

    public ThreadRefactoring(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.AlreadyAnalysedFunctions = new HashSet<Implementation>();
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
        if (thread.IsMain)
          continue;

        this.Thread = thread;
        this.RefactorEntryPointAttributes();
        this.ParseAndRenameNestedFunctions(this.Thread.Function);
        this.AlreadyAnalysedFunctions.Clear();

        if (ToolCommandLineOptions.Get().SuperVerboseMode)
          Output.PrintLine("..... Separated call graph of '{0}'", thread.Name);
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void RefactorEntryPointAttributes()
    {
      this.Thread.Function.Proc.Attributes = new QKeyValue(Token.NoToken,
        "thread", new List<object>(), null);
      this.Thread.Function.Attributes = new QKeyValue(Token.NoToken,
        "thread", new List<object>(), null);
    }

    private void ParseAndRenameNestedFunctions(Implementation impl)
    {
      if (this.AlreadyAnalysedFunctions.Contains(impl))
        return;
      this.AlreadyAnalysedFunctions.Add(impl);

      if (!impl.Equals(this.Thread.Function))
      {
        this.DuplicateFunction(impl);
      }

      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          if (cmd is CallCmd)
          {
            this.ParseAndRenameFunctionsInCall(cmd as CallCmd);
          }
          else if (cmd is AssignCmd)
          {
            this.ParseAndRenameFunctionsInAssign(cmd as AssignCmd);
          }
          else if (cmd is AssumeCmd)
          {
            this.ParseAndRenameFunctionsInAssume(cmd as AssumeCmd);
          }
        }
      }
    }

    private void ParseAndRenameFunctionsInCall(CallCmd cmd)
    {
      var impl = this.AC.GetImplementation(cmd.callee);

      if (impl == null || this.AC.IsAToolFunc(impl.Name))
        return;
      if (!Utilities.ShouldAccessFunction(impl.Name))
        return;
      if (Utilities.ShouldSkipFromAnalysis(impl.Name))
        return;

      this.ParseAndRenameNestedFunctions(impl);
      cmd.callee = cmd.callee + "$" + this.Thread.Name;

      foreach (var expr in cmd.Ins)
      {
        if (!(expr is IdentifierExpr)) continue;
        impl = this.AC.GetImplementation((expr as IdentifierExpr).Name);

        if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
        {
          this.ParseAndRenameNestedFunctions(impl);
          (expr as IdentifierExpr).Name = (expr as IdentifierExpr).Name + "$" + this.Thread.Name;
        }
      }
    }

    private void ParseAndRenameFunctionsInAssign(AssignCmd cmd)
    {
      foreach (var rhs in cmd.Rhss)
      {
        if (!(rhs is IdentifierExpr)) continue;
        var impl = this.AC.GetImplementation((rhs as IdentifierExpr).Name);

        if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
        {
          if (this.AC.IsAToolFunc(impl.Name))
            continue;
          if (!Utilities.ShouldAccessFunction(impl.Name))
            continue;
          if (Utilities.ShouldSkipFromAnalysis(impl.Name))
            continue;

          this.ParseAndRenameNestedFunctions(impl);
          (rhs as IdentifierExpr).Name = (rhs as IdentifierExpr).Name + "$" + this.Thread.Name;
        }
      }
    }

    private void ParseAndRenameFunctionsInAssume(AssumeCmd cmd)
    {
      if (cmd.Expr is NAryExpr)
      {
        foreach (var expr in (cmd.Expr as NAryExpr).Args)
        {
          if (!(expr is IdentifierExpr)) continue;
          var impl = this.AC.GetImplementation((expr as IdentifierExpr).Name);

          if (impl != null && Utilities.ShouldAccessFunction(impl.Name))
          {
            if (this.AC.IsAToolFunc(impl.Name))
              continue;
            if (!Utilities.ShouldAccessFunction(impl.Name))
              continue;
            if (Utilities.ShouldSkipFromAnalysis(impl.Name))
              continue;

            this.ParseAndRenameNestedFunctions(impl);
            (expr as IdentifierExpr).Name = (expr as IdentifierExpr).Name + "$" + this.Thread.Name;
          }
        }
      }
    }

    private void DuplicateFunction(Implementation func)
    {
      var cons = this.AC.GetConstant(func.Name);
      if (cons != null)
      {
        var consName = cons.Name + "$" + this.Thread.Name;
        var newCons = new Constant(cons.tok,
          new TypedIdent(cons.TypedIdent.tok, consName,
            cons.TypedIdent.Type), cons.Unique);
        this.AC.TopLevelDeclarations.Add(newCons);
      }

      var newProc = new Duplicator().Visit(func.Proc.Clone()) as Procedure;
      var newImpl = new Duplicator().Visit(func.Clone()) as Implementation;

      newProc.Name = func.Proc.Name + "$" + this.Thread.Name;
      newImpl.Name = func.Name + "$" + this.Thread.Name;
      newImpl.Proc = newProc;

      newImpl.Attributes = new QKeyValue(Token.NoToken, "tag",
        new List<object>() { this.Thread.Name }, func.Attributes);
      newProc.Attributes = new QKeyValue(Token.NoToken, "tag",
        new List<object>() { this.Thread.Name }, func.Proc.Attributes);

      this.AC.TopLevelDeclarations.Add(newProc);
      this.AC.TopLevelDeclarations.Add(newImpl);
    }
  }
}
