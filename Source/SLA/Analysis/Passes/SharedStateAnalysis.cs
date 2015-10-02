﻿//===-----------------------------------------------------------------------==//
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
  internal class SharedStateAnalysis : IPass
  {
    private AnalysisContext AC;
    private Thread Thread;
    private ExecutionTimer Timer;

    private HashSet<Implementation> AlreadyAnalysed;

    internal SharedStateAnalysis(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;

      this.AlreadyAnalysed = new HashSet<Implementation>();
    }

    /// <summary>
    /// Runs a shared state analysis pass.
    /// </summary>
    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... SharedStateAnalysis");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var thread in this.AC.Threads)
      {
        this.AnalyseThread(thread);
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void AnalyseThread(Thread thread)
    {
      this.Thread = thread;
      this.AC.ThreadMemoryRegions.Add(this.Thread, new HashSet<GlobalVariable>());

      foreach (var impl in this.AC.GetThreadSpecificFunctions(thread))
      {
        this.IdentifySharedMemoryRegions(impl);
      }

      if (ToolCommandLineOptions.Get().SuperVerboseMode)
      {
        string accesses = "";
        foreach (var mr in this.AC.ThreadMemoryRegions[this.Thread])
          accesses += " '" + mr.Name + "'";

        if (this.AC.ThreadMemoryRegions[this.Thread].Count == 0)
          Output.PrintLine("..... '{0}' accesses no memory regions", this.Thread.Name, accesses);
        else if (this.AC.ThreadMemoryRegions[this.Thread].Count == 1)
          Output.PrintLine("..... '{0}' accesses{1}", this.Thread.Name, accesses);
        else
          Output.PrintLine("..... '{0}' accesses{1}", this.Thread.Name, accesses);
      }
    }

    /// <summary>
    /// Performs an analysis to identify shared memory regions.
    /// </summary>
    /// <param name="impl">Implementation</param>
    private void IdentifySharedMemoryRegions(Implementation impl)
    {
      if (this.AlreadyAnalysed.Contains(impl))
        return;
      this.AlreadyAnalysed.Add(impl);

      var memoryRegions = new List<GlobalVariable>();

      foreach (Block b in impl.Blocks)
      {
        foreach (var cmd in b.Cmds.OfType<AssignCmd>())
        {
          foreach (var lhs in cmd.Lhss.OfType<MapAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.StartsWith("$M.")) ||
              !(lhs.Map is SimpleAssignLhs) || lhs.Indexes.Count != 1)
              continue;

            var v = this.AC.TopLevelDeclarations.OfType<GlobalVariable>().ToList().
              Find(val => val.Name.Equals(lhs.DeepAssignedIdentifier.Name));

            if (!memoryRegions.Any(val => val.Name.Equals(v.Name)))
              memoryRegions.Add(v);
          }

          foreach (var lhs in cmd.Lhss.OfType<SimpleAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.StartsWith("$M.")))
              continue;

            var v = this.AC.TopLevelDeclarations.OfType<GlobalVariable>().ToList().
              Find(val => val.Name.Equals(lhs.DeepAssignedIdentifier.Name));

            if (!memoryRegions.Any(val => val.Name.Equals(v.Name)))
              memoryRegions.Add(v);
          }

          foreach (var rhs in cmd.Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun is MapSelect) || rhs.Args.Count != 2 ||
              !((rhs.Args[0] as IdentifierExpr).Name.StartsWith("$M.")))
              continue;

            var v = this.AC.TopLevelDeclarations.OfType<GlobalVariable>().ToList().
              Find(val => val.Name.Equals((rhs.Args[0] as IdentifierExpr).Name));

            if (!memoryRegions.Any(val => val.Name.Equals(v.Name)))
              memoryRegions.Add(v);
          }

          foreach (var rhs in cmd.Rhss.OfType<IdentifierExpr>())
          {
            if (!rhs.Name.StartsWith("$M."))
              continue;

            var v = this.AC.TopLevelDeclarations.OfType<GlobalVariable>().ToList().
              Find(val => val.Name.Equals(rhs.Name));

            if (!memoryRegions.Any(val => val.Name.Equals(v.Name)))
              memoryRegions.Add(v);
          }
        }
      }

      foreach (var mr in memoryRegions)
      {
        if (!this.AC.SharedMemoryRegions.Contains(mr))
          this.AC.SharedMemoryRegions.Add(mr);
        if (!this.AC.ThreadMemoryRegions[this.Thread].Contains(mr))
          this.AC.ThreadMemoryRegions[this.Thread].Add(mr);
      }
    }
  }
}
