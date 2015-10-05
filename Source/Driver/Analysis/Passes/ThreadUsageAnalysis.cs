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
      this.IdentifyThreadCreationInThread(this.AC.MainThread);
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
    /// <param name="parent">Thread</param>
    private void IdentifyThreadCreationInThread(Thread parent)
    {
      this.IdentifyThreadCreationInImplementation(parent, parent.Function);

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        foreach (var child in parent.Children)
        {
          Output.PrintLine("..... '{0}' spawns thread '{1}'", parent.Name, child.Name);
        }
      }
    }

    /// <summary>
    /// Performs an analysis to identify thread creation.
    /// </summary>
    /// <param name="parent">Thread</param>
    /// <param name="impl">Implementation</param>
    private void IdentifyThreadCreationInImplementation(Thread parent, Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if ((block.Cmds[idx] as CallCmd).callee.Contains("pthread_create"))
          {
            var threadName = (call.Ins[2] as IdentifierExpr).Name;
            var thread = Thread.Create(this.AC, threadName, call.Ins[0], call.Ins[3], parent);

            parent.AddChild(thread);
          }
          else if (!Utilities.ShouldSkipFromAnalysis(call.callee))
          {
            var calleeImpl = this.AC.GetImplementation(call.callee);
            if (calleeImpl == null)
              continue;

            this.IdentifyThreadCreationInImplementation(parent, calleeImpl);
          }
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
