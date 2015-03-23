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
using System.Runtime.InteropServices;

using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace Lockpwn.Analysis
{
  internal class ModelCleaner
  {
//    internal static void RemoveGenericTopLevelDeclerations(AnalysisContext ac, EntryPoint ep)
//    {
//      List<string> toRemove = new List<string>();
//      List<string> tagged = new List<string>();
//
//      foreach (var proc in ac.TopLevelDeclarations.OfType<Procedure>())
//      {
//        if (QKeyValue.FindBoolAttribute(proc.Attributes, "entrypoint") ||
//            (QKeyValue.FindStringAttribute(proc.Attributes, "tag") != null &&
//            QKeyValue.FindStringAttribute(proc.Attributes, "tag").Equals(ep.Name)))
//        {
//          tagged.Add(proc.Name);
//          continue;
//        }
//        if (ac.IsAToolFunc(proc.Name))
//          continue;
//        toRemove.Add(proc.Name);
//      }
//
//      foreach (var str in toRemove)
//      {
//        ac.TopLevelDeclarations.RemoveAll(val =>
//          (val is Constant) && (val as Constant).Name.Equals(str));
//        ac.TopLevelDeclarations.RemoveAll(val =>
//          (val is Procedure) && (val as Procedure).Name.Equals(str));
//        ac.TopLevelDeclarations.RemoveAll(val =>
//          (val is Implementation) && (val as Implementation).Name.Equals(str));
//      }
//
//      ac.TopLevelDeclarations.RemoveAll(val =>
//        (val is Procedure) && ((val as Procedure).Name.Equals("$malloc") ||
//          (val as Procedure).Name.Equals("$free") ||
//          (val as Procedure).Name.Equals("$alloca")));
//
//      ac.TopLevelDeclarations.RemoveAll(val =>
//        (val is Variable) && !ac.IsAToolVariable(val as Variable) &&
//        !tagged.Exists(str => str.Equals((val as Variable).Name)));
//
//      ac.TopLevelDeclarations.RemoveAll(val => (val is Axiom));
//      ac.TopLevelDeclarations.RemoveAll(val => (val is Function));
//      ac.TopLevelDeclarations.RemoveAll(val => (val is TypeCtorDecl));
//      ac.TopLevelDeclarations.RemoveAll(val => (val is TypeSynonymDecl));
//    }

    internal static void RemoveEntryPointSpecificTopLevelDeclerations(AnalysisContext ac)
    {
      HashSet<string> toRemove = new HashSet<string>();

      foreach (var impl in ac.TopLevelDeclarations.OfType<Implementation>())
      {
        if (impl.Name.Equals(ac.EntryPoint))
          continue;
        if (QKeyValue.FindBoolAttribute(impl.Attributes, "checker"))
          continue;
        if (impl.Name.StartsWith("$memcpy") || impl.Name.StartsWith("memcpy_fromio") ||
            impl.Name.StartsWith("$memset") ||
            impl.Name.StartsWith("$malloc") || impl.Name.StartsWith("$alloca") ||
            impl.Name.StartsWith("$free"))
          continue;

        toRemove.Add(impl.Name);
      }

      foreach (var str in toRemove)
      {
        ac.TopLevelDeclarations.RemoveAll(val => (val is Constant) &&
          (val as Constant).Name.Equals(str));
        ac.TopLevelDeclarations.RemoveAll(val => (val is Procedure) &&
          (val as Procedure).Name.Equals(str));
        ac.TopLevelDeclarations.RemoveAll(val => (val is Implementation) &&
          (val as Implementation).Name.Equals(str));
      }
    }

    internal static void RemoveGlobalLocksets(AnalysisContext ac)
    {
      List<Variable> toRemove = new List<Variable>();

      foreach (var v in ac.TopLevelDeclarations.OfType<Variable>())
      {
        if (!ac.IsAToolVariable(v))
          continue;
        if (QKeyValue.FindBoolAttribute(v.Attributes, "existential"))
          continue;
        toRemove.Add(v);
      }

      foreach (var v in toRemove)
      {
        ac.TopLevelDeclarations.RemoveAll(val =>
          (val is Variable) && (val as Variable).Name.Equals(v.Name));
      }
    }

    internal static void RemoveExistentials(AnalysisContext ac)
    {
      List<Variable> toRemove = new List<Variable>();

      foreach (var v in ac.TopLevelDeclarations.OfType<Variable>())
      {
        if (!QKeyValue.FindBoolAttribute(v.Attributes, "existential"))
          continue;
        toRemove.Add(v);
      }

      foreach (var v in toRemove)
      {
        ac.TopLevelDeclarations.RemoveAll(val =>
          (val is Variable) && (val as Variable).Name.Equals(v.Name));
      }
    }

    internal static void RemoveAssumesFromImplementation(Implementation impl)
    {
      foreach (var b in impl.Blocks)
      {
        b.Cmds.RemoveAll(cmd => cmd is AssumeCmd);
      }
    }

    internal static void RemoveImplementations(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => val is Implementation);
    }

    internal static void RemoveConstants(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => val is Constant);
    }

    internal static void RemoveWhoopFunctions(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => (val is Implementation) &&
        ac.IsAToolFunc((val as Implementation).Name));
      ac.TopLevelDeclarations.RemoveAll(val => (val is Procedure) &&
        ac.IsAToolFunc((val as Procedure).Name));
    }

    internal static void RemoveCorralFunctions(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => (val is Procedure) &&
        (val as Procedure).Name.Equals("corral_atomic_begin"));
      ac.TopLevelDeclarations.RemoveAll(val => (val is Procedure) &&
        (val as Procedure).Name.Equals("corral_atomic_end"));
      ac.TopLevelDeclarations.RemoveAll(val => (val is Procedure) &&
        (val as Procedure).Name.Equals("corral_getThreadID"));
    }

    internal static void RemoveModelledProcedureBodies(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.RemoveAll(val => (val is Implementation) &&
        (val as Implementation).Name.Equals("pthread_mutex_lock"));
      ac.TopLevelDeclarations.RemoveAll(val => (val is Implementation) &&
        (val as Implementation).Name.Equals("pthread_mutex_unlock"));
    }

    internal static void RemoveOriginalInitFunc(AnalysisContext ac)
    {
      ac.TopLevelDeclarations.Remove(ac.GetConstant(ac.EntryPoint.Name));
      ac.TopLevelDeclarations.Remove(ac.EntryPoint.Proc);
      ac.TopLevelDeclarations.Remove(ac.EntryPoint);
    }

    internal static void RemoveUnecesseryInfoFromSpecialFunctions(AnalysisContext ac)
    {
      var toRemove = new List<string>();

      foreach (var proc in ac.TopLevelDeclarations.OfType<Procedure>())
      {
        if (!(proc.Name.Contains("$memcpy") || proc.Name.Contains("memcpy_fromio") ||
          proc.Name.Contains("$memset") ||
          proc.Name.Equals("pthread_mutex_lock") ||
          proc.Name.Equals("pthread_mutex_unlock")))
          continue;
        proc.Modifies.Clear();
        proc.Requires.Clear();
        proc.Ensures.Clear();
        toRemove.Add(proc.Name);
      }

      foreach (var str in toRemove)
      {
        ac.TopLevelDeclarations.RemoveAll(val => (val is Implementation) &&
          (val as Implementation).Name.Equals(str));
      }
    }
  }
}
