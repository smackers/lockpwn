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
  internal sealed class SequentializationEngine : AbstractEngine
  {
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="program">Program</param>
    internal SequentializationEngine(Program program)
      : base(program)
    { }

    /// <summary>
    /// Starts the engine.
    /// </summary>
    internal override void Start()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". Sequentialization");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        base.Timer.Start();
      }

      Instrumentation.Factory.CreateGlobalRaceCheckingInstrumentation(base.Program.AC).Run();

      Instrumentation.Factory.CreateLocksetInstrumentation(base.Program.AC).Run();
      Instrumentation.Factory.CreateRaceCheckingInstrumentation(base.Program.AC).Run();

      Analysis.Factory.CreateSharedStateAbstraction(base.Program.AC).Run();

      Instrumentation.Factory.CreateErrorReportingInstrumentation(base.Program.AC).Run();
      Instrumentation.Factory.CreateAccessCheckingInstrumentation(base.Program.AC).Run();

      foreach (var thread in base.Program.AC.ThreadTemplates)
        base.Program.AC.InlineThread(thread);
      base.Program.AC.InlineThreadHelpers();

      if (ToolCommandLineOptions.Get().SkipSummarization)
      {
        base.EmitProgramContext(base.Program.AC, "sequentialized");
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        base.Timer.Stop();
        Output.PrintLine("... Sequentialization done [{0}]", base.Timer.Result());
      }
    }
  }
}
