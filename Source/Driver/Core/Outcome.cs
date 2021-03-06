﻿//===-----------------------------------------------------------------------==//
//
// Lockpwn - blazing fast symbolic analysis for concurrent Boogie programs
//
// Copyright (c) 2015 Pantazis Deligiannis (pdeligia@me.com)
//
// This file is distributed under the MIT License. See LICENSE.TXT for details.
//
//===----------------------------------------------------------------------===//

using System;

namespace Lockpwn
{
  internal enum Outcome
  {
    Done = 0,
    FatalError = 1,
    ParsingError = 2,
    InstrumentationError = 3,
    LocksetAnalysisError = 4
  }
}
