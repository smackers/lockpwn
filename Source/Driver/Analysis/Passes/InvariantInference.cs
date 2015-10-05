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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Microsoft.Boogie;
using Microsoft.Basetypes;
using Microsoft.Boogie.Houdini;

using Lockpwn.IO;

namespace Lockpwn.Analysis
{
  internal class HoudiniInvariantInference : IPass
  {
    private AnalysisContext AC;
    private AnalysisContext SummarizedAC;
    private Houdini Houdini;
    private HoudiniOutcome Outcome;
    private ExecutionTimer Timer;

    internal HoudiniInvariantInference(AnalysisContext ac, AnalysisContext summarizedAc)
    {
      Contract.Requires(ac != null && summarizedAc != null);
      this.AC = ac;
      this.SummarizedAC = summarizedAc;
    }

    /// <summary>
    /// Runs a Houdini invariant inference analysis pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... InvariantInference-Houdini");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      this.PerformHoudini();
      this.ApplyInvariants();

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void PerformHoudini()
    {
      var houdiniStats = new HoudiniSession.HoudiniStatistics();
      this.Houdini = new Houdini(this.AC.Program, houdiniStats);
      this.Outcome = this.Houdini.PerformHoudiniInference();

      if (CommandLineOptions.Clo.PrintAssignment)
      {
        Output.PrintLine("..... Assignment computed by Houdini:");
        foreach (var x in this.Outcome.assignment)
        {
          Output.PrintLine(x.Key + " = " + x.Value);
        }
      }

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        int numTrueAssigns = 0;
        foreach (var x in this.Outcome.assignment)
        {
          if (x.Value)
          {
            numTrueAssigns++;
          }
        }

        Output.PrintLine("..... Number of true assignments = " + numTrueAssigns);
        Output.PrintLine("..... Number of false assignments = " + (this.Outcome.assignment.Count - numTrueAssigns));
        Output.PrintLine("..... Prover time = " + houdiniStats.proverTime.ToString("F2"));
        Output.PrintLine("..... Unsat core prover time = " + houdiniStats.unsatCoreProverTime.ToString("F2"));
        Output.PrintLine("..... Number of prover queries = " + houdiniStats.numProverQueries);
        Output.PrintLine("..... Number of unsat core prover queries = " + houdiniStats.numUnsatCoreProverQueries);
        Output.PrintLine("..... Number of unsat core prunings = " + houdiniStats.numUnsatCorePrunings);
      }
    }

    private void ApplyInvariants()
    {
      if (this.Houdini != null) {
        Houdini.ApplyAssignment(this.SummarizedAC.Program, this.Outcome);
        this.Houdini.Close();
        ToolCommandLineOptions.Get().TheProverFactory.Close();
      }
    }
  }
}
