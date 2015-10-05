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
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

using Microsoft.Boogie;
using System.Diagnostics;

using Lockpwn.IO;

namespace Lockpwn
{
  internal class Program
  {
    #region static fields

    /// <summary>
    /// List of files to analyze.
    /// </summary>
    internal List<string> FileList;

    /// <summary>
    /// The analysis context.
    /// </summary>
    internal AnalysisContext AC;

    #endregion

    #region methods

    /// <summary>
    /// Constructor.
    /// </summary>
    internal Program()
    {
      this.FileList = new List<string>();
    }

    #endregion
  }
}
