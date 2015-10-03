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
  internal class LockAbstraction : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    internal LockAbstraction(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    /// <summary>
    /// Runs a lock abstraction pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... LockAbstraction");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.IdentifyAndCreateUniqueLocks();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    /// <summary>
    /// Performs an analysis to identify and create unique locks.
    /// </summary>
    private void IdentifyAndCreateUniqueLocks()
    {
      foreach (var block in this.AC.EntryPoint.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;
          if (!(block.Cmds[idx] as CallCmd).callee.Contains("pthread_mutex_init"))
            continue;

          Expr lockExpr = PointerArithmeticAnalyser.ComputeRootPointer(this.AC.EntryPoint,
            block.Label, (block.Cmds[idx] as CallCmd).Ins[0]);

          Lock newLock = new Lock(new Constant(Token.NoToken,
            new TypedIdent(Token.NoToken, "lock$" + this.AC.Locks.Count,
              Microsoft.Boogie.Type.Int), true), lockExpr);

          if (ToolCommandLineOptions.Get().SuperVerboseMode)
            Output.PrintLine("..... New abstract lock '{0}'", newLock.Id);

          newLock.Id.AddAttribute("lock", new object[] { });
          this.AC.TopLevelDeclarations.Add(newLock.Id);
          this.AC.Locks.Add(newLock);
        }
      }

      if (ToolCommandLineOptions.Get().SuperVerboseMode &&
        this.AC.Locks.Count == 0)
      {
        Output.PrintLine("..... No locks detected");
      }
    }
  }
}
