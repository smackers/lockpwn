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
using System.Globalization;

namespace Lockpwn.IO
{
  /// <summary>
  /// Output class.
  /// </summary>
  internal static class Output
  {
    #region fields

    /// <summary>
    /// Prints debugging information.
    /// </summary>
    internal static bool Debugging;

    #endregion

    #region API

    /// <summary>
    /// Static constructor.
    /// </summary>
    static Output()
    {
      Output.Debugging = false;
    }

    /// <summary>
    /// Formats the given string.
    /// </summary>
    /// <param name="s">String</param>
    /// <param name="args">Arguments</param>
    /// <returns></returns>
    internal static string Format(string s, params object[] args)
    {
      return string.Format(CultureInfo.InvariantCulture, s, args);
    }

    /// <summary>
    /// Writes the text representation of the specified array
    /// of objects to the output stream.
    /// </summary>
    /// <param name="s">String</param>
    /// <param name="args">Arguments</param>
    internal static void Print(string s, params object[] args)
    {
      Console.Write(s, args);
    }

    /// <summary>
    /// Writes the text representation of the specified array
    /// of objects, followed by the current line terminator, to
    /// the output stream.
    /// </summary>
    /// <param name="s">String</param>
    /// <param name="args">Arguments</param>
    internal static void PrintLine(string s, params object[] args)
    {
      Console.WriteLine(s, args);
    }

    /// <summary>
    /// Writes the text representation of the specified array
    /// of objects to the output stream. The text is formatted.
    /// </summary>
    /// <param name="s">String</param>
    /// <param name="args">Arguments</param>
    internal static void PrettyPrint(string s, params object[] args)
    {
      string message = Output.Format(s, args);
      Console.Write(message);
    }

    /// <summary>
    /// Writes the text representation of the specified array
    /// of objects, followed by the current line terminator, to
    /// the output stream. The text is formatted.
    /// </summary>
    /// <param name="s">String</param>
    /// <param name="args">Arguments</param>
    internal static void PrettyPrintLine(string s, params object[] args)
    {
      string message = Output.Format(s, args);
      Console.WriteLine(message);
    }

    /// <summary>
    /// Prints the debugging information, followed by the current
    /// line terminator, to the output stream. The print occurs
    /// only if debugging is enabled.
    /// </summary>
    /// <param name="s">String</param>
    /// <param name="args">Arguments</param>
    internal static void Debug(string s, params object[] args)
    {
      if (!Output.Debugging)
      {
        return;
      }

      string message = Output.Format(s, args);
      Console.WriteLine(message);
    }

    /// <summary>
    /// Clears all buffers for the current writer and causes any
    /// unbuffered data to be written to the underlying device.
    /// </summary>
    internal static void Flush()
    {
      Console.Out.Flush();
    }

    #endregion
  }
}
