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

namespace Lockpwn.Instrumentation
{
  internal class GlobalRaceCheckingInstrumentation : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    internal GlobalRaceCheckingInstrumentation(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... GlobalRaceCheckingInstrumentation");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var thread in this.AC.Threads)
      {
        this.AddCurrentLocksets(thread);
        this.AddMemoryLocksets(thread);
        this.AddAccessCheckingVariables(thread);

        if (ToolCommandLineOptions.Get().SuperVerboseMode)
          Output.PrintLine("..... Instrumented lockset analysis globals for '{0}'", thread.Name);
      }

      this.AddAccessWatchdogConstants();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void AddCurrentLocksets(Thread thread)
    {
      foreach (var l in this.AC.GetLockVariables())
      {
        var ls = new GlobalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
          l.Name + "_in_CLS_$" + thread.Name, Microsoft.Boogie.Type.Bool));
        ls.AddAttribute("current_lockset", new object[] { });
        this.AC.TopLevelDeclarations.Add(ls);
        this.AC.CurrentLocksets.Add(new Lockset(ls, l, thread));
      }
    }

    private void AddMemoryLocksets(Thread thread)
    {
      foreach (var mr in this.AC.ThreadMemoryRegions[thread])
      {
        foreach (var l in this.AC.GetLockVariables())
        {
          var ls = new GlobalVariable(Token.NoToken, new TypedIdent(Token.NoToken,
            l.Name + "_in_LS_" + mr.Name + "_$" + thread.Name, Microsoft.Boogie.Type.Bool));
          ls.AddAttribute("lockset", new object[] { });
          this.AC.TopLevelDeclarations.Add(ls);
          this.AC.MemoryLocksets.Add(new Lockset(ls, l, thread, mr.Name));
        }
      }
    }

    private void AddAccessCheckingVariables(Thread thread)
    {
      foreach (var mr in this.AC.ThreadMemoryRegions[thread])
      {
        var wavar = new GlobalVariable(Token.NoToken,
          new TypedIdent(Token.NoToken, "WRITTEN_" + mr.Name +
            "_$" + thread.Name, Microsoft.Boogie.Type.Bool));
        var ravar = new GlobalVariable(Token.NoToken,
          new TypedIdent(Token.NoToken, "READ_" + mr.Name +
            "_$" + thread.Name, Microsoft.Boogie.Type.Bool));

        wavar.AddAttribute("access_checking", new object[] { });
        ravar.AddAttribute("access_checking", new object[] { });

        if (!this.AC.TopLevelDeclarations.OfType<Variable>().Any(val => val.Name.Equals(wavar.Name)))
          this.AC.TopLevelDeclarations.Add(wavar);
        if (!this.AC.TopLevelDeclarations.OfType<Variable>().Any(val => val.Name.Equals(ravar.Name)))
          this.AC.TopLevelDeclarations.Add(ravar);
      }
    }

    private void AddAccessWatchdogConstants()
    {
      foreach (var mr in this.AC.SharedMemoryRegions)
      {
        var ti = new TypedIdent(Token.NoToken, "WATCHED_ACCESS_" + mr.Name,
          this.AC.MemoryModelType);
        var watchdog = new Constant(Token.NoToken, ti, false);
        watchdog.AddAttribute("watchdog", new object[] { });

        if (!this.AC.TopLevelDeclarations.OfType<Variable>().Any(val => val.Name.Equals(watchdog.Name)))
          this.AC.TopLevelDeclarations.Add(watchdog);
      }
    }
  }
}
