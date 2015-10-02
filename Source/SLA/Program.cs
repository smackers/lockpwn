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
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

using Microsoft.Boogie;
using System.Diagnostics;

using Lockpwn.IO;

namespace Lockpwn
{
  internal class Program
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

      Program.EnableBoogieOptions();

      Program.FileList = new List<string>();

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
          Program.FileList.Add(file);
        }

        foreach (string file in Program.FileList)
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

        Program.ParseAnalyzeAndInstrument();
        Program.RunCruncher();
        Program.RunStaticAnalyzer();

        if (ToolCommandLineOptions.Get().VerboseMode)
          Output.PrintLine(". Done");

        Environment.Exit((int)Outcome.Done);
      }
      catch (Exception e)
      {
        Console.Error.Write("Exception thrown in lockpwn: ");
        Console.Error.WriteLine(e);
        Environment.Exit((int)Outcome.FatalError);
      }
    }

    /// <summary>
    /// Parses, analyzes and instruments the program.
    /// </summary>
    internal static void ParseAnalyzeAndInstrument()
    {
      if (!ToolCommandLineOptions.Get().SkipInstrumentation)
      {
        Program.AC = new ParsingEngine(Program.FileList).Run();

        new ThreadAnalysisEngine(Program.AC).Run();
        new ThreadInstrumentationEngine(Program.AC).Run();
      }

      new AnalysisContextParser(Program.FileList[Program.FileList.Count - 1], "bpl")
        .TryParseNew(ref Program.AC, new List<string> { "instrumented" });
      new AnalysisContextParser(Program.FileList[Program.FileList.Count - 1], "bpl")
        .TryParseNew(ref Program.PostAC, new List<string> { "instrumented" });
    }

    /// <summary>
    /// Runs the cruncher on the program.
    /// </summary>
    internal static void RunCruncher()
    {
      if (ToolCommandLineOptions.Get().SkipCrunching)
        return;

      new Cruncher(Program.AC, Program.PostAC).Run();

      new AnalysisContextParser(Program.FileList[Program.FileList.Count - 1], "bpl")
        .TryParseNew(ref Program.AC, new List<string> { "summarised" });
      new AnalysisContextParser(Program.FileList[Program.FileList.Count - 1], "bpl")
        .TryParseNew(ref Program.PostAC);
    }

    /// <summary>
    /// Runs the static analyzer on the program.
    /// </summary>
    internal static void RunStaticAnalyzer()
    {
      new StaticLocksetAnalyser(Program.AC, Program.PostAC).Run();
    }

    internal static void EnableBoogieOptions()
    {
      CommandLineOptions.Clo.DoModSetAnalysis = true;
      CommandLineOptions.Clo.DontShowLogo = true;
      CommandLineOptions.Clo.TypeEncodingMethod = CommandLineOptions.TypeEncoding.Monomorphic;
      CommandLineOptions.Clo.ModelViewFile = "-";
      CommandLineOptions.Clo.UseLabels = false;
      CommandLineOptions.Clo.EnhancedErrorMessages = 1;
      CommandLineOptions.Clo.ContractInfer = true;
    }

    #endregion
  }
}
