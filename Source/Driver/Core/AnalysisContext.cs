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
using Microsoft.Boogie.GraphUtil;

namespace Lockpwn
{
  /// <summary>
  /// Class implementing an analysis context.
  /// </summary>
  internal class AnalysisContext : CheckingContext
  {
    #region static fields

    /// <summary>
    /// The error reporter associated with the analysis context.
    /// </summary>
    private static ErrorReporter ErrorReporter;

    #endregion

    #region fields

    internal Microsoft.Boogie.Program BoogieProgram;
    internal ResolutionContext ResContext;

    internal List<Declaration> TopLevelDeclarations;

    internal Thread MainThread;
    internal HashSet<Thread> Threads;

    internal List<Lock> Locks;
    internal List<Lockset> CurrentLocksets;
    internal List<Lockset> MemoryLocksets;

    internal HashSet<GlobalVariable> SharedMemoryRegions;
    internal Dictionary<Thread, HashSet<GlobalVariable>> ThreadMemoryRegions;

    internal Microsoft.Boogie.Type MemoryModelType;

    internal Implementation EntryPoint
    {
      get;
      private set;
    }

    #endregion

    #region internal API

    /// <summary>
    /// Static constructor.
    /// </summary>
    static AnalysisContext()
    {
      AnalysisContext.ErrorReporter = new ErrorReporter();
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="program">Program</param>
    /// <param name="rc">ResolutionContext</param>
    private AnalysisContext(Microsoft.Boogie.Program program, ResolutionContext rc)
      : base((IErrorSink)null)
    {
      Contract.Requires(program != null);
      Contract.Requires(rc != null);

      this.BoogieProgram = program;
      this.ResContext = rc;

      this.MainThread = null;
      this.Threads = new HashSet<Thread>();

      this.Locks = new List<Lock>();
      this.CurrentLocksets = new List<Lockset>();
      this.MemoryLocksets = new List<Lockset>();

      this.SharedMemoryRegions = new HashSet<GlobalVariable>();
      this.ThreadMemoryRegions = new Dictionary<Thread, HashSet<GlobalVariable>>();

      this.MemoryModelType = Microsoft.Boogie.Type.Int;

      this.ResetToProgramTopLevelDeclarations();
      this.DetectEntryPoint();
    }

    /// <summary>
    /// Creates an analysis context using information from the given analysis context.
    /// </summary>
    /// <param name="program">Program</param>
    /// <param name="rc">ResolutionContext</param>
    /// <returns>AnalysisContext</returns>
    internal static AnalysisContext Create(Microsoft.Boogie.Program program, ResolutionContext rc)
    {
      return new AnalysisContext(program, rc);
    }

    /// <summary>
    /// Creates an analysis context using information from the given analysis context.
    /// </summary>
    /// <param name="program">Program</param>
    /// <param name="rc">ResolutionContext</param>
    /// <param name="ac">AnalysisContext</param>
    /// <returns>AnalysisContext</returns>
    internal static AnalysisContext CreateWithContext(Microsoft.Boogie.Program program,
      ResolutionContext rc, AnalysisContext ac)
    {
      var newAc = new AnalysisContext(program, rc);

      newAc.MainThread = ac.MainThread.Clone(newAc);
      foreach (var thread in ac.Threads)
      {
        newAc.Threads.Add(thread.Clone(newAc));
      }

      newAc.Locks = ac.Locks;
      newAc.CurrentLocksets = ac.CurrentLocksets;
      newAc.MemoryLocksets = ac.MemoryLocksets;

      newAc.SharedMemoryRegions = ac.SharedMemoryRegions;
      newAc.ThreadMemoryRegions = ac.ThreadMemoryRegions;

      return newAc;
    }

    /// <summary>
    /// Returns the error reporter associated with this analysis context.
    /// </summary>
    /// <returns>ErrorReporter</returns>
    internal ErrorReporter GetErrorReporter()
    {
      return AnalysisContext.ErrorReporter;
    }

    /// <summary>
    /// Eliminates the dead variables.
    /// </summary>
    internal void EliminateDeadVariables()
    {
      ExecutionEngine.EliminateDeadVariables(this.BoogieProgram);
    }

    /// <summary>
    /// Eliminates the assertions.
    /// </summary>
    internal void EliminateAssertions()
    {
      foreach (var impl in this.TopLevelDeclarations.OfType<Implementation>())
      {
        foreach (var block in impl.Blocks)
        {
          block.Cmds.RemoveAll(val => val is AssertCmd);
        }
      }
    }

    /// <summary>
    /// Eliminates the non-candidate invariant inference assertions.
    /// </summary>
    internal void EliminateNonInvariantInferenceAssertions()
    {
      foreach (var impl in this.TopLevelDeclarations.OfType<Implementation>())
      {
        foreach (var block in impl.Blocks)
        {
          block.Cmds.RemoveAll(val => val is AssertCmd &&
            !QKeyValue.FindBoolAttribute((val as AssertCmd).Attributes, "candidate"));
        }
      }
    }

    /// <summary>
    /// Inlines the program.
    /// </summary>
    internal void Inline()
    {
      ExecutionEngine.Inline(this.BoogieProgram);
    }

    internal void InlineThread(Thread thread)
    {
      foreach (var impl in this.GetThreadSpecificFunctions(thread))
      {
        if (impl.Equals(thread.Function) && thread.IsMain)
          continue;
        impl.Proc.Attributes = new QKeyValue(Token.NoToken,
          "inline", new List<object>{ new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) },
          impl.Proc.Attributes);
        impl.Attributes = new QKeyValue(Token.NoToken,
          "inline", new List<object>{ new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) },
          impl.Attributes);
      }
    }

