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
    private List<string> FileList;
    private ExecutionTimer Timer;

    internal ParsingEngine(List<string> fileList)
    {
      Contract.Requires(fileList != null && fileList.Count > 0);
      this.FileList = fileList;
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
      new AnalysisContextParser(this.FileList[this.FileList.Count - 1],
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
