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
using Microsoft.Boogie.Houdini;

using Lockpwn.Analysis;
using Lockpwn.IO;

namespace Lockpwn
{
  internal sealed class Cruncher
  {
    private AnalysisContext AC;
    private AnalysisContext PostAC;
    private Houdini Houdini;
    private HoudiniOutcome Outcome;
    private ExecutionTimer Timer;

    internal Cruncher(AnalysisContext ac, AnalysisContext postAc)
    {
      Contract.Requires(ac != null && postAc != null);
      this.AC = ac;
      this.PostAC = postAc;
    }

    internal void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine(". Cruncher");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.AC.EliminateDeadVariables();
      this.AC.Inline();

      this.PerformHoudini();
      this.ApplyInvariants();

      //      ModelCleaner.RemoveGenericTopLevelDeclerations(this.PostAC, this.EP);
      //      ModelCleaner.RemoveUnusedTopLevelDeclerations(this.AC);
      //      ModelCleaner.RemoveGlobalLocksets(this.PostAC);
      ModelCleaner.RemoveExistentials(this.PostAC);

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("... Cruncher done [{0}]", this.Timer.Result());
      }

      Lockpwn.IO.BoogieProgramEmitter.Emit(this.PostAC.TopLevelDeclarations, ToolCommandLineOptions.Get().
        Files[ToolCommandLineOptions.Get().Files.Count - 1], "summarised", "bpl");
    }

    private void PerformHoudini()
    {
      var houdiniStats = new HoudiniSession.HoudiniStatistics();
      this.Houdini = new Houdini(this.AC.Program, houdiniStats);
      this.Outcome = this.Houdini.PerformHoudiniInference();

      if (CommandLineOptions.Clo.PrintAssignment)
      {
        Output.PrintLine("Assignment computed by Houdini:");
        foreach (var x in this.Outcome.assignment)
        {
          Output.PrintLine(x.Key + " = " + x.Value);
        }
      }

      if (CommandLineOptions.Clo.Trace)
      {
        int numTrueAssigns = 0;
        foreach (var x in this.Outcome.assignment)
        {
          if (x.Value)
          {
            numTrueAssigns++;
          }
        }

        Output.PrintLine("Number of true assignments = " + numTrueAssigns);
        Output.PrintLine("Number of false assignments = " + (this.Outcome.assignment.Count - numTrueAssigns));
        Output.PrintLine("Prover time = " + houdiniStats.proverTime.ToString("F2"));
        Output.PrintLine("Unsat core prover time = " + houdiniStats.unsatCoreProverTime.ToString("F2"));
        Output.PrintLine("Number of prover queries = " + houdiniStats.numProverQueries);
        Output.PrintLine("Number of unsat core prover queries = " + houdiniStats.numUnsatCoreProverQueries);
        Output.PrintLine("Number of unsat core prunings = " + houdiniStats.numUnsatCorePrunings);
      }
    }

    private void ApplyInvariants()
    {
      if (this.Houdini != null) {
        Houdini.ApplyAssignment(this.PostAC.Program, this.Outcome);
        this.Houdini.Close();
        ToolCommandLineOptions.Get().TheProverFactory.Close();
      }
    }

    private bool AllImplementationsValid(HoudiniOutcome outcome)
    {
      foreach (var vcgenOutcome in outcome.implementationOutcomes.Values.Select(i => i.outcome))
      {
        if (vcgenOutcome != VC.VCGen.Outcome.Correct)
        {
          return false;
        }
      }
      return true;
    }
  }
}
