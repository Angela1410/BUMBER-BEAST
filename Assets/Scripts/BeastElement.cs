using UnityEngine;

// the 5 elements in the game
public enum ElementType
{
    Fire,
    Water,
    Earth,
    Grass,
    Electric
}

// put this on a Beast so the enemy knows which element hit it
public class BeastElement : MonoBehaviour
{
    public ElementType element = ElementType.Fire;
}

// the element chart: which element beats which
public static class ElementChart
{
    // true if the attacker's element beats the defender's (that means double damage)
    // the circle goes: Fire > Grass > Earth > Electric > Water > Fire
    public static bool IsSuperEffective(ElementType attacker, ElementType defender)
    {
        return (attacker == ElementType.Fire && defender == ElementType.Grass) ||
            (attacker == ElementType.Grass && defender == ElementType.Earth) ||
            (attacker == ElementType.Earth && defender == ElementType.Electric) ||
            (attacker == ElementType.Electric && defender == ElementType.Water) ||
            (attacker == ElementType.Water && defender == ElementType.Fire);
    }
}
