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
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Lockpwn
{
  internal sealed class LocksetInstrumentationEngine
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    internal LocksetInstrumentationEngine(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    internal void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Console.WriteLine(". StaticLocksetInstrumentation");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(this.AC).Run();

      Instrumentation.Factory.CreateLocksetInstrumentation(this.AC).Run();
      Instrumentation.Factory.CreateRaceCheckingInstrumentation(this.AC).Run();

      Analysis.Factory.CreateSharedStateAbstraction(this.AC).Run();

      Instrumentation.Factory.CreateErrorReportingInstrumentation(this.AC).Run();
      Instrumentation.Factory.CreateAccessCheckingInstrumentation(this.AC).Run();

      foreach (var thread in this.AC.Threads)
      {
        this.AC.InlineThread(thread);
      }

//        ModelCleaner.RemoveInlineFromHelperFunctions(this.AC, this.EP);
//      ModelCleaner.RemoveUnecesseryInfoFromSpecialFunctions(this.AC);
//      ModelCleaner.RemoveCorralFunctions(this.AC);
      //      ModelCleaner.RemoveModelledProcedureBodies(this.AC);

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Console.WriteLine("... StaticLocksetInstrumentation done [{0}]", this.Timer.Result());
      }
    }
  }
}
