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
using System.Diagnostics;
using System.IO.Ports;

namespace Lockpwn
{
  internal class ExecutionTimer
  {
    private Stopwatch Timer;

    internal ExecutionTimer()
    {
      this.Timer = new Stopwatch();
    }

    internal void Start()
    {
      this.Timer.Reset();
      this.Timer.Start();
    }

    internal void Stop()
    {
      this.Timer.Stop();
    }

    internal double Result()
    {
      return this.Timer.Elapsed.TotalSeconds;
    }
  }
}
