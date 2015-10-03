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
    private AnalysisContext AC;
    private AnalysisContext PostAC;
    private ExecutionTimer Timer;

    public ReachabilityAnalysisEngine(AnalysisContext ac, AnalysisContext postAc)
    {
      Contract.Requires(ac != null && postAc != null);
      this.AC = ac;
      this.PostAC = postAc;
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

      Analysis.Factory.CreateRaceCheckAnalysis(this.AC).Run();
      Instrumentation.Factory.CreateYieldInstrumentation(this.PostAC).Run();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("... ReachabilityAnalysis done [{0}]", this.Timer.Result());
      }

      Lockpwn.IO.BoogieProgramEmitter.EmitOutput(this.PostAC.TopLevelDeclarations, ToolCommandLineOptions.
        Get().Files[ToolCommandLineOptions.Get().Files.Count - 1]);
    }
  }
}
