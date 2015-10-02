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
using System.Linq;

using Microsoft.Boogie;
using Microsoft.Basetypes;

using Lockpwn.IO;

namespace Lockpwn.Analysis
{
  internal class RaceCheckAnalysis : IPass
  {
    private AnalysisContext AC;
    PipelineStatistics Stats;
    private ExecutionTimer Timer;

    internal RaceCheckAnalysis(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.Stats = new PipelineStatistics();
    }

    /// <summary>
    /// Runs a race checking analysis pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... RaceCheckAnalysis");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.CheckForRaces();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void CheckForRaces()
    {
      this.AC.EliminateDeadVariables();
      this.AC.Inline();
      if (ToolCommandLineOptions.Get().LoopUnrollCount != -1)
        this.AC.Program.UnrollLoops(ToolCommandLineOptions.Get().LoopUnrollCount,
          ToolCommandLineOptions.Get().SoundLoopUnrolling);

      VC.ConditionGeneration vcgen = null;

      try
      {
        vcgen = new VC.VCGen(this.AC.Program, ToolCommandLineOptions.Get().SimplifyLogFilePath,
          ToolCommandLineOptions.Get().SimplifyLogFileAppend, new List<Checker>());
      }
      catch (ProverException e)
      {
        Lockpwn.IO.Reporter.ErrorWriteLine("Fatal Error: ProverException: {0}", e);
        Environment.Exit((int)Outcome.FatalError);
      }

      int prevAssertionCount = vcgen.CumulativeAssertionCount;

      List<Counterexample> errors;

      DateTime start = new DateTime();
      if (ToolCommandLineOptions.Get().Trace)
      {
        start = DateTime.UtcNow;
        if (ToolCommandLineOptions.Get().Trace)
        {
          Output.PrintLine("");
          Output.PrintLine("Verifying {0} ...", this.AC.EntryPoint.Name.Substring(5));
        }
      }

      VC.VCGen.Outcome vcOutcome;
      try
      {
        vcOutcome = vcgen.VerifyImplementation(this.AC.EntryPoint, out errors);
      }
      catch (VC.VCGenException e)
      {
        Lockpwn.IO.Reporter.ReportBplError(this.AC.EntryPoint, String.Format("Error BP5010: {0}  Encountered in implementation {1}.",
          e.Message, this.AC.EntryPoint.Name), true, true);
        errors = null;
        vcOutcome = VC.VCGen.Outcome.Inconclusive;
      }
      catch (UnexpectedProverOutputException e)
      {
        Lockpwn.IO.Reporter.AdvisoryWriteLine("Advisory: {0} SKIPPED because of internal error: unexpected prover output: {1}",
          this.AC.EntryPoint.Name, e.Message);
        errors = null;
        vcOutcome = VC.VCGen.Outcome.Inconclusive;
      }

      string timeIndication = "";
      DateTime end = DateTime.UtcNow;
      TimeSpan elapsed = end - start;

      if (ToolCommandLineOptions.Get().Trace)
      {
        int poCount = vcgen.CumulativeAssertionCount - prevAssertionCount;
        timeIndication = string.Format("  [{0:F3} s, {1} proof obligation{2}]  ",
          elapsed.TotalSeconds, poCount, poCount == 1 ? "" : "s");
      }

      this.ProcessOutcome(this.AC.EntryPoint, vcOutcome, errors, timeIndication, this.Stats);

      if (vcOutcome == VC.VCGen.Outcome.Errors || ToolCommandLineOptions.Get().Trace)
        Console.Out.Flush();

      ToolCommandLineOptions.Get().TheProverFactory.Close();
      //      cce.NonNull(ToolCommandLineOptions.Get().TheProverFactory).Close();
      vcgen.Dispose();

      Lockpwn.IO.Reporter.WriteTrailer(this.Stats);
    }

    private void ProcessOutcome(Implementation impl, VC.VCGen.Outcome outcome, List<Counterexample> errors,
      string timeIndication, PipelineStatistics stats)
    {
      switch (outcome)
      {
        case VC.VCGen.Outcome.ReachedBound:
          Lockpwn.IO.Reporter.Inform(String.Format("{0}verified", timeIndication));
          Output.PrintLine(string.Format("Stratified Inlining: Reached recursion bound of {0}",
            ToolCommandLineOptions.Get().RecursionBound));
          stats.VerifiedCount++;
          break;

        case VC.VCGen.Outcome.Correct:
          if (ToolCommandLineOptions.Get().vcVariety == CommandLineOptions.VCVariety.Doomed)
          {
            Lockpwn.IO.Reporter.Inform(String.Format("{0}credible", timeIndication));
            stats.VerifiedCount++;
          }
          else
          {
            Lockpwn.IO.Reporter.Inform(String.Format("{0}verified", timeIndication));
            stats.VerifiedCount++;
          }
          break;

        case VC.VCGen.Outcome.TimedOut:
          stats.TimeoutCount++;
          Lockpwn.IO.Reporter.Inform(String.Format("{0}timed out", timeIndication));
          break;

        case VC.VCGen.Outcome.OutOfMemory:
          stats.OutOfMemoryCount++;
          Lockpwn.IO.Reporter.Inform(String.Format("{0}out of memory", timeIndication));
          break;

        case VC.VCGen.Outcome.Inconclusive:
          stats.InconclusiveCount++;
          Lockpwn.IO.Reporter.Inform(String.Format("{0}inconclusive", timeIndication));
          break;

        case VC.VCGen.Outcome.Errors:
          Contract.Assert(errors != null);
          if (ToolCommandLineOptions.Get().vcVariety == CommandLineOptions.VCVariety.Doomed)
          {
            Lockpwn.IO.Reporter.Inform(String.Format("{0}doomed", timeIndication));
            stats.ErrorCount++;
          }

          errors.Sort(new CounterexampleComparer());
          int errorCount = 0;

          foreach (Counterexample error in errors)
            errorCount += this.AC.GetErrorReporter().ReportCounterexample(error);

          if (errorCount == 0)
          {
            Lockpwn.IO.Reporter.Inform(String.Format("{0}verified", timeIndication));
            stats.VerifiedCount++;
          }
          else
          {
            Lockpwn.IO.Reporter.Inform(String.Format("{0}error{1}", timeIndication, errorCount == 1 ? "" : "s"));
            stats.ErrorCount += errorCount;
          }
          break;

        default:
          Contract.Assert(false); // unexpected outcome
          throw new cce.UnreachableException();
      }
    }
  }
}
