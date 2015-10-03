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
  internal sealed class SequentializationEngine
  {
    private Program Program;
    private ExecutionTimer Timer;

    internal SequentializationEngine(Program program)
    {
      Contract.Requires(program != null);
      this.Program = program;
    }

    internal void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". Sequentialization");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.Program.AC).Run();

      Instrumentation.Factory.CreateLocksetInstrumentation(this.Program.AC).Run();
      Instrumentation.Factory.CreateRaceCheckingInstrumentation(this.Program.AC).Run();

      Analysis.Factory.CreateSharedStateAbstraction(this.Program.AC).Run();

      Instrumentation.Factory.CreateErrorReportingInstrumentation(this.Program.AC).Run();
      Instrumentation.Factory.CreateAccessCheckingInstrumentation(this.Program.AC).Run();

      foreach (var thread in this.Program.AC.Threads)
        this.Program.AC.InlineThread(thread);
      this.Program.AC.InlineThreadHelpers();

//        ModelCleaner.RemoveInlineFromHelperFunctions(this.AC, this.EP);
//      ModelCleaner.RemoveUnecesseryInfoFromSpecialFunctions(this.AC);
//      ModelCleaner.RemoveCorralFunctions(this.AC);
      //      ModelCleaner.RemoveModelledProcedureBodies(this.AC);

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("... Sequentialization done [{0}]", this.Timer.Result());
      }

      if (ToolCommandLineOptions.Get().SkipSummarization)
        this.EmitProgramContext(this.Program.AC, "instrumented");
    }

    /// <summary>
    /// Emits the given analysis context.
    /// </summary>
    /// <param name="ac">AnalysisContext</param>
    /// <param name="suffix">Suffix</param>
    private void EmitProgramContext(AnalysisContext ac, string suffix)
    {
      Lockpwn.IO.BoogieProgramEmitter.Emit(ac.TopLevelDeclarations, ToolCommandLineOptions
        .Get().Files[ToolCommandLineOptions.Get().Files.Count - 1], suffix, "bpl");
    }
  }
}
