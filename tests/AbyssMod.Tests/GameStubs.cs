// Minimal managed stand-ins for the generated interop types used by the translator.
// These tests exercise translation planning and property writes without loading Unity.
namespace Il2CppSystem
{
    public sealed class Type
    {
        public Type(string name) => Name = name;

        public string Name { get; }
    }
}

namespace Absf.Master
{
    public class IMasterLoadResult
    {
        public T Cast<T>()
            where T : IMasterLoadResult => (T)this;
    }

    public sealed class MasterLoadResult<T> : IMasterLoadResult
    {
        public System.Collections.Generic.List<T> Rows { get; set; }
    }
}

namespace Project.Master
{
    public sealed class MasterDataStore { }
}

namespace Project.Master.NoaMessagePack
{
    public sealed class MDescriptionTextColors
    {
        public string word { get; set; }
    }

    public sealed class MDescriptionTextColor
    {
        public string word { get; set; }
    }

    public sealed class MAbilityDetails
    {
        public string description { get; set; }
    }
}

namespace AbyssMod
{
    internal static class Logger
    {
        public static void Info(string message) { }

        public static void Warn(string message) =>
            throw new System.InvalidOperationException(message);
    }
}
