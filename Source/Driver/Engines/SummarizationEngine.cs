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
  internal sealed class SummarizationEngine
  {
    private AnalysisContext AC;
    private AnalysisContext PostAC;
    private ExecutionTimer Timer;

    internal SummarizationEngine(AnalysisContext ac, AnalysisContext postAc)
    {
      Contract.Requires(ac != null && postAc != null);
      this.AC = ac;
      this.PostAC = postAc;
    }

    internal void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". Summarization");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.AC.EliminateDeadVariables();
      this.AC.Inline();

      Analysis.Factory.CreateInvariantInference(this.AC, this.PostAC).Run();

      //      ModelCleaner.RemoveGenericTopLevelDeclerations(this.PostAC, this.EP);
      //      ModelCleaner.RemoveUnusedTopLevelDeclerations(this.AC);
      //      ModelCleaner.RemoveGlobalLocksets(this.PostAC);
      ModelCleaner.RemoveExistentials(this.PostAC);

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("... Summarization done [{0}]", this.Timer.Result());
      }

      Lockpwn.IO.BoogieProgramEmitter.Emit(this.PostAC.TopLevelDeclarations, ToolCommandLineOptions
        .Get().Files[ToolCommandLineOptions.Get().Files.Count - 1], "summarised", "bpl");
    }
  }
}
