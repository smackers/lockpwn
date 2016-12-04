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
    #region methods

    internal static void Main(string[] args)
    {
      Contract.Requires(cce.NonNullElements(args));
      Driver.InstallCommandLineOptions(args);

      var program = new Program();

      try
      {
        ExecutionTimer timer = null;
        if (ToolCommandLineOptions.Get().MeasureTime)
        {
          timer = new ExecutionTimer();
          timer.Start();
        }

        Driver.ParseFiles(program);

        try
        {
          Driver.ParseAnalyzeAndSequentialize(program);
          Driver.RunSummarizationEngine(program);
          Driver.RunReachabilityAnalysisEngine(program);
        }
        catch (AnalysisFailedException)
        {
          Lockpwn.IO.Reporter.WarningWriteLine("Warning: Failed fast");
        }

        if (ToolCommandLineOptions.Get().MeasureTime)
        {
          timer.Stop();
          Output.PrintLine(". Done [{0}]", timer.Result());
        }
        else if (ToolCommandLineOptions.Get().VerboseMode)
        {
          Output.PrintLine(". Done");
        }

        Driver.CleanUpTemporaryFiles(program);
        Environment.Exit((int)Outcome.Done);
      }
      catch (Exception e)
      {
        Console.Error.Write("Exception thrown in lockpwn: ");
        Console.Error.WriteLine(e);
        Driver.CleanUpTemporaryFiles(program);
        Environment.Exit((int)Outcome.FatalError);
      }
    }

    /// <summary>
    /// Parses, analyzes and sequentializes the program.
    /// </summary>
    /// <param name="program">Program</param>
    private static void ParseAnalyzeAndSequentialize(Program program)
    {
      if (ToolCommandLineOptions.Get().SkipSequentialization)
        return;

      new ParsingEngine(program).Start();
      new ThreadAnalysisEngine(program).Start();
      new SequentializationEngine(program).Start();
    }

    /// <summary>
    /// Runs the summarization engine on the program.
    /// </summary>
    /// <param name="program">Program</param>
    private static void RunSummarizationEngine(Program program)
    {
      if (ToolCommandLineOptions.Get().SkipSummarization)
        return;

      new SummarizationEngine(program).Start();
    }

    /// <summary>
    /// Runs the reachability analysis engine on the program.
    /// </summary>
    /// <param name="program">Program</param>
    private static void RunReachabilityAnalysisEngine(Program program)
    {
      new ReachabilityAnalysisEngine(program).Start();
    }

    /// <summary>
    /// Installs the command line options.
    /// </summary>
    /// <param name="args">Arguments</param>
    private static void InstallCommandLineOptions(string[] args)
    {
      CommandLineOptions.Install(new ToolCommandLineOptions());

      CommandLineOptions.Clo.DoModSetAnalysis = true;
      CommandLineOptions.Clo.DontShowLogo = true;
      CommandLineOptions.Clo.TypeEncodingMethod = CommandLineOptions.TypeEncoding.Monomorphic;
      CommandLineOptions.Clo.ModelViewFile = "-";
      CommandLineOptions.Clo.UseLabels = false;
      CommandLineOptions.Clo.EnhancedErrorMessages = 1;
      CommandLineOptions.Clo.ContractInfer = true;

      ToolCommandLineOptions.Get().RunningBoogieFromCommandLine = true;
      ToolCommandLineOptions.Get().PrintUnstructured = 2;

      if (!ToolCommandLineOptions.Get().Parse(args))
      {
        Environment.Exit((int)Outcome.FatalError);
      }
    }

    /// <summary>
    /// Parses the input files.
    /// </summary>
    /// <param name="program">Program</param>
    private static void ParseFiles(Program program)
    {
      if (ToolCommandLineOptions.Get().Files.Count == 0)
      {
        Lockpwn.IO.Reporter.ErrorWriteLine("lockpwn: error: no input files were specified.");
        Environment.Exit((int)Outcome.FatalError);
      }

      foreach (string file in ToolCommandLineOptions.Get().Files)
      {
        string extension = Path.GetExtension(file);
        if (extension != null)
        {
          extension = extension.ToLower();
        }

        program.FileList.Add(file);
      }

      foreach (string file in program.FileList)
      {
        Contract.Assert(file != null);
        string extension = Path.GetExtension(file);
        if (extension != null)
        {
          extension = extension.ToLower();
        }
        if (extension != ".bpl")
        {
          Lockpwn.IO.Reporter.ErrorWriteLine("lockpwn: error: {0} is not a .bpl file.", file);
          Environment.Exit((int)Outcome.FatalError);
        }
      }

      if (ToolCommandLineOptions.Get().SkipSequentialization &&
        ToolCommandLineOptions.Get().SkipSummarization)
      {
        string fileName;
        var exists = Lockpwn.IO.BoogieProgramEmitter.Exists(program
          .FileList[program.FileList.Count - 1], "sequentialized", "bpl", out fileName);
        if (!exists)
        {
          Console.Error.WriteLine("Error: File '{0}' not found.", fileName);
          Driver.CleanUpTemporaryFiles(program);
          Environment.Exit((int)Outcome.FatalError);
        }
      }
    }

    /// <summary>
    /// Cleans up temporary files.
    /// </summary>
    /// <param name="program">Program</param>
    private static void CleanUpTemporaryFiles(Program program)
    {
      if (ToolCommandLineOptions.Get().KeepTemporaryFiles)
        return;

      Lockpwn.IO.BoogieProgramEmitter.Remove(program.FileList[program.FileList.Count - 1],
        "sequentialized", "bpl");
      Lockpwn.IO.BoogieProgramEmitter.Remove(program.FileList[program.FileList.Count - 1],
        "summarised", "bpl");
    }

    #endregion
  }
}
