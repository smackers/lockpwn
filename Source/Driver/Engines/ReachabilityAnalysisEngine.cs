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
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Microsoft.Boogie;

using Lockpwn.IO;

namespace Lockpwn
{
  internal sealed class ReachabilityAnalysisEngine
  {
    private Program Program;
    private ExecutionTimer Timer;

    public ReachabilityAnalysisEngine(Program program)
    {
      Contract.Requires(program != null);
      this.Program = program;
    }

    public void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". ReachabilityAnalysis");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      if (ToolCommandLineOptions.Get().SkipSummarization)
      {
        this.ParseFreshProgram("instrumented");
      }
      else
      {
        this.ParseFreshProgram("summarised");
      }

      Analysis.Factory.CreateRaceCheckAnalysis(this.Program.AC).Run();
      Instrumentation.Factory.CreateYieldInstrumentation(this.Program.PostAC).Run();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("... ReachabilityAnalysis done [{0}]", this.Timer.Result());
      }

      Lockpwn.IO.BoogieProgramEmitter.EmitOutput(this.Program.PostAC.TopLevelDeclarations,
        ToolCommandLineOptions.Get().Files[ToolCommandLineOptions.Get().Files.Count - 1]);
    }

    /// <summary>
    /// Parses a fresh program.
    /// </summary>
    /// <param name="suffix">Suffix</param>
    private void ParseFreshProgram(string suffix)
    {
      new AnalysisContextParser(this.Program.FileList[this.Program.FileList.Count - 1], "bpl")
        .TryParseNew(ref this.Program.AC, new List<string> { suffix });
      new AnalysisContextParser(this.Program.FileList[this.Program.FileList.Count - 1], "bpl")
        .TryParseNew(ref this.Program.PostAC, new List<string> { suffix });
    }
  }
}
