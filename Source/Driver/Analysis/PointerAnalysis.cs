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
using System.Reflection;
using System.Diagnostics;

using Microsoft.Boogie;
using Microsoft.Basetypes;

namespace Lockpwn.Analysis
{
  /// <summary>
  /// Class implementing methods for pointer analysis.
  /// </summary>
  internal sealed class PointerAnalysis
  {
    #region fields

    private AnalysisContext AC;
    private Implementation Implementation;
    private List<Variable> InParams;

    private bool Optimise;

    private Dictionary<IdentifierExpr, Dictionary<Expr, int>> ExpressionMap;
    private Dictionary<IdentifierExpr, HashSet<Expr>> AssignmentMap;
    private Dictionary<IdentifierExpr, HashSet<CallCmd>> CallMap;

    private static Dictionary<Implementation, Dictionary<IdentifierExpr, HashSet<Expr>>> Cache =
      new Dictionary<Implementation, Dictionary<IdentifierExpr, HashSet<Expr>>>();

    private enum ArithmeticOperation
    {
      Addition = 0,
      Subtraction,
      Multiplication,
      Division
    }

    internal enum ResultType
    {
      Unknown = 0,
      Pointer,
      Literal,
      Axiom,
      Const,
      Shared,
      Allocated
    }

    #endregion

    #region internal API

    internal PointerAnalysis(AnalysisContext ac, Implementation impl, bool optimise = false)
    {
      Contract.Requires(ac != null && impl != null);
      this.AC = ac;
      this.Implementation = impl;
      this.InParams = impl.InParams;

      this.Optimise = optimise;

      this.ExpressionMap = new Dictionary<IdentifierExpr, Dictionary<Expr, int>>();
      this.AssignmentMap = new Dictionary<IdentifierExpr, HashSet<Expr>>();
      this.CallMap = new Dictionary<IdentifierExpr, HashSet<CallCmd>>();

      if (!PointerAnalysis.Cache.ContainsKey(impl))
        PointerAnalysis.Cache.Add(impl,
          new Dictionary<IdentifierExpr, HashSet<Expr>>());
    }

    /// <summary>
    /// Returns the origins of the pointer.
    /// </summary>
    /// <returns>Result type</returns>
    /// <param name="id">Expression</param>
    /// <param name="ptrExprs">Computed ptr expressions</param>
    internal ResultType GetPointerOrigins(Expr id, out HashSet<Expr> ptrExprs)
    {
      ptrExprs = new HashSet<Expr>();

      if (id is NAryExpr && !this.Optimise)
      {
        var constExpr = this.TryComputeConstNaryExpr(id as NAryExpr);
        if (constExpr != null)
        {
          ptrExprs.Add(constExpr);
        }

        return ResultType.Const;
      }

      if ((id is LiteralExpr) && (id as LiteralExpr).isBigNum)
      {
        ptrExprs.Add(id);
        return ResultType.Literal;
      }

      if (!(id is IdentifierExpr))
      {
        return ResultType.Unknown;
      }

      var identifier = id as IdentifierExpr;
      if (this.InParams.Any(val => val.Name.Equals(identifier.Name)))
      {
        ptrExprs.Add(Expr.Add(identifier, new LiteralExpr(Token.NoToken, BigNum.FromInt(0))));
        return ResultType.Pointer;
      }
      else if (this.IsAxiom(identifier))
      {
        ptrExprs.Add(Expr.Add(identifier, new LiteralExpr(Token.NoToken, BigNum.FromInt(0))));
        return ResultType.Axiom;
      }

      if (PointerAnalysis.Cache[this.Implementation].ContainsKey(identifier))
      {
        ptrExprs = PointerAnalysis.Cache[this.Implementation][identifier];
        return ResultType.Pointer;
      }

      this.ComputeMapsForIdentifierExpr(identifier);

      var identifiers = new Dictionary<IdentifierExpr, bool>();
      identifiers.Add(identifier, false);

      this.ComputeExpressionMap(identifiers);

      Expr shared = null;
      if (this.TryGetSharedIdentifier(identifier, out shared))
      {
        ptrExprs.Add(shared);
        return ResultType.Shared;
      }

      this.ComputeAndCacheRootPointers();
      this.CacheMatchedPointers();

      if (this.CallMap.ContainsKey(identifier))
      {
        Expr local = null;
        if (this.TryGetAllocatedIdentifier(identifier, out local))
        {
          ptrExprs.Add(local);
          return ResultType.Allocated;
        }
      }

      if (PointerAnalysis.Cache[this.Implementation][identifier].Count > 0)
      {
        ptrExprs = PointerAnalysis.Cache[this.Implementation][identifier];
        return ResultType.Pointer;
      }

      ptrExprs.Add(identifier);
      return ResultType.Pointer;
    }

