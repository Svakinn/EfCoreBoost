//Shuts up warnings: warning MSTEST0001: Explicitly enable or disable tests parallelization

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]