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
  internal class AnalysisContextParser
  {
    private string File;
    private string Extension;

    internal AnalysisContextParser(string file, string ext)
    {
      this.File = file;
      this.Extension = ext;
    }

    internal bool TryParseNew(ref AnalysisContext ac, List<string> additional = null)
    {
      List<string> filesToParse = new List<string>();
//      filesToParse.Add(ToolCommandLineOptions.Get().WhoopDeclFile);

      if (additional != null)
      {
        foreach (var str in additional)
        {
          string file = this.File.Substring(0, this.File.IndexOf(Path.GetExtension(this.File))) +
            "_" + str + "." + this.Extension;
          if (!System.IO.File.Exists(file))
            return false;
          filesToParse.Add(file);
        }
      }
      else
      {
        string file = this.File.Substring(0, this.File.IndexOf(Path.GetExtension(this.File))) +
          "." + this.Extension;
        if (!System.IO.File.Exists(file))
          return false;
        filesToParse.Add(file);
      }

      Microsoft.Boogie.Program program = ExecutionEngine.ParseBoogieProgram(filesToParse, false);
      if (program == null) return false;

      ResolutionContext rc = new ResolutionContext(null);
      program.Resolve(rc);
      if (rc.ErrorCount != 0)
      {
        Output.PrintLine("{0} name resolution errors detected", rc.ErrorCount);
        return false;
      }

      int errorCount = program.Typecheck();
      if (errorCount != 0)
      {
        Output.PrintLine("{0} type checking errors detected", errorCount);
        return false;
      }

      ac = new AnalysisContext(program, rc);
      if (ac == null) Environment.Exit((int)Outcome.ParsingError);

      return true;
    }
  }
}
