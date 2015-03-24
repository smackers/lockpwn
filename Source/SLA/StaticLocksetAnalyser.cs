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
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Microsoft.Boogie;

namespace Lockpwn
{
  internal sealed class StaticLocksetAnalyser
  {
    AnalysisContext AC;
    private AnalysisContext PostAC;
    private ExecutionTimer Timer;

    public StaticLocksetAnalyser(AnalysisContext ac, AnalysisContext postAc)
    {
      Contract.Requires(ac != null && postAc != null);
      this.AC = ac;
      this.PostAC = postAc;
    }

    public void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Console.WriteLine(". StaticLocksetAnalysis");

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
        Console.WriteLine("... StaticLocksetAnalysis done [{0}]", this.Timer.Result());
      }

      Lockpwn.IO.BoogieProgramEmitter.EmitOutput(this.PostAC.TopLevelDeclarations, ToolCommandLineOptions.
        Get().Files[ToolCommandLineOptions.Get().Files.Count - 1]);
    }
  }
}
