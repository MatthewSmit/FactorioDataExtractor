using System;

namespace FactorioDataExtractor
{
    internal enum DependencyType
    {
        Hard,
        Incompatible,
        Optional,
        HiddenOptional,
    }

    internal enum DependencyEquality
    {
        None,
        LessThan,
        LessEquals,
        GreaterThan,
        GreaterEquals,
        Equals,
    }

    internal struct Dependency
    {
        public DependencyType Type;
        public string Name;
        public DependencyEquality Equality;
        public Version Version;
    }
}