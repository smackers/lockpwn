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

using Lockpwn.IO;

namespace Lockpwn.Analysis
{
  internal class SharedStateAbstraction : IPass
  {
    private AnalysisContext AC;
    private ExecutionTimer Timer;

    internal SharedStateAbstraction(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
    }

    void IPass.Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... SharedStateAbstraction");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var impl in this.AC.TopLevelDeclarations.OfType<Implementation>().ToList())
      {
        this.AbstractReadAccesses(impl);
        this.AbstractWriteAccesses(impl);
//        this.CleanUpModset(impl);
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void AbstractReadAccesses(Implementation impl)
    {
      if (this.AC.IsAToolFunc(impl.Name))
        return;
      if (!Utilities.ShouldAccessFunction(impl.Name))
        return;
      if (Utilities.ShouldSkipFromAnalysis(impl.Name))
        return;

      foreach (var b in impl.Blocks)
      {
        for (int k = 0; k < b.Cmds.Count; k++)
        {
          if (!(b.Cmds[k] is AssignCmd)) continue;

          foreach (var rhs in (b.Cmds[k] as AssignCmd).Rhss.OfType<NAryExpr>())
          {
            if (!(rhs.Fun.FunctionName.StartsWith("$load.")) || rhs.Args.Count != 2 ||
              !((rhs.Args[0] as IdentifierExpr).Name.StartsWith("$M.")))
              continue;

            Variable v = (b.Cmds[k] as AssignCmd).Lhss[0].DeepAssignedVariable;
            HavocCmd havoc = new HavocCmd(Token.NoToken,
              new List<IdentifierExpr> { new IdentifierExpr(v.tok, v) });
            b.Cmds[k] = havoc;
          }

          if (!(b.Cmds[k] is AssignCmd)) continue;
          foreach (var rhs in (b.Cmds[k] as AssignCmd).Rhss.OfType<IdentifierExpr>())
          {
            if (!(rhs.Name.StartsWith("$M.")))
              continue;

            Variable v = (b.Cmds[k] as AssignCmd).Lhss[0].DeepAssignedVariable;
            HavocCmd havoc = new HavocCmd(Token.NoToken,
              new List<IdentifierExpr> { new IdentifierExpr(v.tok, v) });
            b.Cmds[k] = havoc;
          }
        }
      }
    }

    private void AbstractWriteAccesses(Implementation impl)
    {
      if (this.AC.IsAToolFunc(impl.Name))
        return;
      if (!Utilities.ShouldAccessFunction(impl.Name))
        return;
      if (Utilities.ShouldSkipFromAnalysis(impl.Name))
        return;

      foreach (var b in impl.Blocks)
      {
        List<Cmd> cmdsToRemove = new List<Cmd>();

        for (int k = 0; k < b.Cmds.Count; k++)
        {
          if (!(b.Cmds[k] is AssignCmd)) continue;

          foreach (var lhs in (b.Cmds[k] as AssignCmd).Lhss.OfType<SimpleAssignLhs>())
          {
            if (!(lhs.DeepAssignedIdentifier.Name.StartsWith("$M.")))
              continue;

            cmdsToRemove.Add(b.Cmds[k]);
          }
        }

        foreach (var c in cmdsToRemove) b.Cmds.Remove(c);
      }
    }

//    private void CleanUpModset(Implementation impl)
//    {
//      if (this.AC.IsAToolFunc(impl.Name))
//        return;
//      if (!Utilities.ShouldAccessFunction(impl.Name))
//        return;
//      if (Utilities.ShouldSkipFromAnalysis(impl.Name))
//        return;
//
//      region.Procedure().Modifies.RemoveAll(val => !(val.Name.Equals("$Alloc") ||
//        val.Name.Equals("$CurrAddr") || val.Name.Equals("CLS") ||
//        val.Name.Contains("LS_$") ||
//        val.Name.Contains("WRITTEN_$") || val.Name.Contains("READ_$")));
//    }
  }
}
