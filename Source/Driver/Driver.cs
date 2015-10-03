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

using Microsoft.Boogie;
using System.Diagnostics;

using Lockpwn.IO;

namespace Lockpwn
{
  internal class Driver
  {
    #region static fields

    /// <summary>
    /// List of files to analyze.
    /// </summary>
    private static List<string> FileList;

    /// <summary>
    /// The analysis context.
    /// </summary>
    private static AnalysisContext AC;

    /// <summary>
    /// The post analysis context.
    /// </summary>
    private static AnalysisContext PostAC;

    #endregion

    #region methods

    internal static void Main(string[] args)
    {
      Contract.Requires(cce.NonNullElements(args));
      CommandLineOptions.Install(new ToolCommandLineOptions());

      Driver.EnableBoogieOptions();

      Driver.FileList = new List<string>();

      try
      {
        ToolCommandLineOptions.Get().RunningBoogieFromCommandLine = true;
        ToolCommandLineOptions.Get().PrintUnstructured = 2;

        if (!ToolCommandLineOptions.Get().Parse(args))
        {
          Environment.Exit((int)Outcome.FatalError);
        }

        if (ToolCommandLineOptions.Get().Files.Count == 0)
        {
          Lockpwn.IO.Reporter.ErrorWriteLine("lockpwn: error: no input files were specified");
          Environment.Exit((int)Outcome.FatalError);
        }

        foreach (string file in ToolCommandLineOptions.Get().Files)
        {
          string extension = Path.GetExtension(file);
          if (extension != null)
          {
            extension = extension.ToLower();
          }
          Driver.FileList.Add(file);
        }

        foreach (string file in Driver.FileList)
        {
          Contract.Assert(file != null);
          string extension = Path.GetExtension(file);
          if (extension != null)
          {
            extension = extension.ToLower();
          }
          if (extension != ".bpl")
          {
            Lockpwn.IO.Reporter.ErrorWriteLine("lockpwn: error: {0} is not a .bpl file", file);
            Environment.Exit((int)Outcome.FatalError);
          }
        }

        Driver.ParseAnalyzeAndInstrument();
        Driver.RunSummarizationEngine();
        Driver.RunReachabilityAnalysisEngine();

        if (ToolCommandLineOptions.Get().VerboseMode)
          Output.PrintLine(". Done");

        Driver.CleanUpTemporaryFiles();
        Environment.Exit((int)Outcome.Done);
      }
      catch (Exception e)
      {
        Console.Error.Write("Exception thrown in lockpwn: ");
        Console.Error.WriteLine(e);
        Driver.CleanUpTemporaryFiles();
        Environment.Exit((int)Outcome.FatalError);
      }
    }

    /// <summary>
    /// Parses, analyzes and instruments the program.
    /// </summary>
    private static void ParseAnalyzeAndInstrument()
    {
      if (!ToolCommandLineOptions.Get().SkipInstrumentation)
      {
        Driver.AC = new ParsingEngine(Driver.FileList).Run();

        new ThreadAnalysisEngine(Driver.AC).Run();
        new ThreadInstrumentationEngine(Driver.AC).Run();
      }

      new AnalysisContextParser(Driver.FileList[Driver.FileList.Count - 1], "bpl")
        .TryParseNew(ref Driver.AC, new List<string> { "instrumented" });
      new AnalysisContextParser(Driver.FileList[Driver.FileList.Count - 1], "bpl")
        .TryParseNew(ref Driver.PostAC, new List<string> { "instrumented" });
    }

    /// <summary>
    /// Runs the summarization engine on the program.
    /// </summary>
    private static void RunSummarizationEngine()
    {
      if (ToolCommandLineOptions.Get().SkipSummarization)
        return;

      new SummarizationEngine(Driver.AC, Driver.PostAC).Run();

      new AnalysisContextParser(Driver.FileList[Driver.FileList.Count - 1], "bpl")
        .TryParseNew(ref Driver.AC, new List<string> { "summarised" });
      new AnalysisContextParser(Driver.FileList[Driver.FileList.Count - 1], "bpl")
        .TryParseNew(ref Driver.PostAC);
    }

    /// <summary>
    /// Runs the reachability analysis engine on the program.
    /// </summary>
    private static void RunReachabilityAnalysisEngine()
    {
      new ReachabilityAnalysisEngine(Driver.AC, Driver.PostAC).Run();
    }

    /// <summary>
    /// Enables the boogie options.
    /// </summary>
    private static void EnableBoogieOptions()
    {
      CommandLineOptions.Clo.DoModSetAnalysis = true;
      CommandLineOptions.Clo.DontShowLogo = true;
      CommandLineOptions.Clo.TypeEncodingMethod = CommandLineOptions.TypeEncoding.Monomorphic;
      CommandLineOptions.Clo.ModelViewFile = "-";
      CommandLineOptions.Clo.UseLabels = false;
      CommandLineOptions.Clo.EnhancedErrorMessages = 1;
      CommandLineOptions.Clo.ContractInfer = true;
    }

    /// <summary>
    /// Cleans up temporary files.
    /// </summary>
    private static void CleanUpTemporaryFiles()
    {
      if (ToolCommandLineOptions.Get().KeepTemporaryFiles)
        return;

      Lockpwn.IO.BoogieProgramEmitter.Remove(Driver.FileList[Driver.FileList.Count - 1],
        "instrumented", "bpl");
      Lockpwn.IO.BoogieProgramEmitter.Remove(Driver.FileList[Driver.FileList.Count - 1],
        "summarised", "bpl");
    }

    #endregion
  }
}
