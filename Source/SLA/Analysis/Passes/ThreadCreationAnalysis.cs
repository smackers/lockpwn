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

namespace Lockpwn.Analysis
{
  internal class ThreadCreationAnalysis : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    internal ThreadCreationAnalysis(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    /// <summary>
    /// Runs a thread creation analysis pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Console.WriteLine("... ThreadCreationAnalysis");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.CreateMainThread();
      this.IdentifyThreadCreation();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Console.WriteLine("..... [{0}]", this.Timer.Result());
      }
    }

    /// <summary>
    /// Creates main thread.
    /// </summary>
    private void CreateMainThread()
    {
      var thread = new Thread(this.AC);
      this.AC.Threads.Add(thread);

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
        Console.WriteLine("..... '{0}' is the main thread", thread.Name);
    }

    /// <summary>
    /// Performs an analysis to identify thread creation.
    /// </summary>
    private void IdentifyThreadCreation()
    {
      foreach (var block in this.AC.EntryPoint.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if (!(block.Cmds[idx] as CallCmd).callee.Contains("pthread_create"))
            continue;

          var thread = new Thread(this.AC, call.Ins[0], call.Ins[2], call.Ins[3], this.AC.EntryPoint);
          this.AC.Threads.Add(thread);

          if (ToolCommandLineOptions.Get().SuperVerboseMode)
            Console.WriteLine("..... '{0}' spawns new thread '{1}'",
              this.AC.EntryPoint.Name, thread.Name);
        }
      }
    }
  }
}
