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

      this.InstrumentYieldInPThreadCreate();
      this.InstrumentYieldInCallWrapperStart();
      this.InstrumentYieldInThreadStart();

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>().ToList())
      {
        if (this.AC.IsAToolFunc(impl.Name))
          continue;
        if (!Utilities.ShouldAccessFunction(impl.Name))
          continue;
        if (Utilities.ShouldSkipFromAnalysis(impl.Name))
          continue;
        
        this.InstrumentYieldInLocks(impl);
        this.InstrumentYieldInUnlocks(impl);
        this.InstrumentYieldInMemoryAccesses(impl);
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

    private void InstrumentYieldInPThreadCreate()
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

    // HACK make this start of async call
    private void InstrumentYieldInCallWrapperStart()
    {
      var impl = this.AC.GetImplementation("__call_wrapper");
      var firstBlock = impl.Blocks[0];
      firstBlock.Cmds.Insert(0, new YieldCmd(Token.NoToken));
      this.YieldCounter++;
    }

    private void InstrumentYieldInThreadStart()
    {
      foreach (var impl in this.AC.GetThreadFunctions())
      {
        var firstBlock = impl.Blocks[0];
        firstBlock.Cmds.Insert(0, new YieldCmd(Token.NoToken));
        this.YieldCounter++;
      }
    }

    private void InstrumentYieldInLocks(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if (!call.callee.Equals("pthread_mutex_lock") &&
            !call.callee.Equals("corral_atomic_begin"))
            continue;

          block.Cmds.Insert(idx, new YieldCmd(Token.NoToken));
          idx++;

          this.YieldCounter++;
        }
      }
    }

    private void InstrumentYieldInUnlocks(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is CallCmd))
            continue;

          var call = block.Cmds[idx] as CallCmd;
          if (!call.callee.Equals("pthread_mutex_unlock") &&
            !call.callee.Equals("corral_atomic_end"))
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

    private void InstrumentYieldInMemoryAccesses(Implementation impl)
    {
      foreach (var block in impl.Blocks)
      {
        for (int idx = 0; idx < block.Cmds.Count; idx++)
        {
          if (!(block.Cmds[idx] is AssignCmd)) continue;
          var assign = block.Cmds[idx] as AssignCmd;

          var lhss = assign.Lhss.OfType<SimpleAssignLhs>();
          var rhssMap = assign.Rhss.OfType<NAryExpr>();
          var rhss = assign.Rhss.OfType<IdentifierExpr>();

          bool writeAccessFound = false;
          bool readAccessFound = false;

          var resource = "";

          if (lhss.Count() == 1)
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
            if (rhs.Fun.FunctionName.StartsWith("$load.") && rhs.Args.Count == 2 &&
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
