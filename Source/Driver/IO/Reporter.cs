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
using System.IO;

using Microsoft.Boogie;

using Lockpwn.IO;

namespace Lockpwn.IO
{
  /// <summary>
  /// IO reporter class.
  /// </summary>
  internal static class Reporter
  {
    internal static void ReportBplError(Absy node, string message, bool error, bool showBplLocation)
    {
      Contract.Requires(message != null);
      Contract.Requires(node != null);
      IToken tok = node.tok;
      string s;
      if (tok != null && showBplLocation)
      {
        s = string.Format("{0}({1},{2}): {3}", tok.filename, tok.line, tok.col, message);
      }
      else
      {
        s = message;
      }
      if (error)
      {
        Lockpwn.IO.Reporter.ErrorWriteLine(s);
      }
      else
      {
        Output.PrintLine(s);
      }
    }

    internal static void ErrorWriteLine(string s)
    {
      Contract.Requires(s != null);
      Console.Error.WriteLine(s);
    }

    internal static void ErrorWriteLine(string format, params object[] args)
    {
      Contract.Requires(format != null);
      string s = string.Format(format, args);
      Lockpwn.IO.Reporter.ErrorWriteLine(s);
    }

    internal static void WarningWriteLine(string format, params object[] args)
    {
      Contract.Requires(format != null);
      ConsoleColor col = Console.ForegroundColor;
      Console.ForegroundColor = ConsoleColor.Yellow;
      Output.PrintLine(format, args);
      Console.ForegroundColor = col;
    }

    internal static void Inform(string s)
    {
      if (CommandLineOptions.Clo.Trace || CommandLineOptions.Clo.TraceProofObligations)
      {
        Output.PrintLine(s);
      }
    }

    public static void WriteTrailer(PipelineStatistics stats)
    {
      Contract.Requires(0 <= stats.ErrorCount);

      if (CommandLineOptions.Clo.vcVariety == CommandLineOptions.VCVariety.Doomed)
      {
        Output.Print("..... {0} credible, {1} doomed{2}", stats.VerifiedCount,
          stats.ErrorCount, stats.ErrorCount == 1 ? "" : "s");
      }
      else
      {
        Output.Print("..... {0} verified, {1} error{2}", stats.VerifiedCount,
          stats.ErrorCount, stats.ErrorCount == 1 ? "" : "s");
      }

      if (stats.InconclusiveCount != 0)
      {
        Output.Print(", {0} inconclusive{1}", stats.InconclusiveCount,
          stats.InconclusiveCount == 1 ? "" : "s");
      }

      if (stats.TimeoutCount != 0)
      {
        Output.Print(", {0} time out{1}", stats.TimeoutCount,
          stats.TimeoutCount == 1 ? "" : "s");
      }

      if (stats.OutOfMemoryCount != 0)
      {
        Output.Print(", {0} out of memory", stats.OutOfMemoryCount);
      }

      Output.PrintLine("");
      Output.Flush();
    }
  }
}
