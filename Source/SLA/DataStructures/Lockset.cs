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
using System.Diagnostics.Contracts;

using Microsoft.Boogie;

namespace Lockpwn
{
  internal class Lockset
  {
    internal readonly Variable Id;
    internal readonly Variable Lock;
    internal readonly Thread Thread;
    internal readonly string TargetName;

    internal Lockset(Variable id, Variable l, Thread thread, string target = "")
    {
      this.Id = id;
      this.Lock = l;
      this.Thread = thread;
      this.TargetName = target;
    }
  }
}
