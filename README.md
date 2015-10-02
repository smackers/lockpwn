lockpwn
====================
A lightweight, lockset-based, symbolic data race analyser for concurrent C programs.

## Build instructions
1. Clone this project.
1. Compile using Visual Studio or Mono.

## How to use

The input to lockpwn is a concurrent C program translated to the Boogie intermediate verification language using the [SMACK](https://github.com/smackers/smack) LLVM-to-Boogie translator.

Given an input ${PROGRAM} in Boogie, do the following:

```
.\lockpwn.exe ${PROGRAM}.bpl /o ${OUTPUT}.bpl
```

The output, ${OUTPUT}.bpl, is an instrumented with context switches Boogie program. This can be directly fed to the Corral bug-finder using the /cooperative option (so Corral does not automatically instrument yield statements).

## Tool options

Use /v for verbose mode or /v2 for super verbose mode. Use /time for timing information.

## Example run

The following is the output from an example run of lockpwn using the flag `/v2`:

```
. Parsing
... ProgramSimplifier
. ThreadAnalysis
... ThreadUsageAnalysis
..... 'main' is the main thread
..... 'main' spawns new thread 'thread1'
..... 'main' spawns new thread 'thread2'
... LockAbstraction
... ThreadRefactoring
..... Separated call graph of 'thread1'
..... Separated call graph of 'thread2'
... SharedStateAnalysis
..... 'main' accesses '$M.0' '$M.1'
..... 'thread1' accesses '$M.1'
..... 'thread2' accesses '$M.1'
. StaticLocksetInstrumentation
... GlobalRaceCheckingInstrumentation
..... Instrumented lockset analysis globals for 'main'
..... Instrumented lockset analysis globals for 'thread1'
..... Instrumented lockset analysis globals for 'thread2'
... LocksetInstrumentation
..... Instrumented '0' locks in 'main'
..... Instrumented '0' unlocks in 'main'
..... Instrumented '0' locks in 'thread1'
..... Instrumented '0' unlocks in 'thread1'
..... Instrumented '0' locks in 'thread2'
..... Instrumented '0' unlocks in 'thread2'
... RaceCheckingInstrumentation
..... Instrumented '5' read accesses in 'main'
..... Instrumented '0' write accesses in 'main'
..... Instrumented '0' read accesses in 'thread1'
..... Instrumented '1' write access in 'thread1'
..... Instrumented '3' read accesses in 'thread2'
..... Instrumented '0' write accesses in 'thread2'
... SharedStateAbstraction
... LoopSummaryInstrumentation
..... Instrumented '0' loop invariant candidates in 'main'
..... Instrumented '0' loop invariant candidates in 'thread1'
..... Instrumented '0' loop invariant candidates in 'thread2'
... ErrorReportingInstrumentation
... AccessCheckingInstrumentation
..... Instrumented assertions for '$M.1'
. Cruncher
. StaticLocksetAnalysis
... RaceCheckAnalysis
..... Conflict in memory region: $M.1
..... 0 verified, 7 errors
... YieldInstrumentation
..... Instrumented '6' yields
. Done
```
