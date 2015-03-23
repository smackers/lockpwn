lockpwn
====================
lockpwn is a static lockset analyser for multithreaded programs. We currently support only C and pthreads.

The input to lockpwn is a concurrent C program translated to the Boogie intermediate verification language using SMACK.

## Build instructions
1. Clone this project.
1. Compile using Visual Studio or Mono.

## How to use

Given an input ${PROGRAM} in Boogie, do the following:

```
.\lockpwn.exe ${PROGRAM}.bpl
```

Use /v for verbose mode or /v2 for super verbose mode. Use /time for timing information.
