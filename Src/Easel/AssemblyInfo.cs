using System.Runtime.CompilerServices;

// The mask compositor is internal plumbing; the render tests still want to exercise it directly.
[assembly: InternalsVisibleTo("Easel.Test")]
