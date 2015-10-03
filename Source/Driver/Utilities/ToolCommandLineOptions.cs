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
using Lockpwn.IO;
using Microsoft.Boogie;

namespace Lockpwn
{
  internal class ToolCommandLineOptions : CommandLineOptions
  {
    /// <summary>
    /// The output file.
    /// </summary>
    internal string OutputFile = "";

    /// <summary>
    /// Print verbose information.
    /// </summary>
    internal bool VerboseMode = false;

    /// <summary>
    /// Print even more verbose information.
    /// </summary>
    internal bool SuperVerboseMode = false;

    /// <summary>
    /// Keep temporary files.
    /// </summary>
    internal bool KeepTemporaryFiles = false;

    /// <summary>
    /// Measure time.
    /// </summary>
    internal bool MeasureTime = false;

    /// <summary>
    /// Disables the user provided assertions.
    /// </summary>
    internal bool DisableUserAssertions = false;

    internal bool ShowErrorModel = false;

    /// <summary>
    /// Skip the instrumentation phase.
    /// </summary>
    internal bool SkipInstrumentation = false;

    /// <summary>
    /// Skip the summarization phase.
    /// </summary>
    internal bool SkipSummarization = false;

    internal ToolCommandLineOptions() : base("lockpwn", "lockpwn static lockset analyser")
    {

    }

    internal ToolCommandLineOptions(string toolName, string descriptiveName)
      : base(toolName, descriptiveName)
    {

    }

    protected override bool ParseOption(string option, CommandLineOptionEngine.CommandLineParseState ps)
    {
      if (option == "?")
      {
        this.ShowHelp();
        System.Environment.Exit(1);
      }
      else if (option == "o")
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
      else if (option == "v")
      {
        this.VerboseMode = true;
        return true;
      }
      else if (option == "v2")
      {
        this.VerboseMode = true;
        this.SuperVerboseMode = true;
        return true;
      }
      else if (option == "debug")
      {
        Output.Debugging = true;
        return true;
      }
      else if (option == "temp")
      {
        this.KeepTemporaryFiles = true;
        return true;
      }
      else if (option == "time")
      {
        this.MeasureTime = true;
        return true;
      }
      else if (option == "noAssert")
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
        this.SkipInstrumentation = true;
        return true;
      }
      else if (option == "noSummary")
      {
        this.SkipSummarization = true;
        return true;
      }

      return base.ParseOption(option, ps);
    }

    /// <summary>
    /// Shows help.
    /// </summary>
    private void ShowHelp()
    {
      string help = "\n";

      help += "--------------";
      help += "\nBasic options:";
      help += "\n--------------";
      help += "\n  /?\t\t Show this help menu";
      help += "\n  /o:[x]\t Name of the output file";
      help += "\n  /v\t\t Enable verbose mode";
      help += "\n  /v2\t\t Enable super verbose mode";
      help += "\n  /debug\t Enable debugging";
      help += "\n  /time\t\t Print timing information";

      help += "\n\n-----------------";
      help += "\nAdvanced options:";
      help += "\n-----------------";
      help += "\n  /noAssert\t Disables user-provided assertions";

      help += "\n";

      Output.PrettyPrintLine(help);
    }

    internal static ToolCommandLineOptions Get()
    {
      return (ToolCommandLineOptions)CommandLineOptions.Clo;
    }
  }
}
