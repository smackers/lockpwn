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
  internal sealed class ParsingEngine
  {
    private Program Program;
    private ExecutionTimer Timer;

    internal ParsingEngine(Program program)
    {
      Contract.Requires(program != null);
      this.Program = program;
    }

    internal AnalysisContext Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". Parsing");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      AnalysisContext ac = null;
      new AnalysisContextParser(this.Program.FileList[this.Program.FileList.Count - 1],
        "bpl").TryParseNew(ref ac);

      Refactoring.Factory.CreateProgramSimplifier(ac).Run();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("... Parsing done [{0}]", this.Timer.Result());
      }

      return ac;
    }
  }
}
