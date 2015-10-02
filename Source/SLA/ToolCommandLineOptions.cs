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

using Microsoft.Boogie;

namespace Lockpwn
{
  internal class ToolCommandLineOptions : CommandLineOptions
  {
    /// <summary>
    /// The input file.
    /// </summary>
    internal string InputFile = "";

    /// <summary>
    /// The output file.
    /// </summary>
    internal string OutputFile = "";

    /// <summary>
    /// Measure time.
    /// </summary>
    internal bool MeasureTime = false;

    /// <summary>
    /// Print verbose information.
    /// </summary>
    internal bool VerboseMode = false;

    /// <summary>
    /// Print even more verbose information.
    /// </summary>
    internal bool SuperVerboseMode = false;

    /// <summary>
    /// Print debugging information.
    /// </summary>
    internal bool DebugMode = false;

    /// <summary>
    /// Disables the user provided assertions.
    /// </summary>
    internal bool DisableUserAssertions = false;

    internal bool ShowErrorModel = false;

    internal bool NoInstrumentation = false;

    internal ToolCommandLineOptions() : base("lockpwn", "lockpwn static lockset analyser")
    {

    }

    internal ToolCommandLineOptions(string toolName, string descriptiveName)
      : base(toolName, descriptiveName)
    {

    }

    protected override bool ParseOption(string option, CommandLineOptionEngine.CommandLineParseState ps)
    {
      if (option == "inputFile" || option == "i")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          this.InputFile = ps.args[ps.i];
        }
        return true;
      }
      else if (option == "outputFile" || option == "o")
      {
        if (ps.ConfirmArgumentCount(1))
        {
          var split = ps.args[ps.i].Split('.');
          if (split.Length != 2 || !split[1].Equals("bpl"))
          {
            Lockpwn.IO.Reporter.ErrorWriteLine("Extension of output must be '.bpl'");
            System.Environment.Exit((int)Outcome.ParsingError);
          }

          this.OutputFile = split[0];
        }
        return true;
      }
      else if (option == "time")
      {
        this.MeasureTime = true;
        return true;
      }
      else if (option == "verbose" || option == "v")
      {
        this.VerboseMode = true;
        return true;
      }
      else if (option == "superVerbose" || option == "v2")
      {
        this.VerboseMode = true;
        this.SuperVerboseMode = true;
        return true;
      }
      else if (option == "debug" || option == "d")
      {
        this.DebugMode = true;
        return true;
      }
      else if (option == "noUserAssertions")
      {
        this.DisableUserAssertions = true;
        return true;
      }
      else if (option == "showErrorModel")
      {
        this.ShowErrorModel = true;
        return true;
      }
      else if (option == "noInstrument")
      {
        this.NoInstrumentation = true;
        return true;
      }

      return base.ParseOption(option, ps);
    }

    internal static ToolCommandLineOptions Get()
    {
      return (ToolCommandLineOptions)CommandLineOptions.Clo;
    }
  }
}