    internal bool IsAxiom(IdentifierExpr expr)
    {
      bool result = false;
      if (expr == null)
        return result;

      foreach (var axiom in this.AC.TopLevelDeclarations.OfType<Axiom>())
      {
        Expr axiomExpr = null;
        if (axiom.Expr is NAryExpr)
          axiomExpr = (axiom.Expr as NAryExpr).Args[0];
        else
          axiomExpr = axiom.Expr;

        if (axiomExpr.ToString().Equals(expr.Name))
        {
          result = true;
          break;
        }
      }

      return result;
    }

    internal IdentifierExpr GetIdentifier(Expr expr)
    {
      IdentifierExpr id = null;
      if (expr is NAryExpr)
        id = (expr as NAryExpr).Args[0] as IdentifierExpr;
      else
        id = expr as IdentifierExpr;
      return id;
    }

    internal Expr ComputeLiteralsInExpr(Expr expr)
    {
      if (!(expr is NAryExpr) || !((expr as NAryExpr).Args[0] is NAryExpr))
      {
        return expr;
      }

      int l1 = ((expr as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
      int l2 = (((expr as NAryExpr).Args[0] as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;

      Expr result = ((expr as NAryExpr).Args[0] as NAryExpr).Args[0];

      return Expr.Add(result, new LiteralExpr(Token.NoToken, BigNum.FromInt(l1 + l2)));
    }

    /// <summary>
    /// Recomputes the expression from the given inparams.
    /// </summary>
    /// <param name="tid">Expression</param>
    /// <param name="impl">Implementation</param>
    /// <param name="inPtrs">List of expressions</param>
    /// <returns>Expression</returns>
    internal Expr RecomputeExprFromInParams(Expr tid, List<Expr> inPtrs)
    {
      if (inPtrs != null && (!(tid is LiteralExpr) || tid is NAryExpr))
      {
        if (tid is IdentifierExpr)
        {
          for (int i = 0; i < this.Implementation.InParams.Count; i++)
          {
            if (tid.ToString().Equals(this.Implementation.InParams[i].ToString()))
            {
              tid = inPtrs[i];
            }
          }
        }
        else if (tid is NAryExpr)
        {
          for (int i = 0; i < (tid as NAryExpr).Args.Count; i++)
          {
            for (int j = 0; j < this.Implementation.InParams.Count; j++)
            {
              if ((tid as NAryExpr).Args[i].ToString().Equals(this.Implementation.InParams[j].ToString()))
              {
                (tid as NAryExpr).Args[i] = inPtrs[j];
              }
            }
          }
        }

        tid = this.ComputeLiteralsInExpr(tid);
      }

      return tid;
    }

    #endregion

    #region pointer arithmetic analysis functions

    private void ComputeMapsForIdentifierExpr(IdentifierExpr id)
    {
      if (PointerAnalysis.Cache[this.Implementation].ContainsKey(id))
        return;

      if (!this.ExpressionMap.ContainsKey(id))
        this.ExpressionMap.Add(id, new Dictionary<Expr, int>());
      if (!this.AssignmentMap.ContainsKey(id))
        this.AssignmentMap.Add(id, new HashSet<Expr>());
      if (!this.CallMap.ContainsKey(id))
        this.CallMap.Add(id, new HashSet<CallCmd>());

      foreach (var block in this.Implementation.Blocks)
      {
        for (int i = block.Cmds.Count - 1; i >= 0; i--)
        {
          if (block.Cmds[i] is AssignCmd)
          {
            var assign = block.Cmds[i] as AssignCmd;
            if (!assign.Lhss[0].DeepAssignedIdentifier.Name.Equals(id.Name))
              continue;
            if (this.AssignmentMap[id].Contains(assign.Rhss[0]))
              continue;
            
            var expr = assign.Rhss[0];
            PointerAnalysis.TryPerformCast(ref expr);
            this.AssignmentMap[id].Add(expr);

            if (expr.ToString().StartsWith("$pa("))
              this.ExpressionMap[id].Add(expr, 0);
            if (expr is IdentifierExpr && this.InParams.Any(val =>
              val.Name.Equals((expr as IdentifierExpr).Name)))
              this.ExpressionMap[id].Add(expr, 0);
            if (expr is IdentifierExpr && this.AC.TopLevelDeclarations.OfType<Constant>().
              Any(val => val.Name.Equals((expr as IdentifierExpr).Name)))
              this.ExpressionMap[id].Add(expr, 0);
            if (expr is LiteralExpr)
              this.ExpressionMap[id].Add(expr, 0);
          }
          else if (block.Cmds[i] is CallCmd)
          {
            var call = block.Cmds[i] as CallCmd;
            if (call.callee.Equals("$alloc"))
            {
              if (!call.Outs[0].Name.Equals(id.Name))
                continue;

              this.CallMap[id].Add(call);
            }
          }
        }
      }
    }

    private void ComputeExpressionMap(Dictionary<IdentifierExpr, bool> identifiers)
    {
      foreach (var id in identifiers.Keys.ToList())
      {
        if (PointerAnalysis.Cache[this.Implementation].ContainsKey(id))
          continue;
        if (identifiers[id]) continue;

        bool fixpoint = true;
        do
        {
          fixpoint = this.TryComputeNAryExprs(id);
        }
        while (!fixpoint);
        identifiers[id] = true;

        foreach (var expr in this.ExpressionMap[id].Keys.ToList())
        {
          if (!(expr is IdentifierExpr)) continue;
          var exprId = expr as IdentifierExpr;

          if (identifiers.ContainsKey(exprId) && identifiers[exprId])
            continue;
          if (this.InParams.Any(val => val.Name.Equals(exprId.Name)))
            continue;
          if (this.AC.TopLevelDeclarations.OfType<Constant>().Any(val => val.Name.Equals(exprId.Name)))
            continue;

          this.ComputeMapsForIdentifierExpr(exprId);
          if (PointerAnalysis.Cache[this.Implementation].ContainsKey(exprId) &&
            !identifiers.ContainsKey(exprId))
          {
            identifiers.Add(exprId, true);
          }
          else if (!identifiers.ContainsKey(exprId))
          {
            identifiers.Add(exprId, false);
          }
        }

        foreach (var expr in this.AssignmentMap[id].ToList())
        {
          if (!(expr is IdentifierExpr)) continue;
          var exprId = expr as IdentifierExpr;

          if (identifiers.ContainsKey(exprId) && identifiers[exprId])
            continue;
          if (this.InParams.Any(val => val.Name.Equals(exprId.Name)))
            continue;

          this.ComputeMapsForIdentifierExpr(exprId);
          if (PointerAnalysis.Cache[this.Implementation].ContainsKey(exprId) &&
            !identifiers.ContainsKey(exprId))
          {
            identifiers.Add(exprId, true);
          }
          else if (!identifiers.ContainsKey(exprId))
          {
            identifiers.Add(exprId, false);
          }
        }
      }

      if (identifiers.Values.Contains(false))
        this.ComputeExpressionMap(identifiers);
    }

    private void ComputeAndCacheRootPointers()
    {
      foreach (var identifier in this.ExpressionMap)
      {
        if (PointerAnalysis.Cache[this.Implementation].ContainsKey(identifier.Key))
          continue;

        PointerAnalysis.Cache[this.Implementation].Add(identifier.Key, new HashSet<Expr>());
        foreach (var pair in identifier.Value)
        {
          if (pair.Key is LiteralExpr)
          {
            PointerAnalysis.Cache[this.Implementation][identifier.Key].Add(
              Expr.Add(pair.Key, new LiteralExpr(Token.NoToken, BigNum.FromInt(pair.Value))));
          }
          else if (pair.Key is IdentifierExpr)
          {
            var id = pair.Key as IdentifierExpr;
            if (this.InParams.Any(val => val.Name.Equals(id.Name)))
            {
              PointerAnalysis.Cache[this.Implementation][identifier.Key].Add(
                Expr.Add(id, new LiteralExpr(Token.NoToken, BigNum.FromInt(pair.Value))));
            }
            else
            {
              var outcome = new HashSet<Expr>();
              var alreadyMatched = new HashSet<Tuple<IdentifierExpr, IdentifierExpr>>();
              this.MatchExpressions(outcome, identifier.Key, id, pair.Value, alreadyMatched);
              foreach (var expr in outcome)
              {
                PointerAnalysis.Cache[this.Implementation][identifier.Key].Add(expr);
              }
            }
          }
        }
      }
    }

    private void CacheMatchedPointers()
    {
      foreach (var identifier in this.AssignmentMap)
      {
        if (!PointerAnalysis.Cache[this.Implementation].ContainsKey(identifier.Key))
          continue;

        foreach (var expr in identifier.Value)
        {
          if (!(expr is IdentifierExpr))continue;
          var exprId = expr as IdentifierExpr;
          if (!exprId.Name.StartsWith("$p")) continue;
          if (!PointerAnalysis.Cache[this.Implementation].ContainsKey(exprId))
            continue;

          var results = PointerAnalysis.Cache[this.Implementation][exprId];
          foreach (var res in results)
          {
            PointerAnalysis.Cache[this.Implementation][identifier.Key].Add(res);
          }
        }
      }
    }

    private void MatchExpressions(HashSet<Expr> outcome, IdentifierExpr lhs, IdentifierExpr rhs, int value,
      HashSet<Tuple<IdentifierExpr, IdentifierExpr>> alreadyMatched)
    {
      if (alreadyMatched.Any(val => val.Item1.Name.Equals(lhs.Name) &&
          val.Item2.Name.Equals(rhs.Name)))
        return;

      alreadyMatched.Add(new Tuple<IdentifierExpr, IdentifierExpr>(lhs, rhs));
      if (PointerAnalysis.Cache[this.Implementation].ContainsKey(rhs))
      {
        var results = PointerAnalysis.Cache[this.Implementation][rhs];
        foreach (var r in results)
        {
          var arg = (r as NAryExpr).Args[0];
          var num = ((r as NAryExpr).Args[1] as LiteralExpr).asBigNum.ToInt;
          var result = Expr.Add(arg, new LiteralExpr(Token.NoToken, BigNum.FromInt(num + value)));
          outcome.Add(result);
        }
      }
      else if (this.ExpressionMap.ContainsKey(rhs))
      {
        foreach (var pair in this.ExpressionMap[rhs])
        {
          if (!(pair.Key is IdentifierExpr))
            continue;

          var id = pair.Key as IdentifierExpr;
          if (this.InParams.Any(val => val.Name.Equals(id.Name)))
          {
            var result = Expr.Add(id, new LiteralExpr(Token.NoToken, BigNum.FromInt(pair.Value + value)));
            if (outcome.Contains(result))
              return;
            outcome.Add(result);
          }
          else if (this.AC.TopLevelDeclarations.OfType<Constant>().Any(val => val.Name.Equals(id.Name)))
          {
            var result = Expr.Add(id, new LiteralExpr(Token.NoToken, BigNum.FromInt(pair.Value + value)));
            if (outcome.Contains(result))
              return;
            outcome.Add(result);
          }
          else if (id.Name.StartsWith("$p"))
          {
            this.MatchExpressions(outcome, rhs, id, pair.Value + value, alreadyMatched);
          }
        }
      }

      if (this.CallMap.ContainsKey(rhs))
      {
        foreach (var call in this.CallMap[rhs])
        {
          this.CallMap[lhs].Add(call);
        }
      }

      if (this.AssignmentMap.ContainsKey(rhs))
      {
        foreach (var assign in this.AssignmentMap[rhs])
        {
          if (!(assign is IdentifierExpr))
            continue;

          var id = assign as IdentifierExpr;
          if (id.Name.StartsWith("$p"))
          {
            this.MatchExpressions(outcome, rhs, id, value, alreadyMatched);
          }
        }
      }
    }

    private bool TryComputeNAryExprs(IdentifierExpr id)
    {
      var toRemove = new HashSet<Expr>();
      foreach (var expr in this.ExpressionMap[id].Keys.ToList())
      {
        if (!(expr is NAryExpr))
          continue;

        int ixs = 0;

        if (((expr as NAryExpr).Args[0] is IdentifierExpr) &&
          ((expr as NAryExpr).Args[0] as IdentifierExpr).Name.StartsWith("$M."))
        {
          toRemove.Add(expr);
          continue;
        }

        if (PointerAnalysis.ShouldSkipFromAnalysis(expr as NAryExpr))
        {
          toRemove.Add(expr);
          continue;
        }

        if (PointerAnalysis.IsArithmeticExpression(expr as NAryExpr))
        {
          toRemove.Add(expr);
          continue;
        }

        Expr p = (expr as NAryExpr).Args[0];
        Expr i = (expr as NAryExpr).Args[1];
        Expr s = (expr as NAryExpr).Args[2];

        if (!(i is LiteralExpr && s is LiteralExpr))
        {
          toRemove.Add(expr);
          continue;
        }

        ixs = (i as LiteralExpr).asBigNum.ToInt * (s as LiteralExpr).asBigNum.ToInt;

        this.ExpressionMap[id].Add(p, this.ExpressionMap[id][expr] + ixs);
        toRemove.Add(expr);
      }

      foreach (var expr in toRemove)
      {
        this.ExpressionMap[id].Remove(expr);
      }

      if (this.ExpressionMap[id].Any(val => val.Key is NAryExpr))
        return false;

      return true;
    }

    private Expr TryComputeConstNaryExpr(NAryExpr expr)
    {
      Expr result = expr;
      int ixs = 0;

      do
      {
        var nary = result as NAryExpr;

        if (!nary.Fun.FunctionName.Equals("$pa") ||
          ((nary.Args[0] is NAryExpr) &&
            !(nary.Args[0] as NAryExpr).Fun.FunctionName.Equals("$pa")) ||
          ((nary.Args[0] is IdentifierExpr) &&
            this.AC.GetConstant((nary.Args[0] as IdentifierExpr).Name) == null))
        {
          return null;
        }

        Expr p = (result as NAryExpr).Args[0];
        Expr i = (result as NAryExpr).Args[1];
        Expr s = (result as NAryExpr).Args[2];

        if ((i is LiteralExpr) && (s is LiteralExpr))
        {
          ixs += (i as LiteralExpr).asBigNum.ToInt * (s as LiteralExpr).asBigNum.ToInt;
        }
        else
        {
          return null;
        }

        result = p;
      }
      while (result is NAryExpr);

      return Expr.Add(result, new LiteralExpr(Token.NoToken, BigNum.FromInt(ixs)));
    }

    private bool TryGetAllocatedIdentifier(IdentifierExpr identifier, out Expr allocated)
    {
      var call = this.CallMap[identifier].FirstOrDefault(val => val.callee.Equals("$alloc"));
      if (call != null)
      {
        var id = call.Outs[0] as IdentifierExpr;
        var local = this.Implementation.LocVars.FirstOrDefault(val => val.Name.Equals(id.Name));

        allocated = new IdentifierExpr(local.tok, local);
        return true;
      }

      allocated = identifier;
      return false;
    }

    private bool TryGetSharedIdentifier(IdentifierExpr identifier, out Expr shared)
    {
      if (this.AssignmentMap.ContainsKey(identifier))
      {
        foreach (var naryExpr in this.AssignmentMap[identifier].OfType<NAryExpr>())
        {
          if (naryExpr.Fun is MapSelect && naryExpr.Args.Count == 2 &&
            (naryExpr.Args[0] as IdentifierExpr).Name.StartsWith("$M."))
          {
            var id = naryExpr.Args[1] as IdentifierExpr;
            var local = this.Implementation.LocVars.FirstOrDefault(val => val.Name.Equals(id.Name));

            shared = new IdentifierExpr(local.tok, local);
            return true;
          }
        }
      }

      shared = identifier;
      return false;
    }

    #endregion

    #region helper functions

    private static bool TryPerformCast(ref Expr expr)
    {
      if (!(expr is NAryExpr))
      {
        return false;
      }

      var fun = (expr as NAryExpr).Fun;
      if (!(fun.FunctionName == "$bitcast.ref.ref" ||
        fun.FunctionName.StartsWith("$zext.") ||
        fun.FunctionName.StartsWith("$i2p.") ||
        fun.FunctionName.StartsWith("$p2i.")))
      {
        return false;
      }

      expr = (expr as NAryExpr).Args[0];

      return true;
    }

    private static bool IsArithmeticExpression(NAryExpr expr)
    {
      if (expr.Fun.FunctionName.StartsWith("$add.") ||
        (expr as NAryExpr).Fun.FunctionName == "+" ||
        expr.Fun.FunctionName.StartsWith("$sub.") ||
        (expr as NAryExpr).Fun.FunctionName == "-" ||
        expr.Fun.FunctionName.StartsWith("$mul.") ||
        (expr as NAryExpr).Fun.FunctionName == "*")
        return true;
      return false;
    }

    /// <summary>
    /// These functions should be skipped from pointer alias analysis.
    /// </summary>
    /// <returns>Boolean value</returns>
    /// <param name="call">CallCmd</param>
    private static bool ShouldSkipFromAnalysis(NAryExpr expr)
    {
      if (expr.Fun.FunctionName.StartsWith("$and.") ||
        expr.Fun.FunctionName.StartsWith("$or.") ||
        expr.Fun.FunctionName.StartsWith("$xor.") ||
        expr.Fun.FunctionName.StartsWith("$eq.") ||
        expr.Fun.FunctionName.StartsWith("$ne.") ||
        expr.Fun.FunctionName.StartsWith("$ugt.") ||
        expr.Fun.FunctionName.StartsWith("$uge.") ||
        expr.Fun.FunctionName.StartsWith("$ult.") ||
        expr.Fun.FunctionName.StartsWith("$ule.") ||
        expr.Fun.FunctionName.StartsWith("$sgt.") ||
        expr.Fun.FunctionName.StartsWith("$sge.") ||
        expr.Fun.FunctionName.StartsWith("$slt.") ||
        expr.Fun.FunctionName.StartsWith("$sle.") ||
        expr.Fun.FunctionName.StartsWith("$i2p.") ||
        expr.Fun.FunctionName.StartsWith("$p2i.") ||
        expr.Fun.FunctionName.StartsWith("$trunc.") ||
        expr.Fun.FunctionName.StartsWith("$ashr") ||
        expr.Fun.FunctionName.StartsWith("$lshr") ||
        expr.Fun.FunctionName.StartsWith("$urem") ||
        expr.Fun.FunctionName.StartsWith("$udiv") ||
        expr.Fun.FunctionName == "!=" ||
        expr.Fun.FunctionName == "-")
        return true;
      return false;
    }

    #endregion
  }
}
