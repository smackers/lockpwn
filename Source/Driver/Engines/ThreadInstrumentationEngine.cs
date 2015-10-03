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

using Lockpwn.IO;

namespace Lockpwn
{
  internal sealed class ThreadInstrumentationEngine
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    internal ThreadInstrumentationEngine(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    internal void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". ThreadInstrumentation");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC).Run();

      Instrumentation.Factory.CreateLocksetInstrumentation(this.AC).Run();
      Instrumentation.Factory.CreateRaceCheckingInstrumentation(this.AC).Run();

      Analysis.Factory.CreateSharedStateAbstraction(this.AC).Run();

      if (!ToolCommandLineOptions.Get().SkipSummarization)
      {
        Instrumentation.Factory.CreateLoopSummaryInstrumentation(this.AC).Run();
      }

      Instrumentation.Factory.CreateErrorReportingInstrumentation(this.AC).Run();
      Instrumentation.Factory.CreateAccessCheckingInstrumentation(this.AC).Run();

      foreach (var thread in this.AC.Threads)
        this.AC.InlineThread(thread);
      this.AC.InlineThreadHelpers();

//        ModelCleaner.RemoveInlineFromHelperFunctions(this.AC, this.EP);
//      ModelCleaner.RemoveUnecesseryInfoFromSpecialFunctions(this.AC);
//      ModelCleaner.RemoveCorralFunctions(this.AC);
      //      ModelCleaner.RemoveModelledProcedureBodies(this.AC);

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("... ThreadInstrumentation done [{0}]", this.Timer.Result());
      }

      Lockpwn.IO.BoogieProgramEmitter.Emit(this.AC.TopLevelDeclarations, ToolCommandLineOptions
        .Get().Files[ToolCommandLineOptions.Get().Files.Count - 1], "instrumented", "bpl");
    }
  }
}
