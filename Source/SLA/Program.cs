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

namespace Lockpwn
{
  internal class Program
  {
    internal static void Main(string[] args)
    {
      Contract.Requires(cce.NonNullElements(args));
      CommandLineOptions.Install(new ToolCommandLineOptions());

      Program.EnableBoogieOptions();

      var fileList = new List<string>();

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
          fileList.Add(file);
        }

        foreach (string file in fileList)
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

        if (ToolCommandLineOptions.Get().NoInstrumentation)
        {
          Program.CheckAndSpitProgram(fileList);
        }

        var ac = new ParsingEngine(fileList).Run();

        new ThreadAnalysisEngine(ac).Run();
        new ThreadInstrumentationEngine(ac).Run();

        AnalysisContext postAc = null;
        new AnalysisContextParser(fileList[fileList.Count - 1], "bpl").TryParseNew(ref ac,
          new List<string> { "instrumented" });
        new AnalysisContextParser(fileList[fileList.Count - 1], "bpl").TryParseNew(ref postAc,
          new List<string> { "instrumented" });
        postAc.ErrorReporter = ac.ErrorReporter;

        new Cruncher(ac, postAc).Run();

        new AnalysisContextParser(fileList[fileList.Count - 1], "bpl").TryParseNew(ref ac,
          new List<string> { "summarised" });
        new AnalysisContextParser(fileList[fileList.Count - 1], "bpl").TryParseNew(ref postAc);
        postAc.ErrorReporter = ac.ErrorReporter;

        new StaticLocksetAnalyser(ac, postAc).Run();

        if (ToolCommandLineOptions.Get().VerboseMode)
          Console.WriteLine(". Done");

        Environment.Exit((int)Outcome.Done);
      }
      catch (Exception e)
      {
        Console.Error.Write("Exception thrown in lockpwn: ");
        Console.Error.WriteLine(e);
        Environment.Exit((int)Outcome.FatalError);
      }
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

    internal static void CheckAndSpitProgram(List<string> fileList)
    {
      AnalysisContext ac = null;
      AnalysisContext postAc = null;
      new AnalysisContextParser(fileList[fileList.Count - 1], "bpl").TryParseNew(ref ac,
        new List<string> { "instrumented" });
      new AnalysisContextParser(fileList[fileList.Count - 1], "bpl").TryParseNew(ref postAc);
      postAc.ErrorReporter = ac.ErrorReporter;

      new StaticLocksetAnalyser(ac, postAc).Run();

      if (ToolCommandLineOptions.Get().VerboseMode)
        Console.WriteLine(". Done");

      Environment.Exit((int)Outcome.Done);
    }
  }
}
