using System;

namespace System.Runtime.CompilerServices
{
    // 1. 支持 init-only setters (C# 9)
    internal static class IsExternalInit {}
    // 2. 支持 required 关键字 (C# 11)
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal class RequiredMemberAttribute : System.Attribute {}
    // 3. 支持编译器特性检查 (C# 11+)
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal class CompilerFeatureRequiredAttribute : System.Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }
        public string FeatureName { get; }
    }
}



