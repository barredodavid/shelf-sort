using UnityEngine;
using UnityEngine.Rendering.Universal;

// The shop's look: wall tiles, wood, panels, price tags, awning, and a neutral light so product colours stay true.
public partial class Game
{
    Light sun;
    ColorAdjustments colorAdj;

    (Color low, Color high) WallColors => (new Color(.92f, .66f, .55f), new Color(1f, .93f, .84f));
    Color WoodC => Wood;
    Color BackboardC => Backboard;
    Color TagC => Gold;
    Color AwningC => new(.3f, .78f, .62f);

    void ApplyLighting()
    {
        sun.color = new Color(1f, .96f, .9f);
        sun.intensity = 1.1f;
        RenderSettings.ambientLight = new Color(.58f, .6f, .64f);
        colorAdj.postExposure.value = .15f;
    }
}
