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
  internal class ThreadUsageAnalysis : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    internal ThreadUsageAnalysis(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    /// <summary>
    /// Runs a thread usage analysis pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... ThreadUsageAnalysis");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.CreateMainThread();
      this.IdentifyThreadCreation();
      this.IdentifyThreadJoin();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    /// <summary>
    /// Creates main thread.
    /// </summary>
    private void CreateMainThread()
    {
      var thread = Thread.CreateMain(this.AC);

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
        Output.PrintLine("..... '{0}' is the main thread", thread.Name);
    }

    /// <summary>
    /// Performs an analysis to identify thread creation.
    /// </summary>
    private void IdentifyThreadCreation()
    {
      var currentThread = this.AC.MainThread;

      foreach (var block in currentThread.Function.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if (!(block.Cmds[idx] as CallCmd).callee.Contains("pthread_create"))
            continue;

          var threadName = (call.Ins[2] as IdentifierExpr).Name;
          var thread = Thread.Create(this.AC, threadName, call.Ins[0], call.Ins[3], currentThread);

          currentThread.AddChild(thread);
        }
      }

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        foreach (var child in currentThread.Children)
        {
          Output.PrintLine("..... '{0}' spawns thread '{1}'", currentThread.Name, child.Name);
        }
      }
    }

    /// <summary>
    /// Performs an analysis to identify thread join.
    /// </summary>
    private void IdentifyThreadJoin()
    {
      var currentThread = this.AC.MainThread;

      foreach (var block in currentThread.Function.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if (!(block.Cmds[idx] as CallCmd).callee.Contains("pthread_join"))
            continue;

          var threadIdExpr = PointerArithmeticAnalyser.ComputeRootPointer(
            currentThread.Function, block.Label, call.Ins[0], true);
          if (threadIdExpr is NAryExpr)
          {
            var nary = threadIdExpr as NAryExpr;
            if (nary.Fun is MapSelect && nary.Args.Count == 2)
            {
              threadIdExpr = nary.Args[1];
            }
          }

          if (!(threadIdExpr is IdentifierExpr))
            continue;

          var thread = currentThread.Children.FirstOrDefault(val =>
            val.Id.Name.Equals((threadIdExpr as IdentifierExpr).Name));
          if (thread == null)
            continue;

          thread.Joiner = new Tuple<Implementation, Block, CallCmd>(currentThread.Function, block, call);

          if (ToolCommandLineOptions.Get().SuperVerboseMode)
            Output.PrintLine("..... '{0}' blocks on thread '{1}'",
              currentThread.Name, thread.Name);
        }
      }
    }
  }
}
