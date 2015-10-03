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

using Lockpwn.IO;

namespace Lockpwn
{
  internal sealed class ThreadAnalysisEngine
  {
    private Program Program;
    private ExecutionTimer Timer;

    internal ThreadAnalysisEngine(Program program)
    {
      Contract.Requires(program != null);
      this.Program = program;
    }

    internal void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". ThreadAnalysis");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      Analysis.Factory.CreateThreadCreationAnalysis(this.Program.AC).Run();
      Analysis.Factory.CreateLockAbstraction(this.Program.AC).Run();

      if (this.Program.AC.Locks.Count > 0)
      {
        Refactoring.Factory.CreateLockRefactoring(this.Program.AC).Run();
      }

      Refactoring.Factory.CreateThreadRefactoring(this.Program.AC).Run();

      Analysis.Factory.CreateSharedStateAnalysis(this.Program.AC).Run();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("... ThreadAnalysis done [{0}]", this.Timer.Result());
      }
    }
  }
}
