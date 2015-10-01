lockpwn
====================
lockpwn is a lightweight symbolic lockset analyser for concurrent C programs.

We currently support only pthreads.

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
