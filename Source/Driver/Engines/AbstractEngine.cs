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
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Microsoft.Boogie;

using Lockpwn.IO;

namespace Lockpwn
{
  internal abstract class AbstractEngine
  {
    /// <summary>
    /// The program.
    /// </summary>
    protected Program Program;

    /// <summary>
    /// The timer.
    /// </summary>
    protected ExecutionTimer Timer;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="program">Program</param>
    internal AbstractEngine(Program program)
    {
      Contract.Requires(program != null);
      this.Program = program;

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
      }
    }

    /// <summary>
    /// Starts the engine.
    /// </summary>
    internal abstract void Start();

    /// <summary>
    /// Parses the analysis context from the input file.
    /// </summary>
    /// <returns>AnalysisContext</returns>
    protected AnalysisContext ParseContextFromInputFile()
    {
      return AnalysisContextParser.Parse(this.Program.FileList[this.Program
        .FileList.Count - 1], "bpl");
    }

    /// <summary>
    /// Parses the analysis context from the file with the given suffix.
    /// </summary>
    /// <param name="suffix">Suffix</param>
    /// <returns>AnalysisContext</returns>
    protected AnalysisContext ParseContextFromFile(string suffix)
    {
      return AnalysisContextParser.Parse(this.Program.FileList[this.Program
        .FileList.Count - 1], "bpl", new List<string> { suffix });
    }

    /// <summary>
    /// Parses the analysis context from the file with the
    /// given analysis context and suffix.
    /// </summary>
    /// <param name="suffix">Suffix</param>
    /// <returns>AnalysisContext</returns>
    protected AnalysisContext ParseContextFromFile(AnalysisContext ac, string suffix)
    {
      return AnalysisContextParser.ParseWithContext(ac, this.Program.FileList[this.Program
        .FileList.Count - 1], "bpl", new List<string> { suffix });
    }

    /// <summary>
    /// Emits the given analysis context to the user specified output file.
    /// </summary>
    /// <param name="ac">AnalysisContext</param>
    /// <param name="suffix">Suffix</param>
    protected void EmitProgramContext(AnalysisContext ac)
    {
      Lockpwn.IO.BoogieProgramEmitter.EmitToFileWithName(ac.TopLevelDeclarations,
        ToolCommandLineOptions.Get().Files[ToolCommandLineOptions.Get().Files.Count - 1],
        ToolCommandLineOptions.Get().OutputFile);
    }

    /// <summary>
    /// Emits the given analysis context.
    /// </summary>
    /// <param name="ac">AnalysisContext</param>
    /// <param name="suffix">Suffix</param>
    protected void EmitProgramContext(AnalysisContext ac, string suffix)
    {
      Lockpwn.IO.BoogieProgramEmitter.Emit(ac.TopLevelDeclarations, ToolCommandLineOptions
        .Get().Files[ToolCommandLineOptions.Get().Files.Count - 1], suffix, "bpl");
    }
  }
}
