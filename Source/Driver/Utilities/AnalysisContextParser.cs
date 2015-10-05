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
  internal static class AnalysisContextParser
  {
    /// <summary>
    /// Parses the analysis context from the given file.
    /// </summary>
    /// <param name="file">File</param>
    /// <param name="extension">Extension</param>
    /// <param name="additional">Additional files</param>
    internal static AnalysisContext Parse(string file, string extension, List<string> additional = null)
    {
      Microsoft.Boogie.Program program = null;
      ResolutionContext rc = null;

      var filesToParse = AnalysisContextParser.GetFilesToParse(file, extension, additional);
      AnalysisContextParser.CreateProgramFromFiles(filesToParse, out program, out rc);

      var parsedAc = AnalysisContext.Create(program, rc);
      if (parsedAc == null) Environment.Exit((int)Outcome.ParsingError);

      return parsedAc;
    }

    /// <summary>
    /// Parses the analysis context from the given file using the given analysis context.
    /// </summary>
    /// <param name="file">File</param>
    /// <param name="extension">Extension</param>
    /// <param name="additional">Additional files</param>
    internal static AnalysisContext ParseWithContext(AnalysisContext ac, string file, string extension,
      List<string> additional = null)
    {
      Microsoft.Boogie.Program program = null;
      ResolutionContext rc = null;

      var filesToParse = AnalysisContextParser.GetFilesToParse(file, extension, additional);
      AnalysisContextParser.CreateProgramFromFiles(filesToParse, out program, out rc);

      var parsedAc = AnalysisContext.CreateWithContext(program, rc, ac);
      if (parsedAc == null) Environment.Exit((int)Outcome.ParsingError);

      return parsedAc;
    }

    /// <summary>
    /// Returns the files to parse.
    /// </summary>
    /// <param name="file">File</param>
    /// <param name="extension">Extension</param>
    /// <param name="additional">Additional files</param>
    /// <returns>List of files</returns>
    private static List<string> GetFilesToParse(string file, string extension, List<string> additional)
    {
      List<string> filesToParse = new List<string>();

      if (additional != null)
      {
        foreach (var str in additional)
        {
          string f = file.Substring(0, file.IndexOf(Path.GetExtension(file))) +
            "_" + str + "." + extension;
          if (!System.IO.File.Exists(f))
            Environment.Exit((int)Outcome.ParsingError);
          filesToParse.Add(f);
        }
      }
      else
      {
        string f = file.Substring(0, file.IndexOf(Path.GetExtension(file))) +
          "." + extension;
        if (!System.IO.File.Exists(f))
          Environment.Exit((int)Outcome.ParsingError);
        filesToParse.Add(f);
      }

      return filesToParse;
    }

    /// <summary>
    /// Creates a program from the given files.
    /// </summary>
    /// <param name="filesToParse">Files to parse</param>
    /// <param name="program">Program</param>
    /// <param name="rc">ResolutionContext</param>
    private static void CreateProgramFromFiles(List<string> filesToParse,
      out Microsoft.Boogie.Program program, out ResolutionContext rc)
    {
      program = ExecutionEngine.ParseBoogieProgram(filesToParse, false);
      if (program == null) Environment.Exit((int)Outcome.ParsingError);

      rc = new ResolutionContext(null);
      program.Resolve(rc);
      if (rc.ErrorCount != 0)
      {
        Output.PrintLine("{0} name resolution errors detected", rc.ErrorCount);
        Environment.Exit((int)Outcome.ParsingError);
      }

      int errorCount = program.Typecheck();
      if (errorCount != 0)
      {
        Output.PrintLine("{0} type checking errors detected", errorCount);
        Environment.Exit((int)Outcome.ParsingError);
      }
    }
  }
}
