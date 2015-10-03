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
using System.IO;

using Microsoft.Boogie;

namespace Lockpwn.IO
{
  /// <summary>
  /// IO emitter class.
  /// </summary>
  internal static class BoogieProgramEmitter
  {
    internal static void EmitOutput(List<Declaration> declarations, string file)
    {
      string directoryContainingFile = Path.GetDirectoryName(file);
      if (string.IsNullOrEmpty(directoryContainingFile))
        directoryContainingFile = Directory.GetCurrentDirectory();

      var output = Path.GetFileNameWithoutExtension(file) + "_pwned";
      if (ToolCommandLineOptions.Get().OutputFile.Length > 0)
        output = ToolCommandLineOptions.Get().OutputFile;

      var fileName = directoryContainingFile + Path.DirectorySeparatorChar + output;

      using(TokenTextWriter writer = new TokenTextWriter(fileName + ".bpl", true))
      {
        declarations.Emit(writer);
      }
    }

    internal static void Emit(List<Declaration> declarations, string file, string extension)
    {
      string directoryContainingFile = Path.GetDirectoryName(file);
      if (string.IsNullOrEmpty(directoryContainingFile))
        directoryContainingFile = Directory.GetCurrentDirectory();

      var fileName = directoryContainingFile + Path.DirectorySeparatorChar +
                     Path.GetFileNameWithoutExtension(file);

      using(TokenTextWriter writer = new TokenTextWriter(fileName + "." + extension, true))
      {
        declarations.Emit(writer);
      }
    }

    internal static void Emit(List<Declaration> declarations, string file, string suffix, string extension)
    {
      string directoryContainingFile = Path.GetDirectoryName(file);
      if (string.IsNullOrEmpty(directoryContainingFile))
        directoryContainingFile = Directory.GetCurrentDirectory();

      var fileName = directoryContainingFile + Path.DirectorySeparatorChar +
        Path.GetFileNameWithoutExtension(file) + "_" + suffix;

      using(TokenTextWriter writer = new TokenTextWriter(fileName + "." + extension, true))
      {
        declarations.Emit(writer);
      }
    }

    internal static void Remove(string file, string suffix, string extension)
    {
      string directoryContainingFile = Path.GetDirectoryName(file);
      if (string.IsNullOrEmpty(directoryContainingFile))
        directoryContainingFile = Directory.GetCurrentDirectory();

      var fileName = directoryContainingFile + Path.DirectorySeparatorChar +
        Path.GetFileNameWithoutExtension(file) + "_" + suffix;

      File.Delete(fileName + "." + extension);
    }
  }
}