    internal void InlineThreadHelpers()
    {
      foreach (var impl in this.TopLevelDeclarations.OfType<Implementation>())
      {
        if (!Utilities.IsPThreadFunction(impl.Name))
          continue;
        impl.Proc.Attributes = new QKeyValue(Token.NoToken,
          "inline", new List<object>{ new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) },
          impl.Proc.Attributes);
        impl.Attributes = new QKeyValue(Token.NoToken,
          "inline", new List<object>{ new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) },
          impl.Attributes);
      }
    }

    internal Graph<Block> GetImplementationGraph(Implementation impl)
    {
      return Microsoft.Boogie.Program.GraphFromImpl(impl);
    }

    internal List<Implementation> GetThreadSpecificFunctions(Thread thread)
    {
      var functions = new List<Implementation>();
      functions.Add(thread.Function);

      foreach (var impl in this.TopLevelDeclarations.OfType<Implementation>())
      {
        if (QKeyValue.FindStringAttribute(impl.Attributes, "tag") != null &&
            QKeyValue.FindStringAttribute(impl.Attributes, "tag").Equals(thread.Name))
        {
          functions.Add(impl);
        }
      }

      return functions;
    }

    internal List<Variable> GetLockVariables()
    {
      return this.TopLevelDeclarations.OfType<Variable>().ToList().
        FindAll(val => QKeyValue.FindBoolAttribute(val.Attributes, "lock"));
    }

    internal List<Variable> GetCurrentLocksetVariables()
    {
      return this.TopLevelDeclarations.OfType<Variable>().ToList().
        FindAll(val => QKeyValue.FindBoolAttribute(val.Attributes, "current_lockset"));
    }

    internal List<Variable> GetMemoryLocksetVariables()
    {
      return this.TopLevelDeclarations.OfType<Variable>().ToList().
        FindAll(val => QKeyValue.FindBoolAttribute(val.Attributes, "lockset"));
    }

    internal List<Variable> GetWriteAccessCheckingVariables()
    {
      return this.TopLevelDeclarations.OfType<Variable>().ToList().
        FindAll(val => QKeyValue.FindBoolAttribute(val.Attributes, "access_checking") &&
          val.Name.Contains("WRITTEN_"));
    }

    internal List<Variable> GetReadAccessCheckingVariables()
    {
      return this.TopLevelDeclarations.OfType<Variable>().ToList().
        FindAll(val => QKeyValue.FindBoolAttribute(val.Attributes, "access_checking") &&
          val.Name.Contains("READ_"));
    }

    internal List<Variable> GetAccessWatchdogConstants()
    {
      return this.TopLevelDeclarations.OfType<Variable>().ToList().
        FindAll(val => QKeyValue.FindBoolAttribute(val.Attributes, "watchdog"));
    }

    internal Implementation GetImplementation(string name)
    {
      Contract.Requires(name != null);
      var impl = (this.TopLevelDeclarations.FirstOrDefault(val => (val is Implementation) &&
        (val as Implementation).Name.Equals(name)) as Implementation);
      return impl;
    }

    internal Constant GetConstant(string name)
    {
      Contract.Requires(name != null);
      var cons = (this.TopLevelDeclarations.FirstOrDefault(val => (val is Constant) &&
        (val as Constant).Name.Equals(name)) as Constant);
      return cons;
    }

    internal Axiom GetExternalAxiom(string name)
    {
      Contract.Requires(name != null);
      var axiom = (this.TopLevelDeclarations.FirstOrDefault(val => (val is Axiom) &&
        (val as Axiom).Expr.ToString().Equals("$isExternal(" + name + ")")) as Axiom);
      return axiom;
    }

    internal string GetWriteAccessVariableName(Thread thread, string name)
    {
      return "WRITTEN_" + name + "_$" + thread.Name;
    }

    internal string GetReadAccessVariableName(Thread thread, string name)
    {
      return "READ_" + name + "_$" + thread.Name;
    }

    internal string GetAccessWatchdogConstantName(string name)
    {
      return "WATCHED_ACCESS_" + name;
    }

    internal bool IsAToolVariable(Variable v)
    {
      Contract.Requires(v != null);
      if (QKeyValue.FindBoolAttribute(v.Attributes, "lock") ||
        QKeyValue.FindBoolAttribute(v.Attributes, "current_lockset") ||
        QKeyValue.FindBoolAttribute(v.Attributes, "lockset") ||
        QKeyValue.FindBoolAttribute(v.Attributes, "access_checking") ||
        QKeyValue.FindBoolAttribute(v.Attributes, "existential") ||
        QKeyValue.FindBoolAttribute(v.Attributes, "watchdog"))
        return true;
      return false;
    }

    internal bool IsAToolFunc(string name)
    {
      Contract.Requires(name != null);
      if (name.Contains("_UPDATE_CLS_") ||
        name.Contains("_WRITE_LS_") || name.Contains("_READ_LS_") ||
        name.Contains("_CHECK_WRITE_LS_") || name.Contains("_CHECK_READ_LS_") ||
        name.Contains("_NO_OP_"))
        return true;
      return false;
    }

    internal bool IsCalledByAnyFunc(string name)
    {
      Contract.Requires(name != null);
      foreach (var impl in this.TopLevelDeclarations.OfType<Implementation>())
      {
        foreach (var block in impl.Blocks)
        {
          foreach (var cmd in block.Cmds)
          {
            if (cmd is CallCmd)
            {
              if ((cmd as CallCmd).callee.Equals(name))
                return true;
              foreach (var expr in (cmd as CallCmd).Ins)
              {
                if (!(expr is IdentifierExpr))
                  continue;
                if ((expr as IdentifierExpr).Name.Equals(name))
                  return true;
              }
            }
            else if (cmd is AssignCmd)
            {
              foreach (var rhs in (cmd as AssignCmd).Rhss)
              {
                if (!(rhs is IdentifierExpr))
                  continue;
                if ((rhs as IdentifierExpr).Name.Equals(name))
                  return true;
              }
            }
          }
        }
      }

      return false;
    }

    /// <summary>
    /// Resets to program's top level declarations.
    /// </summary>
    internal void ResetToProgramTopLevelDeclarations()
    {
      this.TopLevelDeclarations = this.BoogieProgram.TopLevelDeclarations.ToArray().ToList();
    }

    /// <summary>
    /// Detects the entry point of the program.
    /// </summary>
    private void DetectEntryPoint()
    {
      this.EntryPoint = this.TopLevelDeclarations.OfType<Implementation>().ToList().
        FirstOrDefault(val => QKeyValue.FindBoolAttribute(val.Attributes, "entrypoint"));
      if (this.EntryPoint == null)
      {
        this.EntryPoint = this.GetImplementation("main");
        if (this.EntryPoint == null)
        {
          Lockpwn.IO.Reporter.ErrorWriteLine("Unable to detect entrypoint or main function.");
          Environment.Exit((int)Outcome.ParsingError);
        }

        this.EntryPoint.Proc.Attributes = new QKeyValue(Token.NoToken, "entrypoint",
          new List<object>() { }, this.EntryPoint.Proc.Attributes);
        this.EntryPoint.Attributes = new QKeyValue(Token.NoToken, "entrypoint",
          new List<object>() { }, this.EntryPoint.Attributes);
      }
    }

    #endregion
  }
}
