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
using System.ComponentModel.Design.Serialization;

using Microsoft.Boogie;
using Microsoft.Basetypes;

using Lockpwn.IO;

namespace Lockpwn.Refactoring
{
  internal class ProgramSimplifier : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    internal ProgramSimplifier(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    /// <summary>
    /// Runs a program simplification pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... ProgramSimplifier");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>().ToList())
      {
        this.SimplifyImplementation(impl);
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void SimplifyImplementation(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        if (ToolCommandLineOptions.Get().DisableUserAssertions)
        {
          block.Cmds.RemoveAll(val => val is AssertCmd);
        }

        this.RemoveDomainSpecificFunctions(block);

        foreach (var call in block.cmds.OfType<CallCmd>().Where(val => val.IsAsync))
        {
          call.IsAsync = false;
        }
      }
    }

    private void RemoveDomainSpecificFunctions(Block block)
    {
      var toRemove = new List<Cmd>();
      foreach (var call in block.cmds.OfType<CallCmd>())
      {
        if (call.callee.Equals("pthread_exit"))
          toRemove.Add(call);
      }

      block.Cmds.RemoveAll(val => toRemove.Contains(val));
    }
  }
}
