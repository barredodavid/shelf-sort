#if UNITY_EDITOR // ad capture only: nothing of it goes into a player build
using UnityEngine;

// One ad video (AdDirector.cs): a hand-made level, the moves the fake finger plays on it, and how it is filmed.
[CreateAssetMenu(menuName = "Shelf Sort/Ad Scenario")]
public class AdScenario : ScriptableObject
{
    [Tooltip("One line in the format of Resources/levels.txt: time;hard;par;shelf|shelf|... A shelf is its rows joined by '/', front first, " +
             "one char per slot: 'a' empty, 'b'..'m' a product, 'B'..'M' the same product frozen, 'n'..'y' hidden in a mystery carton, 'z' a crate. " +
             "Shelves 0 and 1 are the top row (left, right), 2 and 3 the middle one, 4 and 5 the bottom one.")]
    [TextArea(2, 4)] public string level = "60;0;6;bba|bcc|eec|ded|dff|afa";
    [Tooltip("Country whose products fill the shelves: 0 France, 1 Italy, 2 Japan, 3 USA")]
    [Range(0, 3)] public int country;
    [Tooltip("Padlocked products, 'shelf.slot' separated by spaces; each opens after 3 moves")]
    public string locks = "";

    [Header("Moves")]
    [Tooltip("Separated by spaces: 'shelf.slot>shelf.slot' drags a product, 'shelf.slot>shelf' drops it on that shelf's first empty slot, " +
             "'shelf.slot' only taps it (a frozen or padlocked product says why it stays). '@1.5' at the end of one: wait that long before it instead of the delay.")]
    [TextArea(2, 6)] public string moves = "1.0>0.2 2.2>1.0";
    [Tooltip("Seconds between two moves")] public float delay = .3f;
    [Tooltip("Seconds of clock lost per second of video (0: the clock stands still). It stops just above 0.")]
    public float clockSpeed;

    [Header("Interface shown (the rest is hidden)")]
    public bool topBar;
    public bool timer;
    public bool shopSign;
    public bool bonuses;
    [Tooltip("The game's praise (SUPER! x2...) and the seconds flying to the clock")] public bool toasts = true;

    [Header("Filming")]
    [Tooltip("Time.timeScale from the last drop until the board settles (1: no slow motion)")]
    [Range(.1f, 1)] public float slowMo = 1;
    [Tooltip("Camera zoom reached by the last move, growing from 1 (1.1 = 10% closer)")]
    [Range(1, 1.5f)] public float zoom = 1;
    [Tooltip("Line at the top, in the game's font and outline")] public string hook = "";
    [Tooltip("Big text in the middle once the moves are played (empty: none)")] public string outro = "";
}
#endif
