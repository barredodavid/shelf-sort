// The bits of UnityEngine that LevelGen.cs touches, so it compiles as a plain .NET console app.
namespace UnityEngine
{
    public static class Mathf
    {
        public static int Min(int a, int b) => System.Math.Min(a, b);
        public static int CeilToInt(float f) => (int)System.Math.Ceiling(f);
        public static float Round(float f) => (float)System.Math.Round(f);
    }

    public class TextAsset
    {
        public string text = "";
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : class => null; // the baker never reads baked levels
    }
}
