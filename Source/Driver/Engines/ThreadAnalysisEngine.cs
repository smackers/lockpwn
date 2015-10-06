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

using Lockpwn.IO;

namespace Lockpwn
{
  internal sealed class ThreadAnalysisEngine : AbstractEngine
  {
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="program">Program</param>
    internal ThreadAnalysisEngine(Program program)
      : base(program)
    { }

    /// <summary>
    /// Starts the engine.
    /// </summary>
    internal override void Start()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". ThreadAnalysis");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        base.Timer.Start();
      }

      Analysis.Factory.CreateThreadUsageAnalysis(base.Program.AC).Run();
      Analysis.Factory.CreateLockUsageAnalysis(base.Program.AC).Run();

      Refactoring.Factory.CreateThreadRefactoring(base.Program.AC).Run();

      Analysis.Factory.CreateSharedStateAnalysis(base.Program.AC).Run();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        base.Timer.Stop();
        Output.PrintLine("... ThreadAnalysis done [{0}]", base.Timer.Result());
      }
    }
  }
}
