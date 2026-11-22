using UnityEngine;

public enum ElementType
{
    Fire,
    Water,
    Earth,
    Grass,
    Electric
}

public class BeastElement : MonoBehaviour
{
    public ElementType element = ElementType.Fire;
}

public static class ElementChart
{
    public static bool IsSuperEffective(ElementType attacker, ElementType defender)
    {
        return (attacker == ElementType.Fire && defender == ElementType.Grass) ||
            (attacker == ElementType.Grass && defender == ElementType.Earth) ||
            (attacker == ElementType.Earth && defender == ElementType.Electric) ||
            (attacker == ElementType.Electric && defender == ElementType.Water) ||
            (attacker == ElementType.Water && defender == ElementType.Fire);
    }
}
