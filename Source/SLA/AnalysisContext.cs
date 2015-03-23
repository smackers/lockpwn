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

namespace Lockpwn
{
  internal class AnalysisContext : CheckingContext
  {
    #region fields

    internal Microsoft.Boogie.Program Program;
    internal ResolutionContext ResContext;

    internal List<Declaration> TopLevelDeclarations;

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

    internal Implementation Checker
    {
      get;
      private set;
    }

    #endregion

    #region internal API

    internal AnalysisContext(Microsoft.Boogie.Program program, ResolutionContext rc)
      : base((IErrorSink)null)
    {
      Contract.Requires(program != null);
      Contract.Requires(rc != null);

      this.Program = program;
      this.ResContext = rc;

      this.Threads = new HashSet<Thread>();

      this.Locks = new List<Lock>();
      this.CurrentLocksets = new List<Lockset>();
      this.MemoryLocksets = new List<Lockset>();

      this.SharedMemoryRegions = new HashSet<GlobalVariable>();
      this.ThreadMemoryRegions = new Dictionary<Thread, HashSet<GlobalVariable>>();

      this.MemoryModelType = Microsoft.Boogie.Type.Int;

      this.ResetToProgramTopLevelDeclarations();

      this.EntryPoint = this.TopLevelDeclarations.OfType<Implementation>().ToList().
        FirstOrDefault(val => QKeyValue.FindBoolAttribute(val.Attributes, "entrypoint"));

      this.Checker = this.TopLevelDeclarations.OfType<Implementation>().ToList().
        FirstOrDefault(val => val.Name.Equals("lockpwn$checker"));
    }

    internal void EliminateDeadVariables()
    {
      ExecutionEngine.EliminateDeadVariables(this.Program);
    }

    internal void Inline()
    {
      ExecutionEngine.Inline(this.Program);
    }

    internal void InlineThread(Thread thread)
    {
      foreach (var impl in this.GetThreadSpecificFunctions(thread))
      {
        if (impl.Equals(thread.Function))
          continue;
        impl.Proc.Attributes = new QKeyValue(Token.NoToken,
          "inline", new List<object>{ new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) },
          impl.Proc.Attributes);
        impl.Attributes = new QKeyValue(Token.NoToken,
          "inline", new List<object>{ new LiteralExpr(Token.NoToken, BigNum.FromInt(1)) },
          impl.Attributes);
      }
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

    internal void ResetAnalysisContext()
    {
      this.Locks.Clear();
      this.CurrentLocksets.Clear();
      this.MemoryLocksets.Clear();
      this.TopLevelDeclarations = this.Program.TopLevelDeclarations.ToArray().ToList();
    }

    internal void ResetToProgramTopLevelDeclarations()
    {
      this.TopLevelDeclarations = this.Program.TopLevelDeclarations.ToArray().ToList();
    }

    #endregion
  }
}
