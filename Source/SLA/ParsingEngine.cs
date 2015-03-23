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
        Console.WriteLine(". Parsing");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      AnalysisContext ac = null;
      new AnalysisContextParser(this.FileList[this.FileList.Count - 1],
        "bpl").TryParseNew(ref ac);

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Console.WriteLine("... Parsing done [{0}]", this.Timer.Result());
      }

      return ac;
    }
  }
}
