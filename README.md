lockpwn
====================
A lightweight, blazing fast symbolic analyser for concurrent C programs.

## Build instructions
1. Clone this project.
1. Compile using Visual Studio or Mono.

## How to use

The input to lockpwn is a concurrent C program translated to the Boogie intermediate verification language using the [SMACK](https://github.com/smackers/smack) LLVM-to-Boogie translator.

Given an input ${PROGRAM} in Boogie, do the following:

```
.\lockpwn.exe ${PROGRAM}.bpl
```

lockpwn can also easily integrate with Microsoft's Corral bounded bugfinder. This can be achieved as follows:

```
.\lockpwn.exe ${PROGRAM}.bpl /corral
```

The output, ${OUTPUT}.bpl, is an instrumented with context-switches (yields) Boogie program. This can be directly fed to the Corral bug-finder using the `/cooperative` option (so Corral does not automatically instrument `yield` statements).

## Tool options

Use `/v` for verbose mode or `/v2` for super verbose mode. Use `/time` for timing information.

## Publications
- **[Fast and Precise Symbolic Analysis of Concurrency Bugs in Device Drivers](http://multicore.doc.ic.ac.uk/publications/ase-15.html)**. Pantazis Deligiannis, Alastair F. Donaldson, Zvonimir Rakamarić. In the *30th IEEE/ACM International Conference on Automated Software Engineering* (ASE), 2015.
