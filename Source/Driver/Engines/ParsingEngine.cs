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
  internal sealed class ParsingEngine : AbstractEngine
  {
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="program">Program</param>
    internal ParsingEngine(Program program)
      : base(program)
    { }

    /// <summary>
    /// Starts the engine.
    /// </summary>
    internal override void Start()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". Parsing");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        base.Timer.Start();
      }

      base.Program.AC = base.ParseContextFromInputFile();

      Refactoring.Factory.CreateProgramSimplifier(base.Program.AC).Run();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        base.Timer.Stop();
        Output.PrintLine("... Parsing done [{0}]", base.Timer.Result());
      }
    }
  }
}
