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
using System.IO;

using Microsoft.Boogie;

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
        Console.WriteLine(s);
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

    internal static void AdvisoryWriteLine(string format, params object[] args)
    {
      Contract.Requires(format != null);
      ConsoleColor col = Console.ForegroundColor;
      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine(format, args);
      Console.ForegroundColor = col;
    }

    internal static void Inform(string s)
    {
      if (CommandLineOptions.Clo.Trace || CommandLineOptions.Clo.TraceProofObligations)
      {
        Console.WriteLine(s);
      }
    }
  }
}
