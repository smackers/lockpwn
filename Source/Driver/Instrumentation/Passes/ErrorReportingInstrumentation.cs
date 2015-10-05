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

namespace Lockpwn.Instrumentation
{
  internal class ErrorReportingInstrumentation : IPass
  {
    private AnalysisContext AC;
    private Thread Thread;
    private ExecutionTimer Timer;

    private int LogCounter;

    public ErrorReportingInstrumentation(AnalysisContext ac)
    {
      Contract.Requires(ac != null);
      this.AC = ac;
      this.LogCounter = 0;
    }

    public void Run()
    {
      if (ToolCommandLineOptions.Get().VerboseMode)
        Output.PrintLine("... ErrorReportingInstrumentation");

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var thread in this.AC.Threads)
      {
        this.Thread = thread;
        this.InstrumentAsyncFuncs();
        this.CleanUp();
      }

      if (ToolCommandLineOptions.Get().MeasureTime)
      {
        this.Timer.Stop();
        Output.PrintLine("..... [{0}]", this.Timer.Result());
      }
    }

    private void InstrumentAsyncFuncs()
    {
      foreach (var impl in this.AC.GetThreadSpecificFunctions(this.Thread))
      {
        if (this.AC.IsAToolFunc(impl.Name))
          continue;
        if (!Utilities.ShouldAccessFunction(impl.Name))
          continue;
        if (Utilities.ShouldSkipFromAnalysis(impl.Name))
          continue;

        this.InstrumentSourceLocationInfo(impl);
        this.InstrumentRaceCheckingCaptureStates(impl);
      }
    }

    private void InstrumentSourceLocationInfo(Implementation impl)
    {
      foreach (var b in impl.Blocks)
      {
        for (int idx = 0; idx < b.Cmds.Count; idx++)
        {
          if (!(b.Cmds[idx] is CallCmd)) continue;
          CallCmd call = b.Cmds[idx] as CallCmd;

          AssumeCmd assume = null;
          for (int i = idx; i >= 0; i--)
          {
            if (b.Cmds[i] is AssumeCmd)
            {
              assume = b.Cmds[i] as AssumeCmd;
              break;
            }
          }

          if (assume == null)
          {
            for (int i = idx; i < b.Cmds.Count; i++)
            {
              if (b.Cmds[i] is AssumeCmd)
              {
                assume = b.Cmds[i] as AssumeCmd;
                break;
              }
            }
          }

          if (call.callee.Contains("_UPDATE_CLS"))
          {
            call.Attributes = this.GetSourceLocationAttributes(
              assume.Attributes, call.Attributes);
          }
          else if (call.callee.Contains("_WRITE_LS_"))
          {
            call.Attributes = this.GetSourceLocationAttributes(
              assume.Attributes, call.Attributes);
          }
          else if (call.callee.Contains("_READ_LS_"))
          {
            call.Attributes = this.GetSourceLocationAttributes(
              assume.Attributes, call.Attributes);
          }
        }
      }
    }

    private void InstrumentRaceCheckingCaptureStates(Implementation impl)
    {
      if (impl.Equals(this.Thread.Function))
      {
        AssumeCmd assumeLogHead = new AssumeCmd(Token.NoToken, Expr.True);
        assumeLogHead.Attributes = new QKeyValue(Token.NoToken, "captureState",
          new List<object>() { this.Thread.Name + "_header_state" }, assumeLogHead.Attributes);
        impl.Blocks.First().Cmds.Insert(0, assumeLogHead);
      }

      foreach (var b in impl.Blocks)
      {
        List<Cmd> newCmds = new List<Cmd>();

        foreach (var c in b.Cmds)
        {
          if (!(c is CallCmd))
          {
            newCmds.Add(c);
            continue;
          }

          CallCmd call = c as CallCmd;

          if (!(call.callee.Contains("_WRITE_LS_") ||
              call.callee.Contains("_READ_LS_")))
          {
            newCmds.Add(call);
            continue;
          }

          AssumeCmd assume = new AssumeCmd(Token.NoToken, Expr.True);

          assume.Attributes = new QKeyValue(Token.NoToken, "column",
            new List<object>() { new LiteralExpr(Token.NoToken,
                BigNum.FromInt(QKeyValue.FindIntAttribute(call.Attributes, "column", -1)))
            }, null);
          assume.Attributes = new QKeyValue(Token.NoToken, "line",
            new List<object>() { new LiteralExpr(Token.NoToken,
                BigNum.FromInt(QKeyValue.FindIntAttribute(call.Attributes, "line", -1)))
            }, assume.Attributes);

          if (call.callee.Contains("WRITE"))
            assume.Attributes = new QKeyValue(Token.NoToken, "access",
              new List<object>() { "write" }, assume.Attributes);
          else if (call.callee.Contains("READ"))
            assume.Attributes = new QKeyValue(Token.NoToken, "access",
              new List<object>() { "read" }, assume.Attributes);

          assume.Attributes = new QKeyValue(Token.NoToken, "thread",
            new List<object>() { this.Thread.Name }, assume.Attributes);

          assume.Attributes = new QKeyValue(Token.NoToken, "captureState",
            new List<object>() { "access_state_" + this.LogCounter++ }, assume.Attributes);

          assume.Attributes = new QKeyValue(Token.NoToken, "resource",
            new List<object>() { "$" + call.callee.Split(new char[] { '$', '_' })[4] }, assume.Attributes);

          newCmds.Add(call);
          newCmds.Add(assume);
        }

        b.Cmds = newCmds;
      }
    }

    private QKeyValue GetSourceLocationAttributes(QKeyValue attributes, QKeyValue previousAttributes)
    {
      QKeyValue line, col;
      QKeyValue curr = attributes;

      while (curr != null)
      {
        if (curr.Key.Equals("sourceloc")) break;
        curr = curr.Next;
      }
      Contract.Requires(curr.Key.Equals("sourceloc") && curr.Params.Count == 3);

      col = new QKeyValue(Token.NoToken, "column",
        new List<object>() { new LiteralExpr(Token.NoToken,
            BigNum.FromInt(Int32.Parse(string.Format("{0}", curr.Params[2]))))
        }, previousAttributes);
      line = new QKeyValue(Token.NoToken, "line",
        new List<object>() { new LiteralExpr(Token.NoToken,
            BigNum.FromInt(Int32.Parse(string.Format("{0}", curr.Params[1]))))
        }, col);

      return line;
    }

    private void CleanUp()
    {
      foreach (var impl in this.AC.GetThreadSpecificFunctions(this.Thread))
      {
        foreach (Block b in impl.Blocks)
        {
          b.Cmds.RemoveAll(val => (val is AssumeCmd) && (val as AssumeCmd).Attributes != null &&
            (val as AssumeCmd).Attributes.Key.Equals("sourceloc"));
        }
      }
    }
  }
}
