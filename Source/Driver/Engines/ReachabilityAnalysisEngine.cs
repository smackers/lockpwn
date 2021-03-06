﻿//===-----------------------------------------------------------------------==//
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
  internal sealed class ReachabilityAnalysisEngine : AbstractEngine
  {
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="program">Program</param>
    internal ReachabilityAnalysisEngine(Program program)
      : base(program)
    { }

    /// <summary>
    /// Starts the engine.
    /// </summary>
    internal override void Start()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
      {
        Output.PrintLine(". ReachabilityAnalysis");
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        base.Timer.Start();
      }

      string suffix;
      if (!ToolCommandLineOptions.Get().RequiresInvariantInference ||
        ToolCommandLineOptions.Get().SkipSummarization)
      {
        suffix = "sequentialized";
      }
      else
      {
        suffix = "summarised";
      }

      base.Program.AC = base.ParseContextFromFile(suffix);

      Analysis.Factory.CreateRaceCheckAnalysis(base.Program.AC).Run();

      if (ToolCommandLineOptions.Get().EnableCorralMode)
      {
        var originalAnalysisContext = base.ParseContextFromInputFile();
        Instrumentation.Factory.CreateYieldInstrumentation(originalAnalysisContext).Run();

        if (ToolCommandLineOptions.Get().OutputFile.Length > 0)
        {
          base.EmitProgramContext(originalAnalysisContext);
        }
        else
        {
          base.EmitProgramContext(originalAnalysisContext, "corral");
        }
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        base.Timer.Stop();
        Output.PrintLine("... ReachabilityAnalysis done [{0}]", base.Timer.Result());
      }
    }
  }
}
