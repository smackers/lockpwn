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
    #region fields

    /// <summary>
    /// The timer.
    /// </summary>
    private Stopwatch Timer;

    #endregion

    #region methods

    /// <summary>
    /// Constructor.
    /// </summary>
    internal ExecutionTimer()
    {
      this.Timer = new Stopwatch();
    }

    /// <summary>
    /// Starts timing.
    /// </summary>
    internal void Start()
    {
      this.Timer.Reset();
      this.Timer.Start();
    }

    /// <summary>
    /// Stops timing.
    /// </summary>
    internal void Stop()
    {
      this.Timer.Stop();
    }

    /// <summary>
    /// Returns the result.
    /// </summary>
    /// <returns>Time</returns>
    internal double Result()
    {
      return this.Timer.Elapsed.TotalSeconds;
    }

    #endregion
  }
}
