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

using Lockpwn.Analysis;
using Lockpwn.IO;

namespace Lockpwn.Instrumentation
{
  internal class YieldInstrumentation : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    private int YieldCounter;

    public YieldInstrumentation(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.YieldCounter = 0;
    }

    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... YieldInstrumentation");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.InstrumentYieldsInPThreadCreate();

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>().ToList())
      {
        if (this.AC.IsAToolFunc(impl.Name))
          continue;
        if (!Utilities.ShouldAccessFunction(impl.Name))
          continue;
        if (Utilities.ShouldSkipFromAnalysis(impl.Name))
          continue;
        
        this.InstrumentYieldsInLocks(impl);
        this.InstrumentYieldsInMemoryAccesses(impl);
      }

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        var suffix = this.YieldCounter == 1 ? "" : "s";
        Output.PrintLine("..... Instrumented '{0}' yield" + suffix + "", this.YieldCounter);
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    #region yield instrumentation

    private void InstrumentYieldsInPThreadCreate()
    {
      var impl = this.AC.GetImplementation("pthread_create");
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if (!call.IsAsync)
            continue;

          if (block.Cmds.Count == idx + 1)
            block.Cmds.Add(new YieldCmd(Token.NoToken));
          else
            block.Cmds.Insert(idx + 1, new YieldCmd(Token.NoToken));
          idx++;

          this.YieldCounter++;
        }
      }
    }

    private void InstrumentYieldsInLocks(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if (!call.callee.Equals("pthread_mutex_lock") &&
              !call.callee.Equals("pthread_mutex_unlock"))
            continue;

          block.Cmds.Insert(idx, new YieldCmd(Token.NoToken));
          idx++;

          this.YieldCounter++;
        }
      }
    }

    private void InstrumentYieldsInMemoryAccesses(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is AssignCmd)) continue;
          var assign = block.Cmds[idx] as AssignCmd;

          var lhssMap = assign.Lhss.OfType<MapAssignLhs>();
          var lhss = assign.Lhss.OfType<SimpleAssignLhs>();
          var rhssMap = assign.Rhss.OfType<NAryExpr>();
          var rhss = assign.Rhss.OfType<IdentifierExpr>();

          bool writeAccessFound = false;
          bool readAccessFound = false;

          var resource = "";

          if (lhssMap.Count() == 1)
          {
            var lhs = lhssMap.First();
            if (lhs.DeepAssignedIdentifier.Name.StartsWith("$M.") &&
              lhs.Map is SimpleAssignLhs && lhs.Indexes.Count == 1)
            {
              writeAccessFound = true;
              resource = lhs.DeepAssignedIdentifier.Name;
            }
          }
          else if (lhss.Count() == 1)
          {
            var lhs = lhss.First();
            if (lhs.DeepAssignedIdentifier.Name.StartsWith("$M."))
            {
              writeAccessFound = true;
              resource = lhs.DeepAssignedIdentifier.Name;
            }
          }

          if (rhssMap.Count() == 1)
          {
            var rhs = rhssMap.First();
            if (rhs.Fun is MapSelect && rhs.Args.Count == 2 &&
              (rhs.Args[0] as IdentifierExpr).Name.StartsWith("$M."))
            {
              readAccessFound = true;
              resource = (rhs.Args[0] as IdentifierExpr).Name;
            }
          }
          else if (rhss.Count() == 1)
          {
            var rhs = rhss.First();
            if (rhs.Name.StartsWith("$M."))
            {
              readAccessFound = true;
              resource = rhs.Name;
            }
          }

          if (!writeAccessFound && !readAccessFound)
            continue;
          if (!this.AC.GetErrorReporter().UnprotectedResources.Contains(resource))
            continue;

          if (block.Cmds.Count == idx + 1)
            block.Cmds.Add(new YieldCmd(Token.NoToken));
          else
            block.Cmds.Insert(idx + 1, new YieldCmd(Token.NoToken));
          idx++;

          this.YieldCounter++;
        }
      }
    }

    #endregion
  }
}
