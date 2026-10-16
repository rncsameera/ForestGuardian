# Legacy standalone diagnosis prototype

The active ForestGuardian AI architecture is documented in [`../PythonAI/README.md`](../PythonAI/README.md). Unity gameplay invokes `PythonAI/bayesian.py` through `PythonAIBridge`; it does not use this earlier prototype or a C# Bayesian substitute.

`pond_diagnosis.py` remains as the original small prototype for reference. The full runtime model with normalized water, vegetation, wildlife, rainfall, and dead-vegetation evidence is `PythonAI/bayesian.py`.
