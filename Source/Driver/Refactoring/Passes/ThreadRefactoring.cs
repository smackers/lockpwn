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

    private HashSet<string> AlreadyAnalysedFunctions;
    private HashSet<string> DuplicatedFunctions;

    private Dictionary<Implementation, HashSet<Block>> ToRemoveBlocks;
    private Dictionary<Implementation, Dictionary<Block, HashSet<Cmd>>> ToRemoveCmds;

    public ThreadRefactoring(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;

      this.AlreadyAnalysedFunctions = new HashSet<string>();
      this.DuplicatedFunctions = new HashSet<string>();

      this.ToRemoveBlocks = new Dictionary<Implementation, HashSet<Block>>();
      this.ToRemoveCmds = new Dictionary<Implementation, Dictionary<Block, HashSet<Cmd>>>();
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
        this.AlreadyAnalysedFunctions.Clear();

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
      string name = impl.Name;
      if (tid != null &&
        (this.AC.Threads.Any(val => val.Name.Equals(impl.Name)) ||
          impl.Name.StartsWith("__call_wrapper") ||
          impl.Name.StartsWith("pthread_create")))
      {
        name = impl.Name + "$" + tid.Name;
      }

      if (this.AlreadyAnalysedFunctions.Contains(name))
        return;
      this.AlreadyAnalysedFunctions.Add(name);

      if (!this.AC.MainThread.Name.Equals(impl.Name) &&
        !(this.AC.Threads.Any(val => val.Name.Equals(impl.Name)) &&
          !thread.Name.Equals(impl.Name)) &&
        ((Utilities.ShouldAccessFunction(impl.Name) &&
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

      if (!Utilities.ShouldAccessFunction(callee.Name))
        return;
      if (Utilities.ShouldSkipFromAnalysis(callee.Name) &&
        !callee.Name.StartsWith("__call_wrapper") &&
        !callee.Name.StartsWith("pthread_create"))
        return;

      if (this.AC.Threads.Any(val => val.Name.Equals(cmd.callee)))
      {
        thread = this.AC.Threads.First(val => !val.IsMain && val.Id.IsEqual(tid));
        cmd.callee = thread.Name + "$" + thread.Id;
        return;
      }
      else if (cmd.callee.StartsWith("pthread_create"))
      {
        tid = cmd.Ins[0] as IdentifierExpr;
        var t = this.AC.Threads.First(val => !val.IsMain && val.Id.IsEqual(tid));
        cmd.callee = cmd.callee + "$" + tid.Name;
        cmd.Ins[2] = new IdentifierExpr(Token.NoToken, t.Name + "$" + t.Id, Microsoft.Boogie.Type.Int);
      }
      else if (cmd.callee.StartsWith("__call_wrapper"))
      {
        cmd.callee = cmd.callee + "$" + tid.Name;
      }
      else
      {
        cmd.callee = cmd.callee + "$" + thread.Name;
      }

      this.ParseAndRenameNestedFunctions(thread, callee, tid);

      foreach (var expr in cmd.Ins)
      {
        if (!(expr is IdentifierExpr)) continue;
        callee = this.AC.GetImplementation((expr as IdentifierExpr).Name);

        if (callee != null && Utilities.ShouldAccessFunction(callee.Name))
        {
          this.ParseAndRenameNestedFunctions(thread, callee, tid);
          (expr as IdentifierExpr).Name = (expr as IdentifierExpr).Name + "$" + thread.Name;
        }
      }
    }

    private void ParseAndRenameFunctionsInAssign(Thread thread, Implementation impl, Block block,
      AssignCmd cmd, IdentifierExpr tid)
    {
      foreach (var rhs in cmd.Rhss)
      {
        if (!(rhs is IdentifierExpr)) continue;
        var callee = this.AC.GetImplementation((rhs as IdentifierExpr).Name);

        if (callee != null && Utilities.ShouldAccessFunction(callee.Name))
        {
          if (this.AC.IsAToolFunc(callee.Name))
            continue;
          if (!Utilities.ShouldAccessFunction(callee.Name))
            continue;
          if (Utilities.ShouldSkipFromAnalysis(callee.Name))
            return;

          if (this.AC.Threads.Any(val => val.Name.Equals((rhs as IdentifierExpr).Name)))
          {
            var t = this.AC.Threads.First(val => val.Name.Equals(
              (rhs as IdentifierExpr).Name) && val.Id.IsEqual(tid));
            (rhs as IdentifierExpr).Name = t.Name + "$" + t.Id;
            return;
          }

          this.ParseAndRenameNestedFunctions(thread, callee, tid);
          (rhs as IdentifierExpr).Name = (rhs as IdentifierExpr).Name + "$" + thread.Name;
        }
      }
    }

    private void ParseAndRenameFunctionsInAssume(Thread thread, Implementation impl, Block block,
      AssumeCmd cmd, IdentifierExpr tid)
    {
      if (cmd.Expr is NAryExpr)
      {
        foreach (var expr in (cmd.Expr as NAryExpr).Args)
        {
          if (!(expr is IdentifierExpr)) continue;
          var callee = this.AC.GetImplementation((expr as IdentifierExpr).Name);

          if (callee != null && Utilities.ShouldAccessFunction(callee.Name))
          {
            if (this.AC.IsAToolFunc(callee.Name))
              continue;
            if (!Utilities.ShouldAccessFunction(callee.Name))
              continue;
            if (Utilities.ShouldSkipFromAnalysis(callee.Name))
              return;

            if (this.AC.Threads.Any(val => val.Name.Equals((expr as IdentifierExpr).Name)))
            {
              var t = this.AC.Threads.FirstOrDefault(val => val.Name.Equals(
                (expr as IdentifierExpr).Name) && val.Id.IsEqual(tid));
              if (t == null)
              {
                if (impl.Name.StartsWith("__call_wrapper"))
                {
                  if (!this.ToRemoveBlocks.ContainsKey(impl))
                    this.ToRemoveBlocks.Add(impl, new HashSet<Block>());
                  this.ToRemoveBlocks[impl].Add(block);
                }

                return;
              }
              if (impl.Name.StartsWith("__call_wrapper"))
              {
                if (!this.ToRemoveCmds.ContainsKey(impl))
                  this.ToRemoveCmds.Add(impl, new Dictionary<Block, HashSet<Cmd>>());
                if (!this.ToRemoveCmds[impl].ContainsKey(block))
                  this.ToRemoveCmds[impl].Add(block, new HashSet<Cmd>());
                this.ToRemoveCmds[impl][block].Add(cmd);
              }
              else
              {
                (expr as IdentifierExpr).Name = t.Name + "$" + t.Id;
              }

              return;
            }

            this.ParseAndRenameNestedFunctions(thread, callee, tid);
            (expr as IdentifierExpr).Name = (expr as IdentifierExpr).Name + "$" + thread.Name;
          }
        }
      }
    }

    private Implementation DuplicateFunction(Thread thread, Implementation func, IdentifierExpr tid)
    {
      string suffix = "";
      if (this.AC.Threads.Any(val => val.Name.Equals(func.Name)))
      {
        suffix = "$" + thread.Id.Name;
      }
      else if (func.Name.StartsWith("__call_wrapper") ||
        func.Name.StartsWith("pthread_create"))
      {
        suffix = "$" + tid;
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

      if (this.AC.Threads.Any(val => val.Name.Equals(func.Name)))
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

      foreach (var func in this.ToRemoveBlocks)
      {
        var removedLabels = new HashSet<string>();
        foreach (var block in func.Value)
        {
          removedLabels.Add(block.Label);
          func.Key.Blocks.Remove(block);
        }

        foreach (var block in func.Key.Blocks)
        {
          if (!(block.TransferCmd is GotoCmd))
            continue;

          var transfer = block.TransferCmd as GotoCmd;
          transfer.labelNames.RemoveAll(val => removedLabels.Contains(val));
          transfer.labelTargets.RemoveAll(val => func.Value.Contains(val));
        }
      }

      foreach (var func in this.ToRemoveCmds)
      {
        foreach (var block in func.Value)
        {
          block.Key.Cmds.RemoveAll(val => block.Value.Contains(val));
        }
      }
    }
  }
}
