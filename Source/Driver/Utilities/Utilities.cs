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
using Microsoft.Boogie;

namespace Lockpwn
{
  internal static class Utilities
  {
    /// <summary>
    /// Checks if the given function should be accessed.
    /// </summary>
    /// <param name="funcName">Function name</param>
    /// <returns>Boolean value</returns>
    internal static bool ShouldAccessFunction(string funcName)
    {
      if (funcName.StartsWith("$memcpy") || funcName.StartsWith("$memcpy_fromio") ||
        funcName.StartsWith("$memset") ||
        funcName.StartsWith("$malloc") || funcName.StartsWith("$alloc") ||
        funcName.StartsWith("$free") ||
        funcName.Equals("pthread_mutex_lock") ||
        funcName.Equals("pthread_mutex_unlock"))
        return false;
      return true;
    }

    /// <summary>
    /// These functions should be skipped from the analysis.
    /// </summary>
    /// <param name="funcName">Function name</param>
    /// <returns>Boolean value</returns>
    internal static bool ShouldSkipFromAnalysis(string funcName)
    {
      if (funcName.Equals("$initialize") ||
        funcName.StartsWith("__SMACK_static_init") ||
        funcName.StartsWith("__SMACK_init_func_memory_model") ||
        funcName.StartsWith("__SMACK_init_func_corral_primitives") ||
        funcName.StartsWith("__SMACK_init_func_thread") ||
        funcName.StartsWith("$malloc") || funcName.StartsWith("$alloc") ||
        funcName.StartsWith("$free") ||
        funcName.Contains("pthread_mutex_init") ||
        funcName.Contains("pthread_create") ||
        funcName.Contains("pthread_join") ||
        funcName.Contains("pthread_cond_init") ||
        funcName.Contains("pthread_cond_wait") ||
        funcName.Contains("pthread_mutex_destroy") ||
        funcName.Contains("pthread_exit") ||
        funcName.Contains("pthread_mutexattr_init") ||
        funcName.Contains("pthread_mutexattr_settype") ||
        funcName.Contains("pthread_self") ||
        funcName.Contains("boogie_si_record_i32") ||
        funcName.Contains("corral_atomic_begin") ||
        funcName.Contains("corral_atomic_end") ||
        funcName.Contains("corral_getThreadID") ||
        funcName.Contains("__call_wrapper") ||
        funcName.Contains("__SMACK_nondet") ||
        funcName.Contains("__SMACK_dummy") ||
        funcName.Contains("__VERIFIER_assume") ||
        funcName.Contains("__VERIFIER_error") ||
        funcName.Contains("__VERIFIER_nondet_int"))
        return true;
      return false;
    }

    /// <summary>
    /// True if it is a PThread function
    /// </summary>
    /// <param name="funcName">Function name</param>
    /// <returns>Boolean value</returns>
    internal static bool IsPThreadFunction(string funcName)
    {
      if (funcName.Contains("pthread_mutex_init") ||
        funcName.Contains("pthread_mutex_lock") ||
        funcName.Contains("pthread_mutex_unlock") ||
        funcName.Contains("pthread_create") ||
        funcName.Contains("pthread_join") ||
        funcName.Contains("pthread_cond_init") ||
        funcName.Contains("pthread_cond_wait") ||
        funcName.Contains("pthread_mutex_destroy") ||
        funcName.Contains("pthread_exit") ||
        funcName.Contains("pthread_mutexattr_init") ||
        funcName.Contains("pthread_mutexattr_settype") ||
        funcName.Contains("pthread_self") ||
        funcName.Contains("__call_wrapper"))
        return true;
      return false;
    }
  }
}

