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

namespace Lockpwn
{
  internal sealed class SummarizationEngine : AbstractEngine
  {
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="program">Program</param>
    internal SummarizationEngine(Program program)
      : base(program)
    { }

    /// <summary>
    /// Starts the engine.
    /// </summary>
    internal override void Start()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". Summarization");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        base.Timer.Start();
      }

      Instrumentation.Factory.CreateLoopInvariantInstrumentation(base.Program.AC).Run();

      base.EmitProgramContext(base.Program.AC, "sequentialized");

      if (ToolCommandLineOptions.Get().RequiresInvariantInference)
      {
        base.Program.AC = base.ParseContextFromFile("sequentialized");

        base.Program.AC.EliminateNonInvariantInferenceAssertions();
        base.Program.AC.EliminateDeadVariables();
        base.Program.AC.Inline();

        var summarizedAnalysisContext = base.ParseContextFromFile("sequentialized");
        Analysis.Factory.CreateHoudiniInvariantInference(base.Program.AC, summarizedAnalysisContext).Run();

        ModelCleaner.RemoveExistentials(summarizedAnalysisContext);

        base.EmitProgramContext(summarizedAnalysisContext, "summarised");
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        base.Timer.Stop();
        Output.PrintLine("... Summarization done [{0}]", base.Timer.Result());
      }
    }
  }
}
