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
      CommandLineOptions.Clo.DoModSetAnalysis = true;

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

        var ac = new ParsingEngine(fileList).Run();

        new ThreadAnalysisEngine(ac).Run();
        new LocksetInstrumentationEngine(ac).Run();

        if (ToolCommandLineOptions.Get().VerboseMode)
          Console.WriteLine(". Done");

        Lockpwn.IO.BoogieProgramEmitter.Emit(ac.TopLevelDeclarations, ToolCommandLineOptions.Get().Files[
          ToolCommandLineOptions.Get().Files.Count - 1], "$pwned", "bpl");

        Environment.Exit((int)Outcome.Done);
      }
      catch (Exception e)
      {
        Console.Error.Write("Exception thrown in lockpwn: ");
        Console.Error.WriteLine(e);
        Environment.Exit((int)Outcome.FatalError);
      }
    }

//    private static void RunStaticLocksetAnalysisInstrumentationEngine(AnalysisContext ac)
//    {
//      Program.StartTimer("StaticLocksetAnalysisInstrumentationEngine");
//
//      AnalysisContext ac = null;
//      new AnalysisContextParser(Program.FileList[Program.FileList.Count - 1], "bpl").TryParseNew(
//        ref ac, new List<string> { ep.Name });
//
//      Analysis.SharedStateAnalyser.AnalyseMemoryRegions(ac, ep);
//      AnalysisContext.RegisterEntryPointAnalysisContext(ac, ep);
//
//      var ac = AnalysisContext.GetAnalysisContext(ep);
//      new StaticLocksetAnalysisInstrumentationEngine(ac, ep).Run();
//
//      Program.StopTimer();
//    }
  }
}
