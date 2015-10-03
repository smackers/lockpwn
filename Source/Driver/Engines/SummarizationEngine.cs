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
    private Program Program;
    private ExecutionTimer Timer;

    internal SummarizationEngine(Program program)
    {
      Contract.Requires(program != null);
      this.Program = program;
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

      this.Program.AC.EliminateDeadVariables();
      this.Program.AC.Inline();

      Analysis.Factory.CreateInvariantInference(this.Program.AC, this.Program.PostAC).Run();

      //      ModelCleaner.RemoveGenericTopLevelDeclerations(this.Program.PostAC, this.EP);
      //      ModelCleaner.RemoveUnusedTopLevelDeclerations(this.Program.AC);
      //      ModelCleaner.RemoveGlobalLocksets(this.Program.PostAC);
      ModelCleaner.RemoveExistentials(this.Program.PostAC);

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("... Summarization done [{0}]", this.Timer.Result());
      }

      Lockpwn.IO.BoogieProgramEmitter.Emit(this.Program.PostAC.TopLevelDeclarations, ToolCommandLineOptions
        .Get().Files[ToolCommandLineOptions.Get().Files.Count - 1], "summarised", "bpl");
    }
  }
}
