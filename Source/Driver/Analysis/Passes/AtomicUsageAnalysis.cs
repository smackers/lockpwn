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
  internal class AtomicRegionsAnalysis : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    private bool UsesAtomicLock;

    internal AtomicRegionsAnalysis(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;

      this.UsesAtomicLock = false;
    }

    /// <summary>
    /// Runs an atomic regions usage pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... AtomicRegionsAnalysis");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>().ToList())
      {
        this.IdentifyUseOfAtomicsInImplementation(impl);
        if (this.UsesAtomicLock && !ToolCommandLineOptions.Get().SuperVerboseMode)
        {
          break;
        }
      }

      if (this.UsesAtomicLock)
      {
        this.CreateAtomicLock();
      }

      if (ToolCommandLineOptions.Get().SuperVerboseMode && !this.UsesAtomicLock)
      {
        Output.PrintLine("..... No use of atomics detected");
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    /// <summary>
    /// Performs an analysis to identify use of atomics.
    /// </summary>
    /// <param name="impl">Implementation</param>
    private void IdentifyUseOfAtomicsInImplementation(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        foreach (var cmd in block.Cmds)
        {
          if (cmd is CallCmd)
          {
            CallCmd call = cmd as CallCmd;
            if (call.callee.Equals("corral_atomic_begin") ||
                call.callee.Equals("corral_atomic_end"))
            {
              this.UsesAtomicLock = true;

              if (ToolCommandLineOptions.Get().SuperVerboseMode)
              {
                Output.PrintLine("..... {0} uses atomic lock 'lock$atomic'", impl.Name);
              }
            }
          }
        }
      }
    }

    /// <summary>
    /// Creates the atomic lock.
    /// </summary>
    /// <returns>Lock</returns>
    private Lock CreateAtomicLock()
    {
      Lock l = new Lock(new Constant(Token.NoToken,
        new TypedIdent(Token.NoToken, "lock$atomic",
          Microsoft.Boogie.Type.Int), true));

      l.Id.AddAttribute("lock", new object[] { });
      this.AC.TopLevelDeclarations.Add(l.Id);
      this.AC.Locks.Add(l);

      return l;
    }
  }
}
