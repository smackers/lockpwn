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
    internal static void Emit(List<Declaration> declarations, string file, string extension)
    {
      string directory = BoogieProgramEmitter.GetDirectoryWithFile(file);
      var fileName = directory + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(file) +
        "." + extension;

      using(TokenTextWriter writer = new TokenTextWriter(fileName, true))
      {
        declarations.Emit(writer);
      }
    }

    internal static void Emit(List<Declaration> declarations, string file, string suffix, string extension)
    {
      string directory = BoogieProgramEmitter.GetDirectoryWithFile(file);
      var fileName = directory + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(file) +
        "_" + suffix + "." + extension;

      using(TokenTextWriter writer = new TokenTextWriter(fileName, true))
      {
        declarations.Emit(writer);
      }
    }

    /// <summary>
    /// Emites to a user specified file.
    /// </summary>
    /// <param name="declarations">Declarations</param>
    internal static void EmitToUserSpecifiedFile(List<Declaration> declarations)
    {
      string directory = ToolCommandLineOptions.Get().OutputDirectory;

      var fileName = "";
      if (directory.Length > 0)
      {
        BoogieProgramEmitter.TryCreateDirectory(directory);
        fileName = directory + Path.DirectorySeparatorChar + ToolCommandLineOptions.Get().OutputFile;
      }
      else
      {
        fileName = ToolCommandLineOptions.Get().OutputFile;
      }

      using(TokenTextWriter writer = new TokenTextWriter(fileName, true))
      {
        declarations.Emit(writer);
      }
    }

    /// <summary>
    /// Checks if the file with the given suffix and extension exists.
    /// </summary>
    /// <param name="file">File</param>
    /// <param name="suffix">Suffix</param>
    /// <param name="extension">Extension</param>
    /// <param name="fileName">Name</param>
    internal static bool Exists(string file, string suffix, string extension, out string fileName)
    {
      string directory = BoogieProgramEmitter.GetDirectoryWithFile(file);
      fileName = directory + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(file) +
        "_" + suffix + "." + extension;
      return File.Exists(fileName);
    }

    /// <summary>
    /// Removes the file with the given suffix and extension.
    /// </summary>
    /// <param name="file">File</param>
    /// <param name="suffix">Suffix</param>
    /// <param name="extension">Extension</param>
    internal static void Remove(string file, string suffix, string extension)
    {
      string directory = BoogieProgramEmitter.GetDirectoryWithFile(file);
      var fileName = directory + Path.DirectorySeparatorChar +
        Path.GetFileNameWithoutExtension(file) + "_" + suffix;
      File.Delete(fileName + "." + extension);
    }

    /// <summary>
    /// Returns the directory that contains the given file.
    /// </summary>
    /// <param name="file">File</param>
    /// <returns>Directory</returns>
    private static string GetDirectoryWithFile(string file)
    {
      string directoryContainingFile = Path.GetDirectoryName(file);
      if (string.IsNullOrEmpty(directoryContainingFile))
        directoryContainingFile = Directory.GetCurrentDirectory();
      return directoryContainingFile;
    }

    /// <summary>
    /// Creates the given directory if it does not exist.
    /// </summary>
    /// <param name="directory">Directory</param>
    private static void TryCreateDirectory(string directory) {
      if (directory.Length > 0)
      {
        Directory.CreateDirectory(directory);
      }
    }
  }
}
