lockpwn
====================
lockpwn is a static lockset analyser for concurrent Boogie programs.

We currently support only C and pthreads.

## Build instructions
1. Clone this project.
1. Compile using Visual Studio or Mono.

## How to use

The input to lockpwn is a concurrent C program translated to the Boogie intermediate verification language using the SMACK LLVM-to-Boogie translator.

Given an input ${PROGRAM} in Boogie, do the following:

```
.\lockpwn.exe ${PROGRAM}.bpl
```

The output is an instrumented Boogie program that can be fed to the Corral bug-finder.

## Tool options

Use /v for verbose mode or /v2 for super verbose mode. Use /time for timing information.
